using System;
using System.Data;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
using System.Linq;
using ServiceStack;
using System.Threading;
using System.Globalization;

using ReportPluginFramework.Properties;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using ReportPluginFramework.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.ReportData.TimeSeriesData;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.Enum;

using PerpetuumSoft.Reporting.Components;
using PerpetuumSoft.Reporting.DOM;
using PerpetuumSoft.Reporting.Export.Pdf;
using PerpetuumSoft.Reporting.Export.Csv;
using PerpetuumSoft.Reporting.Export.OpenXML;
using PerpetuumSoft.Reporting.Export.Graph;
using PerpetuumSoft.Reporting.Rendering;

using TimeSeriesPoint = ReportPluginFramework.ReportData.TimeSeriesData.TimeSeriesPoint;
using InterpolationType = ReportPluginFramework.ReportData.TimeSeriesDescription.InterpolationType;
  



// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\Common.cs"
namespace TimeSeriesPlotNamespace
{
    public class Common
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RunFileReportRequest _RunReportRequest;
        private int _WaterYearMonth = 10;
        public static string _DateFormat = "yyyy-MM-dd HH:mmzzz";

        public Dictionary<Guid, DateTimeOffsetInterval> _TimeSeriesTimeRangeIntervals = new Dictionary<Guid, DateTimeOffsetInterval>();

        public string _DllName;
        public string _DllFolder;

        public Common(RunFileReportRequest request)
        {
            _RunReportRequest = request;

            _WaterYearMonth = _RunReportRequest.ReportData.GetSystemConfiguration().WaterYearDefaultMonth;
        }

        public RunFileReportRequest RunReportRequest()
        {
            return _RunReportRequest;
        }

        public IReportData ReportData()
        {
            return _RunReportRequest.ReportData;
        }

        public IPublishGateway Publish()
        {
            return _RunReportRequest.Publish;
        }

        public int GetWaterYearMonth()
        {
            return _WaterYearMonth;
        }

        public void CheckReportDefinitionVersion(string requiredVersion)
        {
            bool containsVersionKey = _RunReportRequest.ReportDefinitionMetadata.ContainsKey("Version");

            string version = (containsVersionKey) ? _RunReportRequest.ReportDefinitionMetadata["Version"] : "1";

            if (version != requiredVersion)
            {
                string msg = string.Format(
                    "Exception due to an incompatible version of report definition: the report definition is '{0}' " +
                    "but this report requires '{1}', check the value of the " +
                    "System Configuration Tool setting ReportPluginConfig-{2}",
                    version, requiredVersion, _DllName);
                throw new Exception(msg);
            }
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

            AddRunReportRequestParametersFromDefinitionMetadata();

            Log.Info(ReportInputInformation());

            return GetDataTablesBuilder().GetCommonDataSet(dllName, dllFolder);
        }

        public DataTablesBuilder GetDataTablesBuilder()
        {
            return new DataTablesBuilder(_RunReportRequest, this);
        }

