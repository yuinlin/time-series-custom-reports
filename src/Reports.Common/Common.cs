using System;
using System.Data;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.Beta.ReportData.TimeSeriesData;

using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;

using TimeSeriesPoint = ReportPluginFramework.Beta.ReportData.TimeSeriesData.TimeSeriesPoint;
using InterpolationType = ReportPluginFramework.Beta.ReportData.TimeSeriesDescription.InterpolationType;

namespace Reports
{
    public class Common
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RunReportRequest _RunReportRequest;
        private int _WaterYearMonth = 10;
        public static string _DateFormat = "yyyy-MM-dd HH:mmzzz";

        public Dictionary<Guid, DateTimeOffsetInterval> _TimeSeriesTimeRangeIntervals = new Dictionary<Guid, DateTimeOffsetInterval>();

        public string _DllName;
        public string _DllFolder;

        public Common(RunReportRequest request)
        {
            _RunReportRequest = request;

            _WaterYearMonth = _RunReportRequest.ReportData.GetSystemConfiguration().WaterYearDefaultMonth;
        }

        private IReportData ReportData()
        {
            return _RunReportRequest.ReportData;
        }

        private IPublishGateway Publish()
        {
            return _RunReportRequest.Publish;
        }

        public int GetWaterYearMonth()
        {
            return _WaterYearMonth;
        }

        public TimeSpan GetReportTimeSpanOffset()
        {
            if ((_RunReportRequest.Inputs != null) && (_RunReportRequest.Inputs.TimeSeriesInputs.Count > 0))
            {
                foreach (TimeSeriesReportRequestInput timeseriesInput in _RunReportRequest.Inputs.TimeSeriesInputs)
                {
                    if (timeseriesInput.IsMaster)
                        return GetTimeSeriesOffset(timeseriesInput.UniqueId);
                }
                return GetTimeSeriesOffset(_RunReportRequest.Inputs.TimeSeriesInputs[0].UniqueId);
            }

            if ((_RunReportRequest.Inputs != null) && (_RunReportRequest.Inputs.LocationInput != null))
                return TimeSpan.FromHours(GetLocationData(_RunReportRequest.Inputs.LocationInput.Identifier).UtcOffset);

            return TimeSpan.Zero;
        }

        public DateTimeOffsetInterval GetPeriodSelectedAdjustedForReport()
        {
            DateTimeOffsetInterval interval = GetPeriodSelectedInUtcOffset(GetReportTimeSpanOffset());

            if ((_RunReportRequest.Inputs != null) &&
                (_RunReportRequest.Inputs.TimeSeriesInputs.Count == 0) &&
                (_RunReportRequest.Inputs.LocationInput != null))
            {
                interval = GetReportTimeRangeInLocationUtcOffset(_RunReportRequest.Inputs.LocationInput.Identifier);
            }
            
            return GroupByHandler.GetTrimmedPeriodSelected(interval);
        }

        public DataSet GetCommonDataSet(string dllName, string dllFolder)
        {
            _DllName = dllName;
            _DllFolder = dllFolder;
            Log.DebugFormat("GetCommonDataSet for dll {0} in folder {1}", dllName, dllFolder);

            AddRunReportRequestParametersFromSettingsFile();

            Log.Info(ReportInputInformation());

            return (new DataTablesBuilder(_RunReportRequest, this)).GetCommonDataSet(dllName, dllFolder);
        }