        public void AddRunReportRequestParametersFromDefinitionMetadata()
        {
            Dictionary<string, string> metadatas = _RunReportRequest.ReportDefinitionMetadata;

            if ((metadatas == null) || (metadatas.Count == 0))
                return;

            foreach (string key in metadatas.Keys)
            {
                Log.DebugFormat("metatdata item key = '{0}' value = '{1}'", key, metadatas[key]);
                try
                {
                    ReportJobParameter parameter = new ReportJobParameter()
                    {
                        Name = key,
                        Value = metadatas[key]
                    };
                    _RunReportRequest.Parameters.Add(parameter);
                }
                catch (Exception exp)
                {
                    Log.Error(string.Format("Error adding report parameter from metadata '{0}', value = '{1}'", key, metadatas[key]), exp);
                }
            }
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
            return (locationDescription != null) ? locationDescription.Name : "";
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
            string retValue = Resources.UtcOffset + ": " + GetOffsetString(GetTimeSeriesDescription(timeseriesUniqueId).UtcOffset);

            if (timerange.Start.HasValue && timerange.End.HasValue)
            {
                retValue += ", " + Resources.StartTime + ": " + timerange.Start.Value.ToString(dateFormat);
                retValue += ", " + Resources.EndTime + ": " + timerange.End.Value.ToString(dateFormat);
            }
            else
            {
                retValue += ", " + Resources.NoData;
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

        public TimeSeriesDescription GetTimeSeriesDescription(string locIdentifier, string parameter, string label)
        {
            try
            {
                TimeSeriesDescriptionServiceRequest request = new TimeSeriesDescriptionServiceRequest();

                request.LocationIdentifier = locIdentifier;
                request.Parameter = parameter;

                TimeSeriesDescriptionListServiceResponse response = Publish().Get(request);

                foreach (TimeSeriesDescription tsDesc in response.TimeSeriesDescriptions)
                    if (tsDesc.Label == label) return tsDesc;
            }
            catch { }

            return null;
        }

        public LocationDescription GetLocationDescriptionByIdentifier(string locationIdentifier)
        {
            var locationDescriptionListRequest = new LocationDescriptionListServiceRequest();
            locationDescriptionListRequest.LocationIdentifier = locationIdentifier;
            var locationDescriptions = Publish().Get(locationDescriptionListRequest).LocationDescriptions;
            return (locationDescriptions.Count > 0) ? locationDescriptions[0] : null;
        }

        public LocationDataServiceResponse GetLocationData(string locationIdentifier)
        {
            var locationDataRequest = new LocationDataServiceRequest();
            locationDataRequest.LocationIdentifier = locationIdentifier;
            return Publish().Get(locationDataRequest);
        }

        public ParameterMetadata GetParameterMetadata(string parameterName)
        {
            var request = new ParameterListServiceRequest();
            var parameters = Publish().Get(request).Parameters;
            foreach (ParameterMetadata parameterMetaData in parameters)
                if (parameterMetaData.Identifier == parameterName)
                    return parameterMetaData;

            Log.InfoFormat("GetParameterMetadata for parameterName = {0} not found, returning null", parameterName);
            return null;
        }

        public string GetPeriodSelectedInformation(DateTimeOffsetInterval interval)
        {
            return Resources.PeriodSelected + ": " + PeriodSelectedString(interval);
        }

        public string GetTimeSeriesUnitInformation(Guid timeseriesUniqueId)
        {
            return Resources.Units + ": " + GetTimeSeriesUnitSymbol(timeseriesUniqueId);
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

        public string GetGradeDisplayName(int? gradeCode)
        {
            GradeMetadata gradeMetadata = GetGradeMetadata(gradeCode);
            if (gradeMetadata != null) return gradeMetadata.DisplayName;

            return "";
        }

        public string GetGradeDisplayName(string gradeCode)
        {
            GradeMetadata gradeMetadata = GetGradeMetadata(gradeCode);
            if (gradeMetadata != null) return gradeMetadata.DisplayName;

            return "";
        }

        public GradeMetadata GetGradeMetadata(int? gradeCode)
        {
            if (!gradeCode.HasValue) return null;

            return GetGradeMetadata(gradeCode.Value.ToString());
        }

        public GradeMetadata GetGradeMetadata(string gradeCode)
        {
            if (string.IsNullOrEmpty(gradeCode)) return null;

            var request = new GradeListServiceRequest();
            var grades = Publish().Get(request).Grades;
            foreach (GradeMetadata gradeMetaData in grades)
                if (gradeMetaData.Identifier == gradeCode)
                    return gradeMetaData;

            Log.InfoFormat("GetGradeMetadata for gradeCode = {0} not found, returning null", gradeCode);
            return null;
        }

        public double GetPercentUncertainty(DischargeUncertainty dischargeUncertainty)
        {
            if (dischargeUncertainty.ActiveUncertaintyType == UncertaintyType.None)
                return double.NaN;
            if (dischargeUncertainty.ActiveUncertaintyType == UncertaintyType.Quantitative)
                return (dischargeUncertainty.QuantitativeUncertainty.Numeric.HasValue) ?
                    dischargeUncertainty.QuantitativeUncertainty.Numeric.Value : double.NaN;
            if (dischargeUncertainty.ActiveUncertaintyType == UncertaintyType.Qualitative)
                return GetPercentUncertaintyForQualitativeGrade(dischargeUncertainty.QualitativeUncertainty);
            return double.NaN;
        }

        public double GetPercentUncertaintyForQualitativeGrade(QualitativeUncertaintyType qualitativeUncertainty)
        {
            switch (qualitativeUncertainty)
            {
                case QualitativeUncertaintyType.Excellent:
                    return 2;
                case QualitativeUncertaintyType.Good:
                    return 5;
                case QualitativeUncertaintyType.Fair:
                    return 8;
                case QualitativeUncertaintyType.Poor:
                    return 10;
            }
            return double.NaN;
        }

        public string PeriodSelectedString(DateTimeOffsetInterval interval)
        {
            string timeRange;
            if (!interval.Start.HasValue && !interval.End.HasValue)
            {
                timeRange = Resources.EntireRecord;
            }
            else if (!interval.Start.HasValue)
            {
                timeRange = string.Format(Resources.FromBeginningOfRecordToX, FormatDateTimeOffset(interval.End.Value));
            }
            else if (!interval.End.HasValue)
            {
                timeRange = string.Format(Resources.XToEndOfRecord, FormatDateTimeOffset(interval.Start.Value));
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
                  GetWaterYearMonth(), TimeRangeString(_RunReportRequest.Interval), utcOffset, TimeRangeString(GetPeriodSelectedInUtcOffset(utcOffset)));
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
                return Resources.NoPoints;
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
            double dataCoverage = GetParameterDouble("DataCoverageThresholdPercent", -1.0);

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
            bool useCoverage = (coverageOptions.RequiresMinimumCoverage.HasValue) ? coverageOptions.RequiresMinimumCoverage.Value : false;
            double coverageAmount = (coverageOptions.CoverageThreshold.HasValue) ? coverageOptions.CoverageThreshold.Value : 0.0;
            return string.Format(Resources.DataCoverageThreshold + ": {0}", (useCoverage) ?
                (coverageAmount * 100.0).ToString() + "%" : Resources.NotApplicableAbbreviated);
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
            return GetComputedStatisticsResponse(timeseriesUniqueId, startTime, endTime, statType, period, periodCount, requiresCoverage, coverageAmount).Points;
        }

        public TimeSeriesComputedStatisticsResponse GetComputedStatisticsResponse(Guid timeseriesUniqueId, DateTimeOffset? startTime, DateTimeOffset? endTime,
    StatisticType statType, StatisticPeriod period, int? periodCount, bool? requiresCoverage, double? coverageAmount)
        {
            Log.DebugFormat("GetComputedStatisticsResponse stat = {0}, period = {1}, periodCount = {2} for TimeRange = '{3}' - '{4}'", statType, period, periodCount, startTime, endTime);

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

            return ReportData().GetTimeSeriesComputedStatistics(compRequest);
        }

        public List<int> GetComputedStatisticsGrades(Guid timeseriesUniqueId, DateTimeOffset? startTime, DateTimeOffset? endTime,
            StatisticType statType, StatisticPeriod period, int? periodCount, bool? requiresCoverage, double? coverageAmount)
        {
            TimeSeriesComputedStatisticsResponse response = GetComputedStatisticsResponse(timeseriesUniqueId,
                startTime, endTime, statType, period, periodCount, requiresCoverage, coverageAmount);

            List<TimeSeriesPoint> points = response.Points;
            List<GradeTimeRange> gradeRanges = response.GradeRanges;

            return GetTimeSeriesPointsGrades(points, gradeRanges);
        }

        public List<int> GetTimeSeriesPointsGrades(List<TimeSeriesPoint> points, List<GradeTimeRange> gradeRanges)
        {
            var pointwiseGrades = new List<int>();
            var gradeIndex = 0;

            foreach (var point in points)
            {
                int grade = -1;
                while (gradeIndex < gradeRanges.Count && gradeRanges[gradeIndex].EndTime <= point.Timestamp) ++gradeIndex;
                if ((gradeIndex < gradeRanges.Count) && (gradeRanges[gradeIndex].StartTime <= point.Timestamp)) grade = gradeRanges[gradeIndex].GradeCode;
                pointwiseGrades.Add(grade);
            }
            return pointwiseGrades;
        }

        public string GetGradeMarker(int code)
        {
            foreach (ReportJobParameter parm in _RunReportRequest.Parameters)
            {
                if (parm.Name.StartsWith("GradeMarker"))
                {
                    string gradeCode = parm.Name.Replace("GradeMarker", "").Trim();

                    if (gradeCode == code.ToString())
                    {
                        return parm.Value;
                    }
                }
            }
            return "";
        }

        public string GetTimeSeriesInterpolationTypeString(Guid timeseriesUniqueId)
        {
            InterpolationType interpolationType = GetTimeSeriesInterpolationType(timeseriesUniqueId);
            string interpolationTypeString = GetLocalizedEnumValue(interpolationType.GetType().Name, interpolationType.ToString());
            return string.Format("{0} - {1}", (int)interpolationType, interpolationTypeString);
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
            return GetTimeSeriesDataResponse(timeseriesUniqueId, StartTime, EndTime).Points;
        }

        public TimeSeriesDataResponse GetTimeSeriesDataResponse(Guid timeseriesUniqueId, DateTimeOffset? StartTime, DateTimeOffset? EndTime)
        {
            Log.DebugFormat("GetTimeSeriesDataResponse uniqueId = {0} from {1} to {2}", timeseriesUniqueId,
                (StartTime.HasValue) ? StartTime.Value.ToString(_DateFormat) : "start of record",
                (EndTime.HasValue) ? EndTime.Value.ToString(_DateFormat) : "end of record");

            TimeSeriesDataRequest request = new TimeSeriesDataRequest();
            request.TimeSeriesUniqueId = timeseriesUniqueId;
            request.QueryFromTime = StartTime;
            request.QueryToTime = EndTime;
            request.TimeSeriesDataType = TimeSeriesDataType.Corrected;
            request.IncludeGapMarkers = true;

            return ReportData().GetTimeSeriesData(request);
        }

        public TimeSeriesDataServiceResponse GetTimeSeriesCorrectedDataReponse(Guid uniqueId, string getParts, 
            DateTimeOffset? StartTime, DateTimeOffset? EndTime)
        {
            TimeSeriesDataCorrectedServiceRequest request = new TimeSeriesDataCorrectedServiceRequest();
            request.ApplyRounding = true;
            request.GetParts = getParts;
            request.IncludeGapMarkers = true;
            request.TimeSeriesUniqueId = uniqueId;
            request.QueryFrom = StartTime;
            request.QueryTo = EndTime;

            return Publish().Get(request);
        }

        public TimeSeriesDataServiceResponse GetTimeSeriesRawDataReponse(Guid uniqueId, string getParts,
            DateTimeOffset? StartTime, DateTimeOffset? EndTime)
        {
            TimeSeriesDataRawServiceRequest request = new TimeSeriesDataRawServiceRequest();
            request.ApplyRounding = true;
            request.GetParts = getParts;
            request.TimeSeriesUniqueId = uniqueId;
            request.QueryFrom = StartTime;
            request.QueryTo = EndTime;

            return Publish().Get(request);
        }

        public List<TimeAlignedPoint> GetTimeAlignedPoints(List<Guid> inputGuids, DateTimeOffset? StartTime, DateTimeOffset? EndTime)
        {
            Log.DebugFormat("GetTimeAlignedPoints number of guids='{0}', from start = {1} to end = {2}", inputGuids.Count,
                (StartTime.HasValue) ? StartTime.Value.ToString(_DateFormat) : "start of record",
                (EndTime.HasValue) ? EndTime.Value.ToString(_DateFormat) : "end of record");

            TimeAlignedDataServiceRequest request = new TimeAlignedDataServiceRequest();
            request.TimeSeriesUniqueIds = inputGuids;
            request.QueryFrom = StartTime;
            request.QueryTo = EndTime;
            request.IncludeGapMarkers = true;

            List<TimeAlignedPoint> points = Publish().Get(request).Points;

            if (points.Count == 0)
                Log.Debug("GetTimeAlignedPoints returns zero points");
            else
                Log.DebugFormat("GetTimeAlignedPoints returns = {0} points, from first timestamp = {1} to last timestamp = {2}",
                  points.Count, points[0].Timestamp, points[points.Count - 1].Timestamp);

            return points;
        }

        public TimeAlignedPoint[] GetInstMinMaxPoints(Guid inputGuid, string interval, bool extrema, DateTimeOffset StartTime, DateTimeOffset EndTime)
        {
            TimeSeriesInstMinMaxFinder instMinMax = new TimeSeriesInstMinMaxFinder(this);
            return instMinMax.GetInstMinMaxPoints(inputGuid, interval, extrema, StartTime, EndTime);
        }

        public string RoundDouble(double value, bool fix, int places, int? limitPlaces, string missingStr)
        {
            return FormatDoubleValue(value, fix, places, missingStr);
        }

        public static string FormatDoubleValue(double value, bool fix, int places, string missingStr)
        {
            return DoubleValueFormatter.FormatDoubleValue(value, fix, places, missingStr);
        }

        public static string FormatSigFigsNumber(double value, int sigfigs)
        {
            return DoubleValueFormatter.FormatSigFigsNumber(value, sigfigs);
        }

        public RatingModelDescription GetRatingModelDescription(string ratingModelIdentifier, string locationIdentifier)
        {
            RatingModelDescriptionListServiceRequest ratingModelDescriptionListRequest = new RatingModelDescriptionListServiceRequest();
            ratingModelDescriptionListRequest.LocationIdentifier = locationIdentifier;

            RatingModelDescriptionListServiceResponse ratingModelDescriptionListResponse = Publish().Get(ratingModelDescriptionListRequest);

            RatingModelDescription ratingModelDescription = null;
            foreach (RatingModelDescription rmDesc in ratingModelDescriptionListResponse.RatingModelDescriptions)
            {
                if (rmDesc.Identifier == ratingModelIdentifier)
                {
                    ratingModelDescription = rmDesc;
                    break;
                }
            }

            return ratingModelDescription;
        }

        public RatingCurveListServiceResponse GetRatingCurveList(string ratingModelIdentifier)
        {
            RatingCurveListServiceResponse ratingCurveResponse = null;

            RatingCurveListServiceRequest ratingCurveListRequest = new RatingCurveListServiceRequest();
            ratingCurveListRequest.RatingModelIdentifier = ratingModelIdentifier;

            try
            {
                ratingCurveResponse = Publish().Get(ratingCurveListRequest);
            }
            catch { }

            return ratingCurveResponse;
        }

        public string GetRatingCurveId(string ratingModelIdentifier, DateTimeOffset time)
        {
            string ratingCurveId = "";
            try
            {
                RatingCurveListServiceRequest curveRequest = new RatingCurveListServiceRequest();
                curveRequest.RatingModelIdentifier = ratingModelIdentifier;
                curveRequest.QueryFrom = time;
                curveRequest.QueryTo = time;

                RatingCurveListServiceResponse curveResponse = Publish().Get(curveRequest);
                if (curveResponse.RatingCurves.Count > 0)
                    ratingCurveId = curveResponse.RatingCurves[0].Id;
            }
            catch { }

            return ratingCurveId;
        }

        public string ReportInputInformation()
        {
            string newLine = Environment.NewLine;
            string dateFormat = "yyyy-MM-dd HH:mm:ss.ffffffzzz";
            RunFileReportRequest runReportRequest = _RunReportRequest;

            string outputFormatMessage = string.Format("{0}OutputFormat: {1}", System.Environment.NewLine, System.Environment.NewLine);
            outputFormatMessage += string.Format("OutputFormat = '{0}'{1}", _RunReportRequest.OutputFormat, System.Environment.NewLine);
            string localeMessage = string.Format("{0}Locale: {1}", System.Environment.NewLine, System.Environment.NewLine);
            localeMessage += string.Format("Locale = '{0}'{1}", _RunReportRequest.Locale, System.Environment.NewLine);

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

            message += outputFormatMessage;
            message += localeMessage;

            message += string.Format("{0}Report Settings: {1}", newLine, newLine);
            if (runReportRequest.Parameters != null)
                foreach (ReportJobParameter parameter in runReportRequest.Parameters)
                    message += string.Format("parameter: '{0}' = '{1}'{2}", parameter.Name, parameter.Value, newLine);

            message += string.Format("{0}Report Definition Metadata: {1}", newLine, newLine);
            Dictionary<string, string> metadatas = runReportRequest.ReportDefinitionMetadata;
            if ((metadatas != null) && (metadatas.Count > 0))
            {
                foreach (string key in metadatas.Keys)
                    message += string.Format("metadata: '{0}' = '{1}'{2}", key, metadatas[key], newLine);
            }

            return message;
        }

        public static string GetLocalizedEnumValue(string enumType, string enumValue)
        {
            try
            {
                string localizedEnum = Resources.ResourceManager.GetString(enumType + enumValue);
                if (string.IsNullOrEmpty(localizedEnum)) localizedEnum = Resources.ResourceManager.GetString(enumValue);
                if (string.IsNullOrEmpty(localizedEnum)) localizedEnum = enumValue;

                return localizedEnum;
            }
            catch
            {
                Log.DebugFormat("GetLocalizedEnumValue catch exception: Enum type = {0}, Enum value = {1}, returning Enum value = {1}", enumType, enumValue);
                return enumValue;
            }
        }
        public static string GetLocalizedDisplayString(string stringToLocalize)
        {
            try
            {
                string localized = Resources.ResourceManager.GetString(stringToLocalize);
                if (!string.IsNullOrEmpty(localized)) return localized;

                localized = Resources.ResourceManager.GetString(stringToLocalize.Replace(" ", string.Empty));
                if (!string.IsNullOrEmpty(localized)) return localized;
            }
            catch { }
            return stringToLocalize;
        }

        public static string GetLocalizedTimeSeriesTypeName(string timeSeriesType)
        {
            return GetLocalizedEnumValue("TimeSeriesType", timeSeriesType.Replace("Processor", ""));
        }

        public static string GetLocalizedRatingCurveTypeName(RatingCurveType ratingCurveType)
        {
            return GetLocalizedEnumValue("", ratingCurveType.ToString());
        }

        public static string GetLocalizedStatisticName(StatisticType statistic)
        {
            return GetLocalizedEnumValue("", statistic.ToString());
        }

        public static bool MatchPartialNameFilter(string filter, string stringValue)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (string.IsNullOrEmpty(stringValue)) stringValue = "";

            string matchThisString = "^" + filter.Trim().Replace("*", ".*") + "$";
            Regex regex = new Regex(matchThisString, RegexOptions.IgnoreCase);
            Match regMatch = regex.Match(stringValue);

            return regMatch.Success;
        }


        public string GetParameterRoundingSpec(string parameterName)
        {
            ParameterListServiceRequest request = new ParameterListServiceRequest();
            List<ParameterMetadata> parameterList = Publish().Get(request).Parameters;
            foreach (ParameterMetadata parameter in parameterList)
            {
                if (parameter.Identifier == parameterName)
                    return parameter.RoundingSpec;
            }
            Log.DebugFormat("GetParameterRoundingSpec did not find parameter = '{0}'", parameterName);
            return "";
        }

        public string GetFormattedDouble(double value, string roundingSpec, string missingStr = null)
        {
            if (string.IsNullOrEmpty(roundingSpec)) roundingSpec = "DEC(3)";

            var request = new RoundServiceSpecRequest
            {
                Data = new List<double> { value },
                RoundingSpec = roundingSpec,
                ValueForNaN = missingStr
            };

            return Publish().Put(request).Data[0];
        }

        public string GetFormattedDouble(double value, string parameterDisplayId, string unitId, string missingStr)
        {
            var request = new RoundServiceRequest
            {
                Data = new List<double> { value },
                ParameterDisplayId = parameterDisplayId,
                UnitId = unitId,
                ValueForNaN = missingStr
            };

            try
            {
                return Publish().Put(request).Data[0];
            }
            catch
            {
                return GetFormattedDouble(value, "", missingStr);
            }
        }

        public List<string> GetFormattedDoubles(List<double> values, string roundingSpec, string missingStr = null)
        {
            if (string.IsNullOrEmpty(roundingSpec)) roundingSpec = "DEC(3)";

            var request = new RoundServiceSpecRequest
            {
                Data = values,
                RoundingSpec = roundingSpec,
                ValueForNaN = missingStr
            };

            return Publish().Put(request).Data;
        }

        public List<string> GetFormattedDoubles(List<double> values, string parameterDisplayId, string unitId, string missingStr)
        {
            if (values.Count == 0) return new List<string>();

            var request = new RoundServiceRequest
            {
                Data = values,
                ParameterDisplayId = parameterDisplayId,
                UnitId = unitId,
                ValueForNaN = missingStr
            };

            try
            {
                return Publish().Put(request).Data;
            }
            catch (Exception exp)
            {
                string defaultRoundingSpec = "DEC(3)";

                Log.Error(string.Format(
                    "Error rounding doubles with parameter = '{0}' and unitId = '{1}', retry with default specification {2}",
                    parameterDisplayId, unitId, defaultRoundingSpec), exp);

                return GetFormattedDoubles(values, defaultRoundingSpec, missingStr);
            }
        }

        public List<string> GetComputedStatisticsFormatted(List<TimeSeriesPoint> points, string parameterDisplayId, string unitId, string missingStr, StatisticType statisticType)
        {
            List<double> statisticPoints = new List<double>();
            foreach (TimeSeriesPoint point in points)
                statisticPoints.Add((point.Value.HasValue) ? point.Value.Value : double.NaN);

            if (statisticType == StatisticType.Count)
                return GetFormattedDoubles(statisticPoints, "DEC(0)", missingStr);

            return GetFormattedDoubles(statisticPoints, parameterDisplayId, unitId, missingStr);
        }

        public double GetDefinedShift(string ratingModelIdentifier, double stage, DateTimeOffset measurementTime)
        {
            double definedShift = double.NaN;
            try
            {
                RatingModelEffectiveShiftsByStageValuesServiceRequest request = new RatingModelEffectiveShiftsByStageValuesServiceRequest();
                request.RatingModelIdentifier = ratingModelIdentifier;
                request.MeasurementTime = measurementTime;
                request.StageValues = new List<double> { stage };

                RatingModelEffectiveShiftsByStageValuesServiceResponse response = Publish().Get(request);

                if (response.EffectiveShiftValues.Count > 0 && response.EffectiveShiftValues[0].HasValue)
                    definedShift = response.EffectiveShiftValues[0].Value;

            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Exception calculating defined shift from '{0}', '{1}', '{2}': ",
                  ratingModelIdentifier, stage, measurementTime), exp);
            }
            return definedShift;
        }

        public double GetCalculatedStage(string ratingModelIdentifier, double discharge, DateTimeOffset measurementTime)
        {
            double calculatedStage = double.NaN;
            try
            {
                RatingModelInputValuesServiceRequest request = new RatingModelInputValuesServiceRequest();
                request.RatingModelIdentifier = ratingModelIdentifier;
                request.OutputValues = new List<double> { discharge };
                request.EffectiveTime = measurementTime;
                RatingModelInputValuesServiceResponse response = Publish().Get(request);
                List<double?> inputValues = response.InputValues;
                if ((inputValues.Count > 0) && (inputValues[0] != null))
                    calculatedStage = (double)inputValues[0];

            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Exception calculating stage from '{0}', '{1}', '{2}': ",
                  ratingModelIdentifier, discharge, measurementTime), exp);
            }
            return calculatedStage;
        }

        public double GetCalculatedDischarge(string ratingModelIdentifier, double stage, DateTimeOffset measurementTime, bool applyShifts)
        {
            double predictedDischarge = double.NaN;
            try
            {
                RatingModelOutputValuesServiceRequest request = new RatingModelOutputValuesServiceRequest();
                request.RatingModelIdentifier = ratingModelIdentifier;
                request.InputValues = new List<double> { stage };
                request.ApplyShifts = applyShifts;
                request.EffectiveTime = measurementTime;
                RatingModelOutputValuesServiceResponse response = Publish().Get(request);
                List<double?> outputValues = response.OutputValues;
                if ((outputValues.Count > 0) && (outputValues[0] != null))
                    predictedDischarge = (double)outputValues[0];
            }
            catch (Exception exp)
            {
                Log.Error(string.Format("Exception calculating predicted discharge from '{0}', '{1}', '{2}, applyShifts = '{3}'': ",
                  ratingModelIdentifier, stage, measurementTime, applyShifts), exp);
            }

            return predictedDischarge;
        }

        public List<FieldVisitDescription> GetFieldVisitDescriptions(string locIdentifier, DateTimeOffset? queryFrom, DateTimeOffset? queryTo)
        {
            FieldVisitDescriptionListServiceRequest request = new FieldVisitDescriptionListServiceRequest();
            request.LocationIdentifier = locIdentifier;
            request.QueryFrom = queryFrom;
            request.QueryTo = queryTo;
            request.IncludeInvalidFieldVisits = false;

            FieldVisitDescriptionListServiceResponse response = Publish().Get(request);

            return response.FieldVisitDescriptions;
        }

        public FieldVisitDataServiceResponse GetFieldVisitData(FieldVisitDescription fieldVisitDescription)
        {
            FieldVisitDataServiceRequest request = new FieldVisitDataServiceRequest();
            request.FieldVisitIdentifier = fieldVisitDescription.Identifier;
            request.ApplyRounding = true;
            request.IncludeInvalidActivities = false;

            FieldVisitDataServiceResponse response = Publish().Get(request);
            return response;
        }

        public List<FieldVisit> GetFieldVisitDataByLocation(string locationIdentifier, IEnumerable<ActivityType> activities = null, IEnumerable<string> parameters = null)
        {
            var request = new FieldVisitDataByLocationServiceRequest
            {
                LocationIdentifier = locationIdentifier,
                Activities = activities?.ToList() ?? new List<ActivityType> { ActivityType.Reading },
                Parameters = parameters?.ToList(),
            };

            return Publish().Get(request).FieldVisitData;
        }

        public List<Reading> GetFieldVisitReadingsByLocation(string locationIdentifier, IEnumerable<string> parameters = null)
        {
            return GetFieldVisitDataByLocation(locationIdentifier, new [] { ActivityType.Reading }, parameters)
                .Where(fv => fv.InspectionActivity?.Readings != null)
                .SelectMany(fv => fv.InspectionActivity.Readings)
                .ToList();
        }

        public List<string> GetFilterList(string filterString)
        {
            List<string> filterList = new List<string>();

            if (!string.IsNullOrEmpty(filterString))
            {
                string[] filterArray = filterString.Split(new string[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string filter in filterArray) filterList.Add(filter.Trim());
            }
            return filterList;
        }
        public List<LocationDescription> GetLocationDescriptions(string locationIdentifierFilter, string locationNameFilter,
            string primaryFolderFilter, List<string> locationTagKeysFilterList, List<string> locationTagValuesFilterList)
        {
            List<LocationDescription> locationDescriptions = new List<LocationDescription>();
            try
            {
                LocationDescriptionListServiceRequest locationDescriptionRequest = new LocationDescriptionListServiceRequest();

                if (!string.IsNullOrEmpty(locationIdentifierFilter)) locationDescriptionRequest.LocationIdentifier = locationIdentifierFilter;
                if (!string.IsNullOrEmpty(locationNameFilter)) locationDescriptionRequest.LocationName = locationNameFilter;
                if (!string.IsNullOrEmpty(primaryFolderFilter)) locationDescriptionRequest.LocationFolder = primaryFolderFilter;
                if (locationTagKeysFilterList.Count > 0) locationDescriptionRequest.TagKeys = locationTagKeysFilterList;
                if (locationTagValuesFilterList.Count > 0) locationDescriptionRequest.TagValues = locationTagValuesFilterList;

                locationDescriptions = Publish().Get(locationDescriptionRequest).LocationDescriptions;
            }
            catch { }

            return locationDescriptions;
        }

        public List<LocationDescription> GetLocationDescriptions(string locationIdentifierFilter, string locationNameFilter,
            string primaryFolderFilter, List<string> locationTagFilterList)
        {
            List<LocationDescription> locationDescriptions = new List<LocationDescription>();
            try
            {
                List<TagDefinition> tags = GetTagDefinitions("Location");

                List<string> keysFilter = new List<string>();
                List<string> valuesFilter = new List<string>();
                List<string> pairsFilter = new List<string>();

                bool unknownTagFilterItem = false;
                foreach (string tagFilter in locationTagFilterList)
                {
                    bool matchesKey = MatchesATagKey(tagFilter, tags);
                    if (matchesKey) keysFilter.Add(tagFilter);

                    bool matchesValue = MatchesATagValue(tagFilter, tags);
                    if (matchesValue) valuesFilter.Add(tagFilter);

                    if (!matchesKey && !matchesValue)
                    {
                        string pairString = MatchesATagPair(tagFilter, tags);
                        if (!string.IsNullOrEmpty(pairString))
                        {
                            pairsFilter.Add(pairString);

                            string[] keyValuePair = pairString.Split(new string[] { "+" }, StringSplitOptions.None);
                            keysFilter.Add(keyValuePair[0]);
                            valuesFilter.Add(keyValuePair[1]);
                        }
                        else
                        {
                            Log.InfoFormat("Location Tag Filter unknown item: '{0}'", tagFilter);
                            unknownTagFilterItem = true;
                        };
                    }
                }

                LogTagFilters("Location", locationTagFilterList, keysFilter, valuesFilter, pairsFilter);

                if (!unknownTagFilterItem)
                {
                    LocationDescriptionListServiceRequest locationDescriptionRequest = new LocationDescriptionListServiceRequest();

                    if (!string.IsNullOrEmpty(locationIdentifierFilter)) locationDescriptionRequest.LocationIdentifier = locationIdentifierFilter;
                    if (!string.IsNullOrEmpty(locationNameFilter)) locationDescriptionRequest.LocationName = locationNameFilter;
                    if (!string.IsNullOrEmpty(primaryFolderFilter)) locationDescriptionRequest.LocationFolder = primaryFolderFilter;
                    if (keysFilter.Count > 0) locationDescriptionRequest.TagKeys = keysFilter;
                    if (valuesFilter.Count > 0) locationDescriptionRequest.TagValues = valuesFilter;

                    LocationDescriptionListServiceResponse locResponse = Publish().Get(locationDescriptionRequest);

                    if (pairsFilter.Count == 0)
                    {
                        locationDescriptions = locResponse.LocationDescriptions;
                    }
                    else
                    {
                        int pairsCount = pairsFilter.Count;
                        foreach (LocationDescription locDesc in locResponse.LocationDescriptions)
                        {
                            int matchCount = 0;
                            foreach (string pairFilter in pairsFilter)
                            {
                                string[] keyValuePairs = pairFilter.Split(new string[] { "+" }, StringSplitOptions.None);
                                foreach (TagMetadata tag in locDesc.Tags)
                                {
                                    if (!string.IsNullOrEmpty(tag.Value) &&
                                        MatchPartialNameFilter(keyValuePairs[0], tag.Key) &&
                                        MatchPartialNameFilter(keyValuePairs[1], tag.Value))
                                        matchCount++;
                                }
                            }
                            if (matchCount == pairsCount) locationDescriptions.Add(locDesc);
                        }
                    }
                }
            }
            catch { }

            return locationDescriptions;
        }

        public bool MatchesATagKey(string tagFilter, List<TagDefinition> Tags)
        {
            string matchThisString = "^" + tagFilter.Replace("*", ".*") + "$";
            Regex regex = new Regex(matchThisString, RegexOptions.IgnoreCase);
            foreach (TagDefinition tag in Tags)
            {
                if (regex.Match(tag.Key).Success)
                    return true;
            }         
            return false;
        }

        public bool MatchesATagValue(string tagFilter, List<TagDefinition> Tags)
        {
            string matchThisString = "^" + tagFilter.Replace("*", ".*") + "$";
            Regex regex = new Regex(matchThisString, RegexOptions.IgnoreCase);
            foreach (TagDefinition tag in Tags)
            {
                if (!tag.ValueType.HasValue) continue;
                if (tag.ValueType.Value != TagValueType.PickList) continue;

                foreach (string pickListValue in tag.PickListValues)
                    if (regex.Match(pickListValue).Success)
                        return true;
            }
            return false;
        }

        public string MatchesATagPair(string tagFilter, List<TagDefinition> Tags)
        {
            if (!tagFilter.Contains(":")) return null;

            string[] splitItems = tagFilter.Split(new string[] { ":" }, StringSplitOptions.None);

            for (int i = 0; i < splitItems.Length - 1; i++)
            {
                string leftSubstring = splitItems[0];
                for (int j = 1; j < i + 1; j++) leftSubstring += ":" + splitItems[j];
                string rightSubstring = splitItems[i + 1];
                for (int j = i + 2; j < splitItems.Length; j++) rightSubstring += ":" + splitItems[j];

                leftSubstring = leftSubstring.Trim();
                rightSubstring = rightSubstring.Trim();

                string leftSubstringRegEx = "^" + leftSubstring.Replace("*", ".*") + "$";
                Regex leftRegex = new Regex(leftSubstringRegEx, RegexOptions.IgnoreCase);
                string rightSubstringRegEx = "^" + rightSubstring.Replace("*", ".*") + "$";
                Regex rightRegex = new Regex(rightSubstringRegEx, RegexOptions.IgnoreCase);

                foreach (TagDefinition tag in Tags)
                {
                    if (!tag.ValueType.HasValue) continue;
                    if (tag.ValueType.Value != TagValueType.PickList) continue;

                    if (!leftRegex.Match(tag.Key).Success) continue;

                    foreach (string pickListValue in tag.PickListValues)
                        if (rightRegex.Match(pickListValue).Success)
                            return string.Format("{0}+{1}", leftSubstring, rightSubstring);
                }
            }
            return null;
        }

        public List<string> GetUnknownTagFilters(List<string> tagsFilterList, string tagType)
        {
            List<string> unknownFilters = new List<string>();
            List<TagDefinition> tags = GetTagDefinitions(tagType);

            foreach (string tagFilter in tagsFilterList)
            {
                bool matchesKey = MatchesATagKey(tagFilter, tags);
                bool matchesValue = MatchesATagValue(tagFilter, tags);

                if (!matchesKey && !matchesValue && string.IsNullOrEmpty(MatchesATagPair(tagFilter, tags)))
                    unknownFilters.Add(tagFilter);
            }
            return unknownFilters;
        }

        public string GetUnknownTagFilterMessage(List<string> tagFilterList, string tagType)
        {
            List<string> unknownTags = GetUnknownTagFilters(tagFilterList, tagType);
            if (unknownTags.Count > 0)
            {
                return string.Format("{0} '{1}'",
                    ((unknownTags.Count > 1) ?
                    Common.GetLocalizedDisplayString("Unknown " + tagType + " Tag Filter Items") :
                    Common.GetLocalizedDisplayString("Unknown " + tagType + " Tag Filter Item")),
                    String.Join(", ", unknownTags));
            }
            return "";
        }

        public List<TagDefinition> GetTagDefinitions()
        {
            return Publish().Get(new TagListServiceRequest()).Tags;
        }

        public List<TagDefinition> GetTagDefinitions(string tagType)
        {
            List<TagDefinition> allTagDefinitions = GetTagDefinitions();
            List<TagDefinition> filteredTagDefinitions = new List<TagDefinition>();
            foreach (TagDefinition tag in allTagDefinitions)
            {
                if ((tagType == "Location") && !tag.AppliesToLocations) continue;
                if ((tagType == "LocationNote") && !tag.AppliesToLocationNotes) continue;
                if ((tagType == "SensorsGauges") && !tag.AppliesToSensorsGauges) continue;

                filteredTagDefinitions.Add(tag);
            }
            return filteredTagDefinitions;
        }
        public static bool MaximumTimeExceeded(DateTimeOffset reportStartTime, double maximumTimeInMinutes, string outputFormat)
        {
            double timeForReportGeneration = 0.5;

            if (maximumTimeInMinutes < 0.0) return false;

            TimeSpan elapsedTime = DateTimeOffset.Now - reportStartTime;
            if (elapsedTime.TotalMinutes + timeForReportGeneration > maximumTimeInMinutes)
                return true;

            return false;
        }
        public static string GetTableTruncatedInformationString(double timeLimitInMinutes)
        {
            return string.Format(Resources.TheTableHasBeenTruncatedX, timeLimitInMinutes);
        }

        public static void LogTagFilters(string tagType, List<string> tagFilterList, 
            List<string> keysFilter, List<string> valuesFilter, List<string> pairsFilter)
        {
            Log.DebugFormat("{0} Tag Filter List: '{1}'", tagType, String.Join(", ", tagFilterList));
            Log.DebugFormat("{0} Tag Filter Keys: '{1}'", tagType, String.Join(", ", keysFilter));
            Log.DebugFormat("{0} Tag Filter Values: '{1}'", tagType, String.Join(", ", valuesFilter));

            foreach (string pairFilter in pairsFilter)
            {
                string[] pair = pairFilter.Split(new string[] { "+" }, StringSplitOptions.None);
                Log.DebugFormat("{0} Tag Filter Pair: key='{1}' and value='{2}'", tagType, pair[0], pair[1]);
            }
        }
    }
}





// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\DataTablesBuilder.cs"
namespace TimeSeriesPlotNamespace
{
    public class DataTablesBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RunFileReportRequest _RunReportRequest;
        private Common _Common;
        public static string DateFormat = "yyyy-MM-dd HH:mmzzz";

        public string _DllName;
        public string _DllFolder;

        public List<string> CommonReportParameters = new List<string> { "ReportTitle", "Description", "Comment" };

        public DataTablesBuilder(RunFileReportRequest request, Common common)
        {
            _RunReportRequest = request;
            _Common = common;
        }

        public DataSet GetCommonDataSet(string dllName, string dllFolder)
        {
            _DllName = dllName;
            _DllFolder = dllFolder;

            DataSet set1 = new DataSet("ReportTables");

            AddRunReportRequestTable(set1, "RunReportRequest");
            AddReportSettingsTable(set1, "ReportSettings");
            AddReportDataTable(set1, "ReportData");
            AddOrganizationIconTable(set1, "OrganizationIcon");
            AddInputsTables(set1);
            AddReportPeriodsTable(set1, "ReportPeriods");

            Log.Debug("Returning CommonDataSet");
            return set1;
        }

        public void AddRunReportRequestTable(DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            table.Columns.Add("RunReportRequest", typeof(object));
            table.Columns.Add("DllName", typeof(string));
            table.Columns.Add("DllFolder", typeof(string));
            table.Columns.Add("CommonLibrary", typeof(object));
            table.Columns.Add("Publish", typeof(object));
            table.Columns.Add("OutputFormat", typeof(string));
            table.Columns.Add("ReportInputInformation", typeof(string));

            dataSet.Tables.Add(table);

            DataRow row = table.NewRow();
            row["RunReportRequest"] = _RunReportRequest;
            row["DllName"] = _DllName;
            row["DllFolder"] = _DllFolder;
            row["CommonLibrary"] = _Common;
            row["Publish"] = _RunReportRequest.Publish;
            row["OutputFormat"] = _RunReportRequest.OutputFormat;
            row["ReportInputInformation"] = _Common.ReportInputInformation();
            table.Rows.Add(row);
        }

        public void AddInputsTables(DataSet dataSet)
        {
            AddReportTimeSeriesInputsTable(dataSet, "TimeSeriesInputs", _RunReportRequest.Inputs);
            AddReportLocationInputTable(dataSet, "LocationInput", _RunReportRequest.Inputs);

            ReportRequestInputs inputs = _RunReportRequest.Inputs;
            if (inputs == null) return;

            if (inputs.TimeSeriesInputs != null)
            {
                foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
                {
                    AddTimeSeriesInputDataTable(dataSet, timeseriesInput);
                    AddTimeSeriesLocationDataTable(dataSet, timeseriesInput);
                }
            }
            if (inputs.LocationInput != null)
            {
                AddLocationInputDataTable(dataSet, inputs.LocationInput);
            }
        }

        public void AddReportSettingsTable(System.Data.DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            dataSet.Tables.Add(ReportSettingsTable(tableName));
        }

        public void AddReportDataTable(System.Data.DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            dataSet.Tables.Add(ReportDataTable(tableName));
        }

        public void AddOrganizationIconTable(System.Data.DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            dataSet.Tables.Add(OrganizationIconTable(tableName));
        }

        public void AddReportPeriodsTable(System.Data.DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            dataSet.Tables.Add(ReportPeriodsTable(tableName));
        }

        public void AddReportTimeSeriesInputsTable(System.Data.DataSet dataSet, string tableName, ReportRequestInputs inputs)
        {
            Log.DebugFormat("AddReportTimeSeriesInputsTable {0}", tableName);
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            dataSet.Tables.Add(table);
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Guid", typeof(Guid));
            table.Columns.Add("Label", typeof(string));
            table.Columns.Add("IsMaster", typeof(bool));

            if (inputs == null) return;

            foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
            {
                DataRow row = table.NewRow();
                row["Name"] = timeseriesInput.Name;
                row["Guid"] = timeseriesInput.UniqueId;
                row["Label"] = timeseriesInput.Label;
                row["IsMaster"] = timeseriesInput.IsMaster;
                table.Rows.Add(row);
            }
        }
        public void AddReportLocationInputTable(System.Data.DataSet dataSet, string tableName, ReportRequestInputs inputs)
        {
            Log.DebugFormat("AddReportLocationInputTable {0}", tableName);
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            dataSet.Tables.Add(table);
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Identifier", typeof(string));

            if ((inputs == null) || (inputs.LocationInput == null)) return;

            System.Data.DataRow row = table.NewRow();
            LocationReportRequestInput locationInput = inputs.LocationInput;
            row["Name"] = locationInput.Name;
            row["Identifier"] = locationInput.Identifier;
            table.Rows.Add(row);
        }
        public void AddTimeSeriesLocationDataTable(System.Data.DataSet dataSet, TimeSeriesReportRequestInput timeseriesInput)
        {
            string tableName = string.Format("{0}Location", timeseriesInput.Name);
            if (dataSet.Tables.Contains(tableName)) return;

            string locationIdentifier = _Common.GetTimeSeriesDescription(timeseriesInput.UniqueId).LocationIdentifier;
            AddLocationDataTables(dataSet, tableName, locationIdentifier);
        }

        public void AddLocationInputDataTable(DataSet dataSet, LocationReportRequestInput locationInput)
        {
            string tableName = "InputLocation";
            if (dataSet.Tables.Contains(tableName)) return;

            if (locationInput == null) return;
            AddLocationDataTables(dataSet, tableName, locationInput.Identifier);
        }

        public void AddLocationDataTables(DataSet dataSet, string tableName, string locationIdentifier)
        {
            dataSet.Tables.Add(LocationDataTable(tableName, locationIdentifier));
            dataSet.Tables.Add(LocationExtendedAttributesTable(tableName + "ExtendedAttributes", locationIdentifier));
        }

        public void AddTimeSeriesInputDataTable(System.Data.DataSet dataSet, TimeSeriesReportRequestInput timeseriesInput)
        {
            string tableName = timeseriesInput.Name;
            if (dataSet.Tables.Contains(tableName)) return;

            Guid timeseriesUniqueId = timeseriesInput.UniqueId;
            dataSet.Tables.Add(TimeSeriesDataTable(tableName, timeseriesUniqueId));
        }