        public void AddRunReportRequestParametersFromSettingsFile()
        {
            string settingsFile = Path.Combine(_DllFolder, "Settings.json");
            Log.DebugFormat("AddSettings - look for settings in file = '{0}'", settingsFile);
            try
            {
                if (File.Exists(settingsFile))
                {
                    using (StreamReader reader = File.OpenText(settingsFile))
                    {
                        JObject jsonObject = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                        foreach (dynamic item in jsonObject["Parameters"])
                        {
                            Log.DebugFormat("item name = '{0}' value = '{1}'", item.Name, item.Value);
                            ReportJobParameter parameter = new ReportJobParameter()
                            {
                                Name = (string)item.Name,
                                Value = (string)item.Value
                            };
                            _RunReportRequest.Parameters.Add(parameter);
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Error reading settings from '{0}'", settingsFile), exp);
            }
        }

        public int GetParameterInt(string parameterName, int defaultValue)
        {
            int retValue = defaultValue;
            try
            {
                int? parameterValue = _RunReportRequest.Parameters.GetInt(parameterName);
                if (parameterValue.HasValue) retValue = parameterValue.Value;
            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Error in GetParameterInt parameterName = {0}, return defaultValue = {1}", parameterName, defaultValue), exp);
            }
            return retValue;
        }

        public double GetParameterDouble(string parameterName, double defaultValue)
        {
            double retValue = defaultValue;
            try
            {
                double? parameterValue = _RunReportRequest.Parameters.GetDouble(parameterName);
                if (parameterValue.HasValue) retValue = parameterValue.Value;
            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Error in GetParameterDouble parameterName = {0}, return defaultValue = {1}", parameterName, defaultValue), exp);
            }
            return retValue;
        }

        public string GetParameterString(string parameterName, string defaultValue)
        {
            string retValue = defaultValue;
            try
            {
                string parameterValue = _RunReportRequest.Parameters.GetString(parameterName);
                if (!string.IsNullOrEmpty(parameterValue)) retValue = parameterValue;
            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Error in GetParameterString parameterName = {0}, return defaultValue = {1}", parameterName, defaultValue), exp);
            }
            return retValue;
        }

        public string GetTimeSeriesIdentifierString(Guid timeseriesUniqueId, string preFix)
        {
            return preFix + ": " + GetTimeSeriesInformation(timeseriesUniqueId);
        }

        public string GetTimeSeriesInformation(Guid timeseriesUniqueId)
        {
            string retValue = GetTimeSeriesDescription(timeseriesUniqueId).Identifier;

            string locationName = GetLocationName(GetTimeSeriesDescription(timeseriesUniqueId).LocationIdentifier);

            if (!string.IsNullOrEmpty(locationName))
            {
                retValue += ", " + locationName;
            }
            return retValue;
        }

        public string GetLocationName(string locationIdentifier)
        {
            LocationDescription locationDescription = GetLocationDescriptionByIdentifier(locationIdentifier);
            return (locationDescription != null)? locationDescription.Name : "";
        }

        public double GetTotalPointCount(Guid timeSeriesUniqueId)
        {
            bool? requiresNoCoverage = null;
            double? noCoverageAmount = null;

            List<TimeSeriesPoint> points = 
                GetComputedStatisticsPoints(timeSeriesUniqueId, StatisticType.Count, StatisticPeriod.Annual, requiresNoCoverage, noCoverageAmount);

            double count = 0;
            foreach (TimeSeriesPoint point in points)
            {
                count += (point.Value.HasValue) ? point.Value.Value : 0;
            }

            return count;
        }

        public string GetTimeRangeString(Guid timeseriesUniqueId, DateTimeOffsetInterval timerange)
        {
            string dateFormat = "yyyy-MM-dd HH:mm:ss";
            string retValue = "Utc Offset: " + GetOffsetString(GetTimeSeriesDescription(timeseriesUniqueId).UtcOffset);

            if (timerange.Start.HasValue && timerange.End.HasValue)
            {
                retValue += ", Start Time: " + timerange.Start.Value.ToString(dateFormat);
                retValue += ", End Time: " + timerange.End.Value.ToString(dateFormat);
            }
            else
            {
                retValue += ", No Data";
            }
            return retValue;
        }

        public Guid? GetTimeSeriesInputByName(string timeseriesInputName)
        {
            Log.DebugFormat("GetTimeSeriesInputByName: look for input with timeseriesInputName = {0}", timeseriesInputName);
            ReportRequestInputs inputs = _RunReportRequest.Inputs;
            if (inputs == null) return null;

            foreach (TimeSeriesReportRequestInput timeseriesInput in _RunReportRequest.Inputs.TimeSeriesInputs)
                if (timeseriesInput.Name == timeseriesInputName)
                {
                    Guid uniqueId = timeseriesInput.UniqueId;
                    Log.DebugFormat("GetTimeSeriesInputByName: found input with timeseriesInputName = {0} and UniqueId = {1}", timeseriesInputName, uniqueId);
                    return uniqueId;
                }
            return null;
        }

        public TimeSeriesDescription GetTimeSeriesDescription(Guid timeseriesUniqueId)
        {
            TimeSeriesDescriptionListByUniqueIdServiceRequest tsDescRequest = new TimeSeriesDescriptionListByUniqueIdServiceRequest();
            tsDescRequest.TimeSeriesUniqueIds = new List<Guid>() { timeseriesUniqueId };
            TimeSeriesDescriptionListByUniqueIdServiceResponse tsDescResponse = Publish().Get(tsDescRequest);
            if (tsDescResponse.TimeSeriesDescriptions.Count > 0)
                return tsDescResponse.TimeSeriesDescriptions[0];

            Log.InfoFormat("GetTimeSeriesDescription for guid = {0} not found, returning null", timeseriesUniqueId);
            return null;
        }

        public LocationDescription GetLocationDescriptionByIdentifier(string locationIdentifier)
        {
            var locationDescriptionListRequest = new LocationDescriptionListServiceRequest();
            locationDescriptionListRequest.LocationIdentifier = locationIdentifier;
            var locationDescriptions = Publish().Get(locationDescriptionListRequest).LocationDescriptions;
            return (locationDescriptions.Count > 0)? locationDescriptions[0] : null;
        }

        public LocationDataServiceResponse GetLocationData(string locationIdentifier)
        {
            var locationDataRequest = new LocationDataServiceRequest();
            locationDataRequest.LocationIdentifier = locationIdentifier;
            return Publish().Get(locationDataRequest);
        }

        public string GetPeriodSelectedInformation(DateTimeOffsetInterval interval)
        {
            return "Period Selected: " + PeriodSelectedString(interval);
        }

        public string GetTimeSeriesUnitInformation(Guid timeseriesUniqueId)
        {
            return "Units: " + GetTimeSeriesUnitSymbol(timeseriesUniqueId);
        }

        public string GetTimeSeriesUnitSymbol(Guid timeseriesUniqueId)
        {
            return GetUnitSymbol(GetTimeSeriesDescription(timeseriesUniqueId).Unit); 
        }

        public string GetUnitSymbol(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return "";

            var units = ReportData().GetUnits();
            foreach (Unit u in units) if (unitId == u.Identifier) return u.Symbol;
            return unitId;
        }

        public string PeriodSelectedString(DateTimeOffsetInterval interval)
        {
            string timeRange;
            if (!interval.Start.HasValue && !interval.End.HasValue)
            {
                timeRange = "Entire Record";
            }
            else if (!interval.Start.HasValue)
            {
                timeRange = string.Format("From Beginning of Record to {0}", FormatDateTimeOffset(interval.End.Value));
            }
            else if (!interval.End.HasValue)
            {
                timeRange = string.Format("{0} to End of Record", FormatDateTimeOffset(interval.Start.Value));
            }
            else
            {
                var localStartTime = interval.Start.Value;
                var localEndTime = localStartTime == interval.End.Value ? interval.End.Value : interval.End.Value.AddTicks(-1);
                timeRange = FormattableString.Invariant($"{FormatDateTimeOffset(localStartTime)} - {FormatDateTimeOffset(localEndTime)}");
            }
            return timeRange;
        }

        public string FormatDateTimeOffset(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("yyyy-MM-dd HH:mm");
        }

        public TimeSpan GetTimeSeriesOffset(Guid timeseriesUniqueId)
        {
            TimeSeriesDescription tsd = GetTimeSeriesDescription(timeseriesUniqueId);
            return TimeSpan.FromHours(tsd.UtcOffset);
        }

        public string GetOffsetString(double offset)
        {
            TimeSpan tsSpan = TimeSpan.FromHours(offset);
            return GetOffsetString(tsSpan);
        }

        public string GetOffsetString(TimeSpan offset)
        {
            return ((offset < TimeSpan.Zero) ? "-" : "+") + offset.ToString(@"hh\:mm");
        }

        public DateTimeOffsetInterval GetReportTimeRangeInTimeSeriesUtcOffset(Guid timeseriesUniqueId)
        {
            TimeSpan tsOffset = GetTimeSeriesOffset(timeseriesUniqueId);

            return GetPeriodSelectedInUtcOffset(tsOffset);
        }

        public DateTimeOffsetInterval GetReportTimeRangeInLocationUtcOffset(string locationIdentifier)
        {
            TimeSpan tsOffset = TimeSpan.FromHours(GetLocationData(locationIdentifier).UtcOffset);

            DateTimeOffsetInterval interval = GetPeriodSelectedInUtcOffset(tsOffset);

            DateTimeOffset? startTime = null;
            DateTimeOffset? endTime = null;

            if (interval.Start.HasValue) startTime = interval.Start.Value.Subtract(tsOffset);
            if (interval.End.HasValue) endTime = interval.End.Value.Subtract(tsOffset);

            return new DateTimeOffsetInterval(startTime, endTime);
        }

        public DateTimeOffsetInterval GetPeriodSelectedInUtcOffset(TimeSpan utcOffset)
        {
            return GetIntervalInUtcOffset(_RunReportRequest.Interval, utcOffset);
        }

        public bool PeriodSelectedIsWaterYear()
        {
            return PeriodSelectedIsWaterYear(GetReportTimeSpanOffset());
        }
        public bool PeriodSelectedIsWaterYear(TimeSpan utcOffset)
        {
            DateTimeOffset? intervalStartTime = GetPeriodSelectedInUtcOffset(utcOffset).Start;
            if (intervalStartTime.HasValue)
            {
                Log.DebugFormat("PeriodSelectedIsWaterYear waterYearMonth = {0}, periodSelected = {1}, Period selected in offset = {2} is {3}",
                  GetWaterYearMonth(),  TimeRangeString(_RunReportRequest.Interval), utcOffset, TimeRangeString(GetPeriodSelectedInUtcOffset(utcOffset)));
            }
            return (intervalStartTime.HasValue) && (intervalStartTime.Value.Month == GetWaterYearMonth());
        }

        public DateTimeOffsetInterval GetIntervalInUtcOffset(DateTimeOffsetInterval interval, TimeSpan utcOffset)
        {
            DateTimeOffset? startTime = interval.Start;
            DateTimeOffset? endTime = interval.End;

            if (startTime.HasValue) startTime = startTime.Value.ToOffset(utcOffset);
            if (endTime.HasValue) endTime = endTime.Value.ToOffset(utcOffset);

            DateTimeOffsetInterval newInterval = new DateTimeOffsetInterval(startTime, endTime);

            Log.DebugFormat("GetIntervalInUtcOffset request interval = {0}, utcOffset = {1}, returns interval = {2}",
                TimeRangeString(interval), utcOffset, TimeRangeString(newInterval));

            return newInterval;
        }

        public string TimeSeriesRangeString(DateTimeOffsetInterval interval)
        {
            if (!interval.Start.HasValue && !interval.End.HasValue)
                return "No Points";
            return TimeIntervalAsString(interval, _DateFormat);
        }

        public string TimeRangeString(DateTimeOffsetInterval interval)
        {
            return TimeIntervalAsString(interval, _DateFormat);
        }

        public string TimeIntervalAsString(DateTimeOffsetInterval interval, string dateFormat)
        {
            DateTimeOffset? startTime = interval.Start;
            DateTimeOffset? endTime = interval.End;

            string startString = (startTime.HasValue) ? startTime.Value.ToString(dateFormat) : "Start of Record";
            string endString = (endTime.HasValue) ? endTime.Value.ToString(dateFormat) : "End of Record";
            Log.DebugFormat("TimeRangeString: interval time range is {0} to {1}", startString, endString);

            return startString + " - " + endString;
        }

        public CoverageOptions GetCoverageOptions()
        {
            double dataCoverage = GetParameterDouble("DataCoverageThreshold", -1.0);

            bool useCoverage = (dataCoverage < 0) ? false : true;
            double coverageAmount = dataCoverage / 100.0;
            coverageAmount = (coverageAmount < 0.0) ? 0.0 : ((coverageAmount > 1.0) ? 1.0 : coverageAmount);

            return new CoverageOptions
            {
                CoverageThreshold = coverageAmount,
                RequiresMinimumCoverage = useCoverage
            };
        }

        public string GetCoverageString(CoverageOptions coverageOptions)
        {
            bool useCoverage = (coverageOptions.RequiresMinimumCoverage.HasValue)? coverageOptions.RequiresMinimumCoverage.Value : false;
            double coverageAmount = (coverageOptions.CoverageThreshold.HasValue)? coverageOptions.CoverageThreshold.Value : 0.0;
            return string.Format("Data Coverage Threshold: {0}", (useCoverage) ? (coverageAmount * 100.0).ToString() + "%" : "n/a");
        }

        public StatisticType GetStatistic(string statistic, StatisticType defaultStatisticType)
        {
            try
            {
                return (StatisticType)Enum.Parse(typeof(StatisticType), statistic);
            }
            catch { }

            return defaultStatisticType;
        }

        public List<TimeSeriesPoint> GetComputedStatisticsPoints(Guid timeseriesUniqueId,
            StatisticType statType, StatisticPeriod period, bool? requiresCoverage, double? coverageAmount)
        {
            return GetComputedStatisticsPoints(timeseriesUniqueId, null, null, statType, period, requiresCoverage, coverageAmount);
        }

        public List<TimeSeriesPoint> GetComputedStatisticsPoints(Guid timeseriesUniqueId, DateTimeOffset? startTime, DateTimeOffset? endTime,
            StatisticType statType, StatisticPeriod period, bool? requiresCoverage, double? coverageAmount)
        {
            return GetComputedStatisticsPoints(timeseriesUniqueId, startTime, endTime, statType, period, null, requiresCoverage, coverageAmount);
        }

        public List<TimeSeriesPoint> GetComputedStatisticsPoints(Guid timeseriesUniqueId, DateTimeOffset? startTime, DateTimeOffset? endTime,
            StatisticType statType, StatisticPeriod period, int? periodCount, bool? requiresCoverage, double? coverageAmount)
        {
            Log.DebugFormat("GetComputedStatisticsPoints stat = {0}, period = {1}, periodCount = {2} for TimeRange = '{3}' - '{4}'", statType, period, periodCount, startTime, endTime);

            CoverageOptions co = new CoverageOptions();
            co.RequiresMinimumCoverage = requiresCoverage;
            co.CoverageThreshold = coverageAmount;
            co.PartialCoverageGradeCode = -1;

            TimeSeriesComputedStatisticsRequest compRequest = new TimeSeriesComputedStatisticsRequest();
            compRequest.TimeSeriesUniqueId = timeseriesUniqueId;
            compRequest.StatisticType = statType;
            compRequest.StatisticPeriod = period;
            compRequest.StatisticPeriodCount = periodCount;
            compRequest.CoverageOptions = co;
            compRequest.QueryFromTime = startTime;
            compRequest.QueryToTime = (endTime.HasValue) ? endTime.Value.AddMilliseconds(-1) : endTime;

            return ReportData().GetTimeSeriesComputedStatistics(compRequest).Points;
        }

        public string GetTimeSeriesInterpolationTypeString(Guid timeseriesUniqueId)
        {
            InterpolationType interpolationType = GetTimeSeriesInterpolationType(timeseriesUniqueId);
            return string.Format("{0} - {1}", (int)interpolationType, interpolationType.ToString());
        }

        public int GetBinAdjustment(Guid timeseriesUniqueId)
        {
            return (HasEndBinInterpolationType(timeseriesUniqueId)) ? -1 : 0;
        }

        public bool HasEndBinInterpolationType(Guid timeseriesUniqueId)
        {
            return IsEndBinInterpolationType(GetTimeSeriesInterpolationType(timeseriesUniqueId));
        }

        public InterpolationType GetTimeSeriesInterpolationType(Guid timeseriesUniqueId)
        {
            return ReportData().GetTimeSeriesDescription(timeseriesUniqueId).InterpolationType;
        }

        public bool IsEndBinInterpolationType(InterpolationType interpolationType)
        {
            return (interpolationType == InterpolationType.InstantaneousTotals ||
                   interpolationType == InterpolationType.PrecedingTotals) ? true : false;
        }

        public DateTimeOffsetInterval GetTimeSeriesTimeRange(Guid timeseriesUniqueId)
        {
            Log.DebugFormat("Get the time-range for uniqueId = {0}", timeseriesUniqueId);

            if (!_TimeSeriesTimeRangeIntervals.ContainsKey(timeseriesUniqueId))
            {
                Log.DebugFormat("Find the time-range for uniqueId = {0}", timeseriesUniqueId);
                DateTimeOffsetInterval timeseriesInterval = (new TimeSeriesTimeRangeFinder(this)).FindTimeSeriesTimeRange(timeseriesUniqueId);
                _TimeSeriesTimeRangeIntervals.Add(timeseriesUniqueId, timeseriesInterval);
            }

            DateTimeOffsetInterval interval = _TimeSeriesTimeRangeIntervals[timeseriesUniqueId];         

            Log.DebugFormat("Time-range for uniqueId = {0} is '{1}'", timeseriesUniqueId, TimeSeriesRangeString(interval));

            return interval;
        }

        public string GetTimeSeriesTimeRangeString(Guid timeseriesUniqueId)
        {
            DateTimeOffsetInterval interval = GetTimeSeriesTimeRange(timeseriesUniqueId);
            return TimeSeriesRangeString(interval);
        }

        public string GetTimeSeriesTimeRangeInformation(Guid timeseriesUniqueId)
        {
            DateTimeOffsetInterval interval = GetTimeSeriesTimeRange(timeseriesUniqueId);
            return GetTimeRangeString(timeseriesUniqueId, interval);
        }

        public List<TimeSeriesPoint> GetTimeSeriesPoints(Guid timeseriesUniqueId, DateTimeOffset? StartTime, DateTimeOffset? EndTime)
        {
            Log.DebugFormat("GetTimeSeriesPoints uniqueId = {0} from {1} to {2}", timeseriesUniqueId,
                (StartTime.HasValue) ? StartTime.Value.ToString(_DateFormat) : "start of record",
                (EndTime.HasValue) ? EndTime.Value.ToString(_DateFormat) : "end of record");

            TimeSeriesPointsRequest request = new TimeSeriesPointsRequest();
            request.TimeSeriesUniqueId = timeseriesUniqueId;
            request.QueryFromTime = StartTime;
            request.QueryToTime = EndTime;
            request.TimeSeriesDataType = TimeSeriesDataType.Corrected;
            request.IncludeGapMarkers = true;

            return ReportData().GetTimeSeriesPoints(request).Points;
        }

        public static string FormatDoubleValue(double value, bool fix, int places, string missingStr)
        {
            return DoubleValueFormatter.FormatDoubleValue(value, fix, places, missingStr);
        }

        public static string FormatSigFigsNumber(double value, int sigfigs)
        {
            return DoubleValueFormatter.FormatSigFigsNumber(value, sigfigs);
        }

        public string ReportInputInformation()
        {
            string newLine = Environment.NewLine;
            string dateFormat = "yyyy-MM-dd HH:mm:ss.ffffffzzz";
            RunReportRequest runReportRequest = _RunReportRequest;
            string message = string.Format("{0}Report: {1}", newLine, _DllName);
            message += string.Format("{0}RunReportRequest Interval: {1}", newLine, TimeIntervalAsString(_RunReportRequest.Interval, dateFormat));
            message += string.Format("{0}Period Selected Adjusted for Report: {1}", newLine, TimeIntervalAsString(GetPeriodSelectedAdjustedForReport(), dateFormat));
            message += string.Format("{0}Formatted Period Selected: ", newLine);
            message += string.Format("{0}{1}Report offset: {2}", 
                PeriodSelectedString(GetPeriodSelectedAdjustedForReport()), newLine, GetOffsetString(GetReportTimeSpanOffset()));
            message += string.Format("{0}TimeSeriesInputs: {1}", newLine, newLine);
            if ((runReportRequest.Inputs != null) && (runReportRequest.Inputs.TimeSeriesInputs != null))
                foreach (TimeSeriesReportRequestInput timeseries in runReportRequest.Inputs.TimeSeriesInputs)
                    message += string.Format("Name = '{0}', UniqueId = '{1}', IsMaster= '{2}', Identifier= '{3}', utcOffset= {4}{5}", 
                        timeseries.Name, timeseries.UniqueId, timeseries.IsMaster,
                        GetTimeSeriesDescription(timeseries.UniqueId).Identifier, 
                        GetOffsetString(GetTimeSeriesDescription(timeseries.UniqueId).UtcOffset), newLine);

            message += string.Format("{0}LocationInput: {1}", newLine, newLine);
            if ((runReportRequest.Inputs != null) && (runReportRequest.Inputs.LocationInput != null))
                message += string.Format("Name = '{0}', Identifier = '{1}', utcOffset = {2}{3}",
                    runReportRequest.Inputs.LocationInput.Name, runReportRequest.Inputs.LocationInput.Identifier, 
                    GetOffsetString(GetLocationData(runReportRequest.Inputs.LocationInput.Identifier).UtcOffset), newLine);

            message += string.Format("{0}Report Settings: {1}", newLine, newLine);
            if (runReportRequest.Parameters != null)
                foreach (ReportJobParameter parameter in runReportRequest.Parameters)
                    message += string.Format("report parameter '{0}' = '{1}'{2}", parameter.Name, parameter.Value, newLine);

            return message;
        }
    }
}