        public DataTable ReportDataTable(string tableName)
        {
            Log.DebugFormat("Create ReportDataTable {0}", tableName);
            DataTable table = new DataTable(tableName);

            table.Columns.Add("SelectedInterval", typeof(DateTimeOffsetInterval));
            table.Columns.Add("PeriodSelectedAdjustedForReport", typeof(DateTimeOffsetInterval));
            table.Columns.Add("PeriodSelectedString", typeof(string));
            table.Columns.Add("PeriodSelectedInformation", typeof(string));
            table.Columns.Add("IReportData", typeof(IReportData));
            table.Columns.Add("Locale", typeof(string));
            table.Columns.Add("PageHeader1", typeof(string));
            table.Columns.Add("PageHeader2", typeof(string));
            table.Columns.Add("FooterDisclaimer", typeof(string));
            table.Columns.Add("ReportTitle", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("Comment", typeof(string));
            table.Columns.Add("ReportSubTitle", typeof(string));
            table.Columns.Add("WaterYearDefaultMonth", typeof(int));
            table.Columns.Add("OutputFormat", typeof(string));

            DataRow row = table.NewRow();

            row["SelectedInterval"] = _RunReportRequest.Interval;
            row["PeriodSelectedAdjustedForReport"] = _Common.GetPeriodSelectedAdjustedForReport();
            row["PeriodSelectedString"] = _Common.PeriodSelectedString(_Common.GetPeriodSelectedAdjustedForReport());
            row["PeriodSelectedInformation"] = _Common.GetPeriodSelectedInformation(_Common.GetPeriodSelectedAdjustedForReport());
            row["IReportData"] = _RunReportRequest.ReportData;
            row["Locale"] = _RunReportRequest.Locale;
            row["PageHeader1"] = GetPageHeader1();
            row["PageHeader2"] = GetPageHeader2();
            row["FooterDisclaimer"] = GetFooterDisclaimer();
            row["ReportTitle"] = _Common.GetParameterString("ReportTitle", "");
            row["Description"] = _Common.GetParameterString("Description", "");
            row["Comment"] = _Common.GetParameterString("Comment", "");
            row["ReportSubTitle"] = GetReportSubTitle();
            row["WaterYearDefaultMonth"] = _Common.GetWaterYearMonth();
            row["OutputFormat"] = _RunReportRequest.OutputFormat;

            table.Rows.Add(row);

            return table;
        }

        public DataTable OrganizationIconTable(string tableName)
        {
            Log.DebugFormat("Create OrganizationIconTable {0}", tableName);
            DataTable table = new DataTable(tableName);

            table.Columns.Add("EncodedImage", typeof(string));
            table.Columns.Add("SupportUrl", typeof(string));

            DataRow row = table.NewRow();

            row["EncodedImage"] = GetOrganizationIconEncodedImage();
            row["SupportUrl"] = GetOrganizationSupportUrl();

            table.Rows.Add(row);

            return table;
        }

        public DataTable ReportSettingsTable(string tableName)
        {
            Log.DebugFormat("Create ReportSettingsTable {0}", tableName);
            DataTable table = new DataTable(tableName);

            foreach (ReportJobParameter reportParameter in _RunReportRequest.Parameters)
                try
                {
                    if (!CommonReportParameters.Contains(reportParameter.Name))
                        table.Columns.Add(reportParameter.Name, typeof(string));
                }
                catch { }

            DataRow row = table.NewRow();

            foreach (ReportJobParameter reportParameter in _RunReportRequest.Parameters)
                try
                {
                    if (!CommonReportParameters.Contains(reportParameter.Name))
                        row[reportParameter.Name] = reportParameter.Value;
                }
                catch { }

            table.Rows.Add(row);

            return table;
        }

        public DataTable TimeSeriesDataTable(string tableName, Guid timeseriesUniqueId)
        {
            Log.DebugFormat("Create TimeSeriesDataTable {0}, {1}", tableName, timeseriesUniqueId);
            DataTable timeSeriesTable = new DataTable(tableName);
            timeSeriesTable.Columns.Add("UniqueId", typeof(Guid));
            timeSeriesTable.Columns.Add("TimeSeriesIdentifier", typeof(string));
            timeSeriesTable.Columns.Add("Parameter", typeof(string));
            timeSeriesTable.Columns.Add("Label", typeof(string));
            timeSeriesTable.Columns.Add("Description", typeof(string));
            timeSeriesTable.Columns.Add("Comment", typeof(string));
            timeSeriesTable.Columns.Add("LocationIdentifier", typeof(string));
            timeSeriesTable.Columns.Add("SubLocationIdentifier", typeof(string));
            timeSeriesTable.Columns.Add("Computation", typeof(string));
            timeSeriesTable.Columns.Add("ComputationPeriod", typeof(string));
            timeSeriesTable.Columns.Add("TimeSeriesType", typeof(string));
            timeSeriesTable.Columns.Add("InterpolationType", typeof(InterpolationType));
            timeSeriesTable.Columns.Add("InterpolationTypeString", typeof(string));
            timeSeriesTable.Columns.Add("LastModified", typeof(DateTimeOffset));
            timeSeriesTable.Columns.Add("RawStartTime", typeof(DateTimeOffset));
            timeSeriesTable.Columns.Add("RawEndTime", typeof(DateTimeOffset));
            timeSeriesTable.Columns.Add("Publish", typeof(bool));
            timeSeriesTable.Columns.Add("Unit", typeof(string));
            timeSeriesTable.Columns.Add("UnitSymbol", typeof(string));
            timeSeriesTable.Columns.Add("UnitInformation", typeof(string));
            timeSeriesTable.Columns.Add("UtcOffset", typeof(TimeSpan));
            timeSeriesTable.Columns.Add("UtcOffsetString", typeof(string));
            timeSeriesTable.Columns.Add("TimeSeriesInformation", typeof(string));
            timeSeriesTable.Columns.Add("TimeSeriesInterval", typeof(DateTimeOffsetInterval));
            timeSeriesTable.Columns.Add("TimeSeriesTimeRangeString", typeof(string));
            timeSeriesTable.Columns.Add("TimeSeriesTimeRangeInformation", typeof(string));
            timeSeriesTable.Columns.Add("RoundingSpec", typeof(string));
            DataRow dataRow = timeSeriesTable.NewRow();

            TimeSeriesDescription tsd = _Common.GetTimeSeriesDescription(timeseriesUniqueId);

            dataRow["UniqueId"] = tsd.UniqueId;
            dataRow["TimeSeriesIdentifier"] = tsd.Identifier;
            dataRow["Parameter"] = tsd.Parameter;
            dataRow["Label"] = tsd.Label;
            dataRow["Description"] = tsd.Description;
            dataRow["Comment"] = tsd.Comment;
            dataRow["LocationIdentifier"] = tsd.LocationIdentifier;
            dataRow["SubLocationIdentifier"] = tsd.SubLocationIdentifier;
            dataRow["Computation"] = tsd.ComputationIdentifier;
            dataRow["ComputationPeriod"] = tsd.ComputationPeriodIdentifier;
            dataRow["TimeSeriesType"] = tsd.TimeSeriesType;
            dataRow["InterpolationType"] = _Common.GetTimeSeriesInterpolationType(timeseriesUniqueId);
            dataRow["InterpolationTypeString"] = GetLegacyInterpolationTypeString(_Common.GetTimeSeriesInterpolationType(timeseriesUniqueId));
            dataRow["LastModified"] = tsd.LastModified;
            if (tsd.RawStartTime.HasValue) dataRow["RawStartTime"] = tsd.RawStartTime.Value;
            if (tsd.RawEndTime.HasValue) dataRow["RawEndTime"] = tsd.RawEndTime.Value;
            dataRow["Publish"] = tsd.Publish;
            dataRow["Unit"] = tsd.Unit;
            dataRow["UnitSymbol"] = _Common.GetTimeSeriesUnitSymbol(timeseriesUniqueId);
            dataRow["UnitInformation"] = _Common.GetTimeSeriesUnitInformation(timeseriesUniqueId);
            dataRow["UtcOffset"] = TimeSpan.FromHours(tsd.UtcOffset);
            dataRow["UtcOffsetString"] = _Common.GetOffsetString(tsd.UtcOffset);
            dataRow["TimeSeriesInformation"] = _Common.GetTimeSeriesInformation(timeseriesUniqueId);
            dataRow["TimeSeriesInterval"] = _Common.GetTimeSeriesTimeRange(timeseriesUniqueId);
            dataRow["TimeSeriesTimeRangeString"] = _Common.GetTimeSeriesTimeRangeString(timeseriesUniqueId);
            dataRow["TimeSeriesTimeRangeInformation"] = _Common.GetTimeSeriesTimeRangeInformation(timeseriesUniqueId);
            dataRow["RoundingSpec"] = _Common.GetParameterRoundingSpec(tsd.Parameter);

            timeSeriesTable.Rows.Add(dataRow);

            return timeSeriesTable;
        }

        private static string GetLegacyInterpolationTypeString(InterpolationType interpolationType)
        {
            // This avoids adding a reference to AopDB.dll, just to get a string like "7 - Discrete Values"
            // (new AquariusDataService.PortsConverter()).GetInterpolationCodeName((int)tsd.InterpolationType);
            return $"{(int)interpolationType} - {interpolationType.ToString().ToTitleCase().SplitCamelCase()}";
        }

        public DataTable LocationDataTable(string tableName, string locationIdentifier)
        {
            Log.DebugFormat("Create LocationDataTable {0}, {1}", tableName, locationIdentifier);
            DataTable locationTable = new DataTable(tableName);
            locationTable.Columns.Add("UniqueId", typeof(Guid));
            locationTable.Columns.Add("LocationIdentifier", typeof(string));
            locationTable.Columns.Add("LocationName", typeof(string));
            locationTable.Columns.Add("UtcOffset", typeof(TimeSpan));
            locationTable.Columns.Add("UtcOffsetString", typeof(string));
            locationTable.Columns.Add("Description", typeof(string));
            locationTable.Columns.Add("Latitude", typeof(double));
            locationTable.Columns.Add("Longitude", typeof(double));
            locationTable.Columns.Add("Elevation", typeof(double));
            locationTable.Columns.Add("ElevationUnit", typeof(string));
            locationTable.Columns.Add("ElevationUnitSymbol", typeof(string));
            locationTable.Columns.Add("LocationType", typeof(string));
            locationTable.Columns.Add("IsExternal", typeof(bool));
            locationTable.Columns.Add("Tags", typeof(object));
            locationTable.Columns.Add("LocationIdentifierAndNameInformation", typeof(string));
            locationTable.Columns.Add("LocationExtraInformation", typeof(string));
            DataRow dataRow = locationTable.NewRow();

            LocationDescription locDescription = _Common.GetLocationDescriptionByIdentifier(locationIdentifier);
            LocationDataServiceResponse locData = _Common.GetLocationData(locationIdentifier);

            dataRow["UniqueId"] = locDescription.UniqueId;
            dataRow["LocationIdentifier"] = locationIdentifier;
            dataRow["LocationName"] = locDescription.Name;
            dataRow["UtcOffset"] = TimeSpan.FromHours(locData.UtcOffset);
            dataRow["UtcOffsetString"] = _Common.GetOffsetString(locData.UtcOffset);
            dataRow["Description"] = locData.Description;
            dataRow["Latitude"] = locData.Latitude;
            dataRow["Longitude"] = locData.Longitude;
            dataRow["Elevation"] = locData.Elevation;
            dataRow["ElevationUnit"] = locData.ElevationUnits;
            dataRow["ElevationUnitSymbol"] = _Common.GetUnitSymbol(locData.ElevationUnits);
            dataRow["LocationType"] = locData.LocationType;
            dataRow["IsExternal"] = locDescription.IsExternalLocation;
            dataRow["Tags"] = locDescription.Tags;
            dataRow["LocationIdentifierAndNameInformation"] = Resources.LocationIdentifier + ": " + locationIdentifier +
                ", " + Resources.LocationName + ": " + locDescription.Name;
            dataRow["LocationExtraInformation"] = Resources.UtcOffset + ": " + _Common.GetOffsetString(locData.UtcOffset) +
                ", " + Resources.Latitude + ": " + locData.Latitude +
                ", " + Resources.Longitude + ": " + locData.Longitude +
                ", " + Resources.Elevation + ": " + locData.Elevation.ToString() +
                ((string.IsNullOrEmpty(locData.Elevation.ToString()) ? "" : " " + locData.ElevationUnits));

            locationTable.Rows.Add(dataRow);

            return locationTable;
        }

        public DataTable LocationExtendedAttributesTable(string tableName, string locationIdentifier)
        {
            Log.DebugFormat("Create LocationExtendedAttributesTable {0}, {1}", tableName, locationIdentifier);
            DataTable locationExtendedAttributesTable = new DataTable(tableName);

            LocationDataServiceResponse locData = _Common.GetLocationData(locationIdentifier);
            List<ExtendedAttribute> attributes = locData.ExtendedAttributes;

            foreach (ExtendedAttribute attribute in attributes)
            {
                try
                {
                    locationExtendedAttributesTable.Columns.Add(attribute.Name, typeof(object));
                }
                catch (Exception exp)
                {
                    Log.Error(string.Format("Error creating column in table = {0} with name = {1}", tableName, attribute.Name), exp);
                }
            }

            DataRow dataRow = locationExtendedAttributesTable.NewRow();

            foreach (ExtendedAttribute attribute in attributes)
            {
                if (locationExtendedAttributesTable.Columns.Contains(attribute.Name))
                    dataRow[attribute.Name] = attribute.Value;
            }

            locationExtendedAttributesTable.Rows.Add(dataRow);

            return locationExtendedAttributesTable;
        }

        public DataTable ReportPeriodsTable(string tableName)
        {
            return ReportPeriodsTable(tableName, _Common.GetReportTimeSpanOffset());
        }

        public DataTable ReportPeriodsTable(string tableName, TimeSpan reportUtcOffset)
        {
            Log.DebugFormat("Create ReportPeriodsTable {0}", tableName);

            DateTimeOffsetInterval timeRangeToAdjust = _Common.GetPeriodSelectedAdjustedForReport();
            GroupByHandler groupByHandler = new GroupByHandler(_Common);
            if ((_RunReportRequest.Inputs != null) && (_RunReportRequest.Inputs.TimeSeriesInputs.Count > 0))
            {
                Guid firstTimeSeriesUniqueId = _RunReportRequest.Inputs.TimeSeriesInputs[0].UniqueId;
                timeRangeToAdjust = groupByHandler.GetIntervalOfOverlap(timeRangeToAdjust, _Common.GetTimeSeriesTimeRange(firstTimeSeriesUniqueId), reportUtcOffset);
                for (int i = 1; i < _RunReportRequest.Inputs.TimeSeriesInputs.Count; i++)
                {
                    Guid timeSeriesUniqueId = _RunReportRequest.Inputs.TimeSeriesInputs[i].UniqueId;
                    timeRangeToAdjust = groupByHandler.GetIntervalOfOverlap(timeRangeToAdjust, _Common.GetTimeSeriesTimeRange(timeSeriesUniqueId), reportUtcOffset);
                }
            }

            DataTable table = new DataTable(tableName);
            table.Columns.Add("GroupByYear", typeof(DateTimeOffsetInterval));
            table.Columns.Add("GroupByWaterYear", typeof(DateTimeOffsetInterval));
            table.Columns.Add("GroupByMonth", typeof(DateTimeOffsetInterval));
            table.Columns.Add("GroupByWeek", typeof(DateTimeOffsetInterval));
            table.Columns.Add("GroupByDay", typeof(DateTimeOffsetInterval));
            table.Columns.Add("NoGroupBy", typeof(DateTimeOffsetInterval));

            DataRow row = table.NewRow();
            row["GroupByYear"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "Year");
            row["GroupByWaterYear"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "WaterYear");
            row["GroupByMonth"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "Month");
            row["GroupByWeek"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "Week");
            row["GroupByDay"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "Day");
            row["NoGroupBy"] = groupByHandler.AdjustIntervalToGroupBy(timeRangeToAdjust, "NoGroupBy");

            table.Rows.Add(row);

            return table;
        }

        public DataTable RatingModelTable(string tableName, string inputOutputParameters, string ratingModelLabel, string locationIdentifier)
        {
            DataTable ratingModelTable = new DataTable(tableName);

            ratingModelTable.Columns.Add("Identifier", typeof(string));
            ratingModelTable.Columns.Add("InputParameter", typeof(string));
            ratingModelTable.Columns.Add("InputUnitId", typeof(string));
            ratingModelTable.Columns.Add("InputUnitSymbol", typeof(string));
            ratingModelTable.Columns.Add("InputParameterRoundingSpec", typeof(string));
            ratingModelTable.Columns.Add("OutputParameter", typeof(string));
            ratingModelTable.Columns.Add("OutputUnitId", typeof(string));
            ratingModelTable.Columns.Add("OutputUnitSymbol", typeof(string));
            ratingModelTable.Columns.Add("OutputParameterRoundingSpec", typeof(string));
            ratingModelTable.Columns.Add("Description", typeof(string));
            ratingModelTable.Columns.Add("Comment", typeof(string));
            ratingModelTable.Columns.Add("TimeRange", typeof(string));
            ratingModelTable.Columns.Add("RatingModelInfo", typeof(string));

            ratingModelTable.Columns.Add("RatingModelDescriptionObject", typeof(object));

            string ratingModelIdentifier = string.Format("{0}.{1}@{2}", inputOutputParameters, ratingModelLabel, locationIdentifier);

            DataRow ratingModelRow = ratingModelTable.NewRow();
            ratingModelRow["Identifier"] = ratingModelIdentifier;
            ratingModelTable.Rows.Add(ratingModelRow);

            RatingModelDescription ratingModelDescription = _Common.GetRatingModelDescription(ratingModelIdentifier, locationIdentifier);
            ratingModelRow["RatingModelDescriptionObject"] = ratingModelDescription;

            string locationName = _Common.GetLocationDescriptionByIdentifier(locationIdentifier).Name;

            if (ratingModelDescription == null)
            {
                if (string.IsNullOrEmpty(ratingModelLabel) && string.IsNullOrEmpty(inputOutputParameters))
                    ratingModelRow["RatingModelInfo"] = Resources.RatingModelUnspecified;
                else
                    ratingModelRow["RatingModelInfo"] = string.Format(Resources.NoRatingModelFoundWithXYZZ,
                      inputOutputParameters, ratingModelLabel, locationIdentifier, locationName);
            }
            else
            {

                string inputParameterDisplayId = ratingModelDescription.InputParameter;
                string inputUnitId = ratingModelDescription.InputUnit;
                string outputParameterDisplayId = ratingModelDescription.OutputParameter;
                string outputUnitId = ratingModelDescription.OutputUnit;
                string rmDescription = ratingModelDescription.Description;

                ratingModelRow["InputParameter"] = inputParameterDisplayId;
                ratingModelRow["InputUnitId"] = inputUnitId;
                ratingModelRow["InputUnitSymbol"] = _Common.GetUnitSymbol(inputUnitId);
                ratingModelRow["InputParameterRoundingSpec"] = _Common.GetParameterRoundingSpec(inputParameterDisplayId);

                ratingModelRow["OutputParameter"] = outputParameterDisplayId;
                ratingModelRow["OutputUnitId"] = outputUnitId;
                ratingModelRow["OutputUnitSymbol"] = _Common.GetUnitSymbol(outputUnitId);
                ratingModelRow["OutputParameterRoundingSpec"] = _Common.GetParameterRoundingSpec(outputParameterDisplayId);

                ratingModelRow["Description"] = rmDescription;
                ratingModelRow["Comment"] = ratingModelDescription.Comment;

                ratingModelRow["RatingModelInfo"] = string.Format(Resources.NoRatingCurvesFoundWithXY,
                  ratingModelIdentifier, locationName);

                RatingCurveListServiceResponse ratingCurveListResponse = _Common.GetRatingCurveList(ratingModelIdentifier);

                if (ratingCurveListResponse != null)
                {
                    ratingModelRow["RatingModelInfo"] = string.Format(Resources.RatingModelInformation,
                      ratingModelIdentifier, locationName,
                      inputParameterDisplayId, _Common.GetUnitSymbol(inputUnitId),
                      outputParameterDisplayId, _Common.GetUnitSymbol(outputUnitId),
                      ratingCurveListResponse.RatingCurves.Count);
                }
            }

            return ratingModelTable;
        }

        public string GetReportSubTitle()
        {
            string reportTitle = _Common.GetParameterString("ReportTitle", "");
            string description = _Common.GetParameterString("Description", "");
            string subTitle = reportTitle + ((!string.IsNullOrEmpty(reportTitle) && !string.IsNullOrEmpty(description)) ? " - " : "") + description;
            Log.DebugFormat("ReportSubTitle = '{0}'", subTitle);
            return subTitle;
        }

        public string GetOrganizationIconEncodedImage()
        {
            return _RunReportRequest.ReportData.GetSystemConfiguration().OrganizationIconBase64;
        }

        public string GetOrganizationSupportUrl()
        {
            return _RunReportRequest.ReportData.GetSystemConfiguration().OrganizationSupportUrl;
        }

        public string GetFooterDisclaimer()
        {
            return _RunReportRequest.ReportData.GetSystemConfiguration().PageFooter;
        }

        public string GetPageHeader1()
        {
            return _RunReportRequest.ReportData.GetSystemConfiguration().PageHeader;
        }

        public string GetPageHeader2()
        {
            return _RunReportRequest.ReportData.GetSystemConfiguration().PageSubheader;
        }
    }
}


// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\DoubleValueFormatter.cs"
namespace TimeSeriesPlotNamespace
{
    public class DoubleValueFormatter
    {
        public static string FormatDoubleValue(double value, bool fix, int places, string missingStr)
        {
            if (double.IsNaN(value))
                return missingStr;

            if (fix)
            {
                double roundVal = Math.Round(value, places, MidpointRounding.ToEven);
                string formatStr = "F" + places.ToString();
                return roundVal.ToString(formatStr, System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return FormatSigFigsNumber(value, places);
            }
        }

        public static string FormatSigFigsNumber(double value, int sigfigs)
        {
            string str = "";
            try
            {
                if (value != 0.0)
                {
                    bool negative = false;
                    if (value < 0.0)
                    {
                        negative = true;
                        value = -1.0 * value;
                    }
                    int exponent = (int)Math.Floor(Math.Log(value) / Math.Log(10));
                    int places = exponent - sigfigs + 1;
                    double divisor = Math.Pow(10.0, (double)(places));

                    double round = (double)Math.Round(value / divisor);

                    decimal result = (decimal)round * (decimal)divisor;

                    if (negative)
                    {
                        result = (decimal)-1.0 * result;
                    }

                    string zeros = "";
                    if (places < 0)
                    {
                        int nz = -1 * places;
                        for (int i = 0; i < nz; i++)
                        {
                            zeros += "0";
                        }
                    }
                    string theFormatStr = "{0:." + zeros + "}";

                    str = string.Format(System.Globalization.CultureInfo.InvariantCulture, theFormatStr, result);
                }
                else
                {
                    string theFormatStr = "{0}";
                    str = string.Format(System.Globalization.CultureInfo.InvariantCulture, theFormatStr, value);
                }
            }
            catch { }

            return str;
        }
        public static string FormatPointValue(TimeSeriesPoint point, StatisticType statType)
        {
            string missingStr = "***";
            bool fix = true;

            if (!point.Value.HasValue) return missingStr;

            if (statType == StatisticType.Count)
                return FormatDoubleValue(point.Value.Value, fix, 0, missingStr);
            else if (statType == StatisticType.Sum)
                return FormatSumValue(point.Value.Value, missingStr);

            return FormatDoubleValue(point.Value.Value, fix, 3, missingStr);
        }

        public static string FormatSumValue(double value, string missingStr)
        {
            if (double.IsNaN(value)) return missingStr;

            int numPlaces = (value > 9999.5) ? 0 : ((value > 999.5) ? 1 : ((value > 99.5) ? 2 : 3));

            bool fix = true;

            return FormatDoubleValue(value, fix, numPlaces, missingStr);
        }
    }
}


// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\GroupByHandler.cs"
namespace TimeSeriesPlotNamespace
{
    public class GroupByHandler
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _WaterYearMonth = 10;
        private Common _Common;

        public GroupByHandler(Common common)
        {
            _Common = common;
            _WaterYearMonth = common.GetWaterYearMonth();
        }

        public DateTimeOffsetInterval AdjustIntervalToGroupBy(DateTimeOffsetInterval SelectedPeriod, DateTimeOffsetInterval TimeSeriesRange, string GroupBy, TimeSpan timeseriesOffset)
        {
            Log.DebugFormat("AdjustIntervalToGroupBy selected interval = {0}, timeseries range = {1}, groupBy = {2}, tsOffset = {3}",
                _Common.TimeRangeString(SelectedPeriod), _Common.TimeSeriesRangeString(TimeSeriesRange), GroupBy, timeseriesOffset);

            DateTimeOffsetInterval reportPeriod = SelectedPeriod;

            try
            {
                DateTimeOffsetInterval selectedInterval = _Common.GetIntervalInUtcOffset(SelectedPeriod, timeseriesOffset);
                DateTimeOffsetInterval trimmedPeriodSelected = GetTrimmedPeriodSelected(selectedInterval);
                DateTimeOffsetInterval overlappedInterval = GetIntervalOfOverlap(trimmedPeriodSelected, TimeSeriesRange, timeseriesOffset);

                reportPeriod = AdjustIntervalToGroupBy(overlappedInterval, GroupBy);
            }
            catch (Exception exp)
            {
                Log.Error("Error in AdjustIntervalToGroupBy", exp);
            }
            Log.DebugFormat("AdjustIntervalToGroupBy returns report period = {0}", _Common.TimeRangeString(reportPeriod));

            return reportPeriod;
        }

        public static DateTimeOffsetInterval GetTrimmedPeriodSelected(DateTimeOffsetInterval periodSelected)
        {
            DateTimeOffset? endTime = periodSelected.End;
            if (endTime.HasValue)
            {
                if ((endTime.Value.Hour == 0) && (endTime.Value.Minute == 0) && (endTime.Value.Second == 0))
                    endTime = endTime.Value.AddMilliseconds(-1); // avoid having endTime be at exact start of day/month/year
            }
            DateTimeOffset? startTime = periodSelected.Start;
            if (startTime.HasValue)
            {
                startTime = new DateTimeOffset(startTime.Value.Year, startTime.Value.Month, startTime.Value.Day, 0, 0, 0, startTime.Value.Offset);
            }
            if (startTime.HasValue && endTime.HasValue && endTime.Value < startTime.Value)
                endTime = startTime.Value;
            return new DateTimeOffsetInterval(startTime, endTime);
        }

        public DateTimeOffsetInterval GetIntervalOfOverlap(DateTimeOffsetInterval SelectedPeriod, DateTimeOffsetInterval TimeSeriesRange, TimeSpan utcOffset)
        {
            DateTimeOffset? StartTime = null;
            DateTimeOffset? EndTime = null;

            if (SelectedPeriod.Start.HasValue && TimeSeriesRange.Start.HasValue)
                StartTime = (SelectedPeriod.Start.Value > TimeSeriesRange.Start.Value) ? SelectedPeriod.Start.Value.ToOffset(utcOffset) :
                    (SelectedPeriod.End.HasValue && (SelectedPeriod.End.Value < TimeSeriesRange.Start.Value)) ?
                    SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.Start.Value;
            else
                StartTime = (SelectedPeriod.Start.HasValue) ? SelectedPeriod.Start.Value.ToOffset(utcOffset) : TimeSeriesRange.Start;

            if (SelectedPeriod.End.HasValue && TimeSeriesRange.End.HasValue)
                EndTime = (SelectedPeriod.End.Value < TimeSeriesRange.End.Value) ? SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.End.Value;
            else
                EndTime = (SelectedPeriod.End.HasValue) ? SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.End;

            if (!TimeSeriesRange.Start.HasValue && !TimeSeriesRange.End.HasValue)
            {
                // TimeSeries has no range - no points in it and will be no useful time range therefore collapse time range
                if (StartTime.HasValue)
                    EndTime = StartTime.Value;
                else if (EndTime.HasValue)
                    StartTime = EndTime.Value;
            }

            DateTimeOffset now = DateTimeOffset.Now.ToOffset(utcOffset);

            DateTimeOffset TheStartTime = (StartTime.HasValue) ? StartTime.Value.ToOffset(utcOffset) : now;
            DateTimeOffset TheEndTime = (EndTime.HasValue) ? EndTime.Value.ToOffset(utcOffset) : now;

            TheEndTime = (TheEndTime > TheStartTime) ? TheEndTime : TheStartTime; // avoid improper interval

            return new DateTimeOffsetInterval(TheStartTime, TheEndTime);
        }
        public DateTimeOffsetInterval AdjustIntervalToGroupBy(DateTimeOffsetInterval timeRange, string GroupBy)
        {
            Log.DebugFormat("Begin GroupBy '{0}' adjustments for interval {1} - {2}", GroupBy, timeRange.Start, timeRange.End);

            if (!timeRange.Start.HasValue || !timeRange.End.HasValue)
            {
                Log.DebugFormat("AdjustIntervalToGroupBy unable to adjust timeRange interval = {0} because start or end has no value, return unadjusted", _Common.TimeRangeString(timeRange));
                return timeRange;
            }

            try
            {
                DateTimeOffset TheStartTime = timeRange.Start.Value;
                DateTimeOffset TheEndTime = timeRange.End.Value;
                TimeSpan tsOffset = TheStartTime.Offset;

                if (GroupBy == "Year")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, 1, 1, 0, 0, 0, tsOffset);
                    TheEndTime = (new DateTimeOffset(TheEndTime.Year, 1, 1, 0, 0, 0, tsOffset)).AddYears(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "WaterYear")
                {
                    int startYear = (TheStartTime.Month >= _WaterYearMonth) ? TheStartTime.Year : TheStartTime.Year - 1;
                    TheStartTime = new DateTimeOffset(startYear, _WaterYearMonth, 1, 0, 0, 0, tsOffset);

                    int endYear = (TheEndTime.Month >= _WaterYearMonth) ? TheEndTime.Year : TheEndTime.Year - 1;
                    TheEndTime = (new DateTimeOffset(endYear, _WaterYearMonth, 1, 0, 0, 0, tsOffset)).AddYears(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "Month")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, 1, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, 1, 0, 0, 0, tsOffset).AddMonths(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "Week")
                {
                    int numberOfDays = ((int)Math.Ceiling(((TheEndTime - TheStartTime).TotalDays + 1) / 7.0)) * 7;
                    TheEndTime = TheStartTime.AddDays(numberOfDays);

                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddMilliseconds(-1);
                }
                else if (GroupBy == "Day")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddDays(1).AddMilliseconds(-1);
                }
                else
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddDays(1).AddMilliseconds(-1);
                }

                Log.DebugFormat("End GroupBy '{0}' adjustments for interval {1} - {2}", GroupBy, TheStartTime, TheEndTime);
                timeRange = new DateTimeOffsetInterval(TheStartTime, TheEndTime);
            }
            catch (Exception exp)
            {
                Log.Error("Error in AdjustIntervalToGroupBy", exp);
            }
            Log.DebugFormat("AdjustIntervalToGroupBy returns timeRange interval = {0}", _Common.TimeRangeString(timeRange));

            return timeRange;
        }

        public DataRelation AddGroupRelation(string GroupBy, string relationName, DataTable groupBy, DataTable pointsTable)
        {
            DataSet dataSet = groupBy.DataSet;
            DataRelation relation;
            if (GroupBy == "Year")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["Year"], pointsTable.Columns["Year"]);
            }
            else if (GroupBy == "WaterYear")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["WaterYear"], pointsTable.Columns["WaterYear"]);
            }
            else if (GroupBy == "Month")
            {
                relation = dataSet.Relations.Add(relationName,
                  new DataColumn[] { groupBy.Columns["Year"], groupBy.Columns["Month"] },
                  new DataColumn[] { pointsTable.Columns["Year"], pointsTable.Columns["Month"] });
            }
            else if (GroupBy == "Day")
            {
                relation = dataSet.Relations.Add(relationName,
                  new DataColumn[] { groupBy.Columns["Year"], groupBy.Columns["Month"], groupBy.Columns["Day"] },
                  new DataColumn[] { pointsTable.Columns["Year"], pointsTable.Columns["Month"], pointsTable.Columns["Day"] });
            }
            else if (GroupBy == "Week")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["Week"], pointsTable.Columns["Week"]);
            }
            else
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["NoGroupBy"], pointsTable.Columns["NoGroupBy"]);
            }

            return relation;
        }

        public DataTable GetGroupByTable(string tableName)
        {
            DataTable groupBy = new DataTable(tableName);

            groupBy.Columns.Add("NoGroupBy", typeof(int));
            groupBy.Columns.Add("Year", typeof(int));
            groupBy.Columns.Add("WaterYear", typeof(int));
            groupBy.Columns.Add("Month", typeof(int));
            groupBy.Columns.Add("Day", typeof(int));
            groupBy.Columns.Add("Week", typeof(int));
            groupBy.Columns.Add("StartTime", typeof(DateTimeOffset));
            groupBy.Columns.Add("EndTime", typeof(DateTimeOffset));

            return groupBy;
        }

        public void AddRowsToGroupBy(DataTable groupBy, string GroupBy, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            if (GroupBy == "Year")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, 1, 1, 0, 0, 0, startTime.Offset);

                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddYears(1).AddMilliseconds(-1));
                    time = time.AddYears(1);
                }
            }
            else if (GroupBy == "WaterYear")
            {
                int firstYear = (startTime.Month >= _WaterYearMonth) ? startTime.Year : startTime.Year - 1;
                DateTimeOffset time = new DateTimeOffset(firstYear, _WaterYearMonth, 1, 0, 0, 0, startTime.Offset);

                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddYears(1).AddMilliseconds(-1));
                    time = time.AddYears(1);
                }
            }
            else if (GroupBy == "Month")
            {
                int monthIntervalCount = _Common.GetParameterInt("IntervalsPerPage", 1);
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, 1, 0, 0, 0, startTime.Offset);
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddMonths(monthIntervalCount).AddMilliseconds(-1));
                    time = time.AddMonths(monthIntervalCount);
                }
            }
            else if (GroupBy == "Day")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, 0, 0, 0, startTime.Offset);
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddDays(1).AddMilliseconds(-1));
                    time = time.AddDays(1);
                }
            }
            else if (GroupBy == "Week")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, 0, 0, 0, startTime.Offset);
                int weekNumber = 1;
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, weekNumber++, time, time.AddDays(7).AddMilliseconds(-1));
                    time = time.AddDays(7);
                }
            }
            else
            {
                AddGroupByRow(groupBy, 0, startTime, endTime);
            }
        }

        private void AddGroupByRow(DataTable groupBy, int weekNumber, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            DataRow row = groupBy.NewRow();

            row["NoGroupBy"] = 0;
            row["Year"] = startTime.Year;
            row["WaterYear"] = (startTime.Month >= _WaterYearMonth) ? startTime.Year : startTime.Year - 1;
            row["Month"] = startTime.Month;
            row["Day"] = startTime.Day;
            row["Week"] = weekNumber;
            row["StartTime"] = startTime;
            row["EndTime"] = endTime;

            Log.DebugFormat("AddGroupByRow: year = {0}, wateryear = {1}, month = {2}, day = {3}, week = {4}, starttime = {5}, endtime = {6}",
              row["Year"], row["WaterYear"], row["Month"], row["Day"], row["Week"], row["StartTime"], row["EndTime"]);

            groupBy.Rows.Add(row);
        }
    }
}


// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\PointsDataTableBuilder.cs"
namespace TimeSeriesPlotNamespace
{
    public class PointsDataTableBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public PointsDataTableBuilder(Common common)
        {
            _Common = common;
        }

        public DataTable GetPointsTable(string tableName)
        {
            DataTable pointsTable = new DataTable(tableName);

            pointsTable.Columns.Add("NoGroupBy", typeof(int));
            pointsTable.Columns.Add("Year", typeof(int));
            pointsTable.Columns.Add("WaterYear", typeof(int));
            pointsTable.Columns.Add("Month", typeof(int));
            pointsTable.Columns.Add("Day", typeof(int));
            pointsTable.Columns.Add("Week", typeof(int));
            pointsTable.Columns.Add("TimeStamp", typeof(DateTimeOffset));
            pointsTable.Columns.Add("TimeStampDouble", typeof(double));
            pointsTable.Columns.Add("Value", typeof(double));
            pointsTable.Columns.Add("DisplayValue", typeof(string));

            return pointsTable;
        }

        public DataTable AddPointsToTable(DataTable groupBy, DataTable pointsTable, List<TimeSeriesPoint> points)
        {
            foreach (TimeSeriesPoint point in points)
            {
                DateTimeOffset dateTimeOffset = point.Timestamp;

                double pointValue = (point.Value.HasValue) ? pointValue = point.Value.Value : double.NaN;
                AddPointToTable(groupBy, pointsTable, dateTimeOffset, pointValue, pointValue.ToString());
            }

            RemoveUnneededGroupByRows(groupBy, pointsTable);

            return pointsTable;
        }

        public void RemoveUnneededGroupByRows(DataTable groupBy, DataTable pointsTable)
        {
            // after all points have been added, remove trailing group-by rows if contain no points, except leave at least one row

            foreach (DataRelation relation in groupBy.DataSet.Relations)
            {
                if ((relation.ParentTable == groupBy) && (relation.ChildTable == pointsTable))
                {
                    while (groupBy.Rows.Count > 1)
                    {
                        DataRow[] childRows = groupBy.Rows[groupBy.Rows.Count - 1].GetChildRows(relation);
                        if (childRows.Length == 0) groupBy.Rows.Remove(groupBy.Rows[groupBy.Rows.Count - 1]);
                        else break;
                    }
                }
            }
        }

        public void AddPointToTable(DataTable groupBy, DataTable pointsTable, DateTimeOffset timestamp, double value, string displayValue)
        {
            int weekNumber = -1;
            foreach (DataRow groupRow in groupBy.Rows)
            {
                DateTimeOffset startTime = (DateTimeOffset)groupRow["StartTime"];
                DateTimeOffset endTime = (DateTimeOffset)groupRow["EndTime"];

                if ((startTime <= timestamp) && (timestamp < endTime))
                {
                    weekNumber = (int)groupRow["Week"];
                    break;
                }
            }
            if (weekNumber == -1)
            {
                Log.DebugFormat("AddPointToTable - weekNumber is -1 which is invalid so SKIP, {0}, {1}", timestamp, value);
                return;
            }
            DataRow row = pointsTable.NewRow();
            row["NoGroupBy"] = 0;
            row["Year"] = timestamp.Year;
            row["WaterYear"] = (timestamp.Month >= _Common.GetWaterYearMonth()) ? timestamp.Year : timestamp.Year - 1;
            row["Month"] = timestamp.Month;
            row["Day"] = timestamp.Day;
            row["Week"] = weekNumber;
            row["TimeStamp"] = timestamp;
            row["TimeStampDouble"] = timestamp.DateTime.ToOADate();
            row["Value"] = value;
            row["DisplayValue"] = displayValue;

            pointsTable.Rows.Add(row);
        }
    }
}


// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\TimeSeriesTimeRangeFinder.cs"
namespace TimeSeriesPlotNamespace
{
    public class TimeSeriesTimeRangeFinder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public TimeSeriesTimeRangeFinder(Common common)
        {
            _Common = common;
        }

        public DateTimeOffsetInterval FindTimeSeriesTimeRange(Guid timeseriesUniqueId)
        {
            List<TimeSeriesPoint> points = FindAnnualCountPoints(timeseriesUniqueId);
            if (points.Count == 0) return new DateTimeOffsetInterval();

            TimeSpan offset = points[0].Timestamp.Offset;

            int binAdjust = _Common.GetBinAdjustment(timeseriesUniqueId);

            DateTimeOffset FirstYear = new DateTimeOffset(points[0].Timestamp.Year + binAdjust, 1, 1, 0, 0, 0, offset);
            DateTimeOffset LastYear = new DateTimeOffset(points[points.Count - 1].Timestamp.Year + binAdjust, 1, 1, 0, 0, 0, offset);
            Log.DebugFormat("Found first point is in Year = {0}, last point is in Year = {1}", FirstYear, LastYear);

            points = FindDailyCountPoints(timeseriesUniqueId, FirstYear, FirstYear.AddYears(1).AddDays(1));
            if (points.Count == 0) return new DateTimeOffsetInterval();

            DateTimeOffset firstTime = FirstNonZeroPoint(points).Timestamp;
            DateTimeOffset queryFromTime = new DateTimeOffset(firstTime.Year, firstTime.Month, firstTime.Day, 0, 0, 0, offset);
            points = _Common.GetTimeSeriesPoints(timeseriesUniqueId, queryFromTime.AddDays(binAdjust), queryFromTime.AddDays(binAdjust + 1));
            if (points.Count == 0) return new DateTimeOffsetInterval();

            firstTime = points[0].Timestamp;
            Log.DebugFormat("Found first point at time = {0}", firstTime.ToString(Common._DateFormat));

            points = FindDailyCountPoints(timeseriesUniqueId, LastYear, LastYear.AddYears(1).AddDays(1));
            if (points.Count == 0) return new DateTimeOffsetInterval(firstTime, null);

            DateTimeOffset lastTime = LastNonZeroPoint(points).Timestamp;
            queryFromTime = new DateTimeOffset(lastTime.Year, lastTime.Month, lastTime.Day, 0, 0, 0, offset);
            points = _Common.GetTimeSeriesPoints(timeseriesUniqueId, queryFromTime.AddDays(binAdjust), queryFromTime.AddDays(binAdjust + 1));
            if (points.Count == 0) return new DateTimeOffsetInterval(firstTime, null);

            lastTime = points[points.Count - 1].Timestamp;
            Log.DebugFormat("Found last point at time = {0}", lastTime.ToString(Common._DateFormat));

            DateTimeOffsetInterval interval = new DateTimeOffsetInterval(firstTime, lastTime);
            return interval;
        }

         private List<TimeSeriesPoint> FindAnnualCountPoints(Guid timeseriesUniqueId)
        {
            Log.DebugFormat("FindAnnualCountPoints for uniqueId = {0} over entire range (StartTime and EndTime set to null)", timeseriesUniqueId);

            return _Common.GetComputedStatisticsPoints(timeseriesUniqueId, StatisticType.Count, StatisticPeriod.Annual, false, null);
        }

        private List<TimeSeriesPoint> FindDailyCountPoints(Guid timeseriesUniqueId, DateTimeOffset fromTime, DateTimeOffset toTime)
        {
            Log.DebugFormat("FindDailyCountPoints for uniqueId = {0} from {1} to {2}",
                timeseriesUniqueId, fromTime.ToString(Common._DateFormat), toTime.ToString(Common._DateFormat));

            return _Common.GetComputedStatisticsPoints(timeseriesUniqueId, fromTime, toTime, StatisticType.Count, StatisticPeriod.Daily, false, null);
        }

        private TimeSeriesPoint FirstNonZeroPoint(List<TimeSeriesPoint> points)
        {
            TimeSeriesPoint firstDataPoint = points[0];
            foreach (TimeSeriesPoint p in points)
                if (p.Value.HasValue && p.Value.Value > 0)
                {
                    firstDataPoint = p;
                    break;
                }
            Log.DebugFormat("FirstNonZeroPoint at timestamp = {0}, value = {1}", firstDataPoint.Timestamp.ToString(Common._DateFormat), firstDataPoint.Value);
            return firstDataPoint;
        }

        private TimeSeriesPoint LastNonZeroPoint(List<TimeSeriesPoint> points)
        {
            points.Reverse();
            TimeSeriesPoint lastPoint = FirstNonZeroPoint(points);
            Log.DebugFormat("LastNonZeroPoint at timestamp = {0}, value = {1}", lastPoint.Timestamp.ToString(Common._DateFormat), lastPoint.Value);
            return lastPoint;
        }
    }
}






// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.Common\TimeSeriesInstMinMaxFinder.cs"
namespace TimeSeriesPlotNamespace
{
    public class TimeSeriesInstMinMaxFinder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public TimeSeriesInstMinMaxFinder(Common common)
        {
            _Common = common;
        }
        public TimeAlignedPoint[] GetInstMinMaxPoints(Guid inputGuid, string interval, bool extrema, DateTimeOffset StartTime, DateTimeOffset EndTime)
        {
            int hoursToExtend = 12; // amount of extra time for finding the interpolated boundary value at start and end times
            List<TimeAlignedPoint> points = _Common.GetTimeAlignedPoints(new List<Guid> { inputGuid },
                StartTime.AddHours(-1 * hoursToExtend), EndTime.AddHours(hoursToExtend));

            DateTimeOffset[] binEnds = GetDayEnds(StartTime, EndTime);

            if (interval == "Monthly")
                binEnds = GetMonthEnds(StartTime, EndTime);
            if (interval == "Yearly")
                binEnds = GetYearEnds(StartTime, EndTime);

            return GetInstMinMaxPoints(points, extrema, StartTime, binEnds);
        }


        public static DateTimeOffset[] GetDayEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> midnights = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddDays(1);
            while (time <= theEndTime.AddDays(1))
            {
                midnights.Add(time);
                time = time.AddDays(1);
            }

            return midnights.ToArray();
        }


        public static DateTimeOffset[] GetMonthEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> times = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddMonths(1);
            while (time <= theEndTime.AddDays(1))
            {
                times.Add(time);
                time = time.AddMonths(1);
            }

            return times.ToArray();
        }

        public static DateTimeOffset[] GetYearEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> times = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddYears(1);
            while (time <= theEndTime.AddDays(1))
            {
                times.Add(time);
                time = time.AddYears(1);
            }

            return times.ToArray();
        }

        public static TimeAlignedPoint[] GetInstMinMaxPoints(List<TimeAlignedPoint> inputPoints,
          bool extrema, DateTimeOffset theStartTime, DateTimeOffset[] binEnds)
        {
            bool MIN = true;
            bool MAX = false;

            TimeAlignedPoint[] pointsOfExtrema = new TimeAlignedPoint[binEnds.Length];
            for (int i = 0; i < binEnds.Length; i++) pointsOfExtrema[i] = new TimeAlignedPoint();

            if (inputPoints.Count == 0) return pointsOfExtrema;

            int j = 0;
            double leftBinValue = double.NaN;

            while ((j < inputPoints.Count) && inputPoints[j].Timestamp < theStartTime) j++;

            if (j >= inputPoints.Count) return pointsOfExtrema;

            if ((inputPoints[j].Timestamp >= theStartTime) && (j > 0))
                leftBinValue = calcInterpolatedPointValue(inputPoints[j - 1], inputPoints[j], theStartTime);

            for (int i = 0; i < binEnds.Length; i++)
            {
                DateTimeOffset currentTime = (i > 0) ? binEnds[i - 1] : theStartTime;
                double currentVal = leftBinValue;

                Log.DebugFormat("i = {0}, j = {1}, leftBinValue = {2}, time = {3}", i, j, leftBinValue, currentTime);

                for (; j < inputPoints.Count; j++)
                {
                    TimeAlignedPoint inputPoint = inputPoints[j];

                    if (inputPoint.Timestamp > binEnds[i])
                        break;

                    if ((inputPoint.NumericValue1.HasValue) &&
                      (double.IsNaN(currentVal) ||
                      ((extrema == MIN) && (inputPoint.NumericValue1.Value < currentVal)) ||
                      ((extrema == MAX) && (inputPoint.NumericValue1.Value > currentVal))))
                    {
                        currentTime = inputPoint.Timestamp;
                        currentVal = inputPoint.NumericValue1.Value;
                    }
                }

                double rightBinValue = double.NaN;

                if ((j > 0) && (j < inputPoints.Count) && (inputPoints[j].Timestamp > binEnds[i]))
                {
                    DateTimeOffset binEnd = binEnds[i];
                    rightBinValue = calcInterpolatedPointValue(inputPoints[j - 1], inputPoints[j], binEnd);
                    Log.DebugFormat("checking boundary extrema i = {0}, j = {1}, binEnd = {2}, rightBinValue = {3}", i, j, binEnd, rightBinValue);
                    if (!double.IsNaN(rightBinValue) &&
                      (double.IsNaN(currentVal) ||
                      ((extrema == MIN) && (rightBinValue < currentVal)) ||
                      ((extrema == MAX) && (rightBinValue > currentVal))))
                    {
                        currentTime = binEnd;
                        currentVal = rightBinValue;
                        Log.DebugFormat("checking boundary extrema i = {0}, j = {1}, currentTime = {2}, currentVal = {3}", i, j, currentTime, currentVal);
                    }
                }

                if (!double.IsNaN(currentVal))
                {
                    pointsOfExtrema[i].Timestamp = currentTime;
                    pointsOfExtrema[i].NumericValue1 = currentVal;
                    Log.DebugFormat("setting extrema i = {0}, j = {1}, currentTime = {2}, currentVal = {3}", i, j, currentTime, currentVal);
                }

                leftBinValue = rightBinValue;
            }

            return pointsOfExtrema;
        }

        public static double calcInterpolatedPointValue(TimeAlignedPoint point1, TimeAlignedPoint point2, DateTimeOffset theX)
        {
            double y1 = double.NaN;
            double y2 = double.NaN;

            if (goodValuePoint(point1)) y1 = point1.NumericValue1.Value;
            if (goodValuePoint(point2)) y2 = point2.NumericValue1.Value;

            return calcInterpolatedValue(point1.Timestamp, y1, point2.Timestamp, y2, theX);
        }

        public static bool goodValuePoint(TimeAlignedPoint point)
        {
            return point.NumericValue1.HasValue; // possible enhancement to check grade code
        }

        public static double calcInterpolatedValue(DateTimeOffset x1, double y1, DateTimeOffset x2, double y2, DateTimeOffset theX)
        {
            if ((x1 <= theX) && (theX <= x2))
            {
                if (x1 == theX)
                    return y1;

                if (x2 == theX)
                    return y2;

                if (!double.IsNaN(y1) && !double.IsNaN(y2))
                {
                    double timeFraction = ((double)(theX - x1).Ticks) / ((double)(x2 - x1).Ticks);

                    double calc = y1 + ((y2 - y1) * timeFraction);

                    return calc;
                }
            }
            return double.NaN;
        }
    }
}


// Copy of shared file: "C:\Dev\AQTSReports\src\Custom\src\Reports\TimeSeriesPlot\..\..\Reports.ReportPluginBase\ReportPluginBase.cs"
namespace TimeSeriesPlotNamespace
{
    public abstract class ReportPluginBase
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Assembly _Assembly = Assembly.GetExecutingAssembly();

        public virtual FileReportOutput GenerateReport(RunFileReportRequest request)
        {
            FileReportOutput fileReportOutput = new FileReportOutput();

            string fileName = GenerateReportIntoFile(request);
            fileReportOutput.OutputFilePath = fileName;

            return fileReportOutput;
        }

        public string GenerateReportIntoFile(RunFileReportRequest request)
        {
            Thread.CurrentThread.CurrentCulture.NumberFormat = CultureInfo.InvariantCulture.NumberFormat;

            DataSet set = GenerateDataSet(request);
            Log.Debug("GenerateReport - after generate dataset");

            string dllFolder = (string)set.Tables["RunReportRequest"].Rows[0]["DllFolder"];
            string dllName = (string)set.Tables["RunReportRequest"].Rows[0]["DllName"];

            string templateFileName = Path.Combine(dllFolder, dllName + ".rst");

            Log.DebugFormat("GenerateReport rst template file name is {0}", templateFileName);

            string xmlTemplate = File.ReadAllText(templateFileName);

            Log.Debug("GenerateReport after read rst template file");

            ReportManager reportManager = new ReportManager();
            reportManager.DataSources.Add(set.DataSetName, set);
            reportManager.OwnerForm = null;
            Log.Debug("GenerateReport after init ReportManager");

            InlineReportSlot inlineReportSlot = new InlineReportSlot();
            inlineReportSlot.DocumentStream = xmlTemplate;
            inlineReportSlot.RenderingError += InlineReportSlot_RenderingError;
            Log.Debug("GenerateReport after init InlineReportSlot");

            reportManager.Reports.Add(inlineReportSlot);
            Document document = inlineReportSlot.RenderDocument();

            Log.Debug("GenerateReport after RenderDocument");
            string outputFormat = request.OutputFormat;
            Log.DebugFormat("GenerateReport after RenderDocument, document name is {0}, page count is {1}, outputFormat is {2}",
                document.Name, document.Pages.Count, outputFormat);

            string tempFileName = Path.GetTempFileName();
            string outputFileName = Path.ChangeExtension(tempFileName, outputFormat);
            try
            {
                File.Delete(tempFileName);
            }
            catch { }
            try
            {
                File.Delete(outputFileName);
            }
            catch { }

            Log.DebugFormat("GenerateReport - the name of file for export report to write to is set to {0}", outputFileName);

            try
            {
                switch (outputFormat.ToUpper())
                {
                    case "PDF":
                        {
                            PdfExportFilter exportFilter = new PdfExportFilter();

                            exportFilter.Compress = true;
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to PDF into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "CSV":
                        {
                            CsvExportFilter exportFilter = new CsvExportFilter();

                            exportFilter.Separator = ",";
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to CSV into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "XLSX":
                        {
                            ExcelExportFilter exportFilter = new ExcelExportFilter();
                            exportFilter.ExportAsData = true;
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to XLSX into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "PNG":
                        {
                            PngExportFilter exportFilter = new PngExportFilter();
                            exportFilter.PagesRange = "1";
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to PNG into tempFileName = {0}", outputFileName);
                            break;
                        }
                    default:
                        {
                            Log.DebugFormat("Unable to export report - unknown export format '{0}'", outputFormat);
                            break;
                        }
                }
            }
            catch (System.Exception exp)
            {
                Log.Debug("Exception during export report", exp);
                throw exp;
            }

            return outputFileName;
        }

        private void InlineReportSlot_RenderingError(object sender, RenderingErrorEventArgs e)
        {
            Log.Debug("Report Render Error");

            if (e.Exception is ScriptException scriptException)
                Log.Error($"Render ScriptException: {scriptException.Describe(true)}");

            Log.Error("Render Error: ", e.Exception);
            Log.Error("Render Error (innerException): ", e.Exception.InnerException);

            throw new System.Exception("Report Rendering Error", e.Exception);
        }

        public virtual DataSet GenerateDataSet(RunFileReportRequest request)
        {
            Assembly.Load("PerpetuumSoft.Reporting.MSChart");

            string location = System.IO.Path.GetDirectoryName(_Assembly.Location);
            string name = System.IO.Path.GetFileNameWithoutExtension(_Assembly.Location);
            FileVersionInfo fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            Log.InfoFormat("Report plugin folder = {0}, plugin name = {1}, plugin version = {2}", location, name, fileVersionInfo.ProductVersion);

           System.Data.DataSet reportTables = (new Common(request)).GetCommonDataSet(name, location);

            AddReportSpecificTables(reportTables);
            return reportTables;
        }

        public virtual void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
        }
    }
}

