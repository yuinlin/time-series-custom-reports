using System;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using ReportPluginFramework.ReportData.TimeSeriesDescription;
using System.Data;
using System.Reflection;
using ServiceStack;

namespace Reports
{
    public class DataTablesBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RunReportRequest _RunReportRequest;
        private Common _Common;
        public static string DateFormat = "yyyy-MM-dd HH:mmzzz";

        public string _DllName;
        public string _DllFolder;

        public DataTablesBuilder(RunReportRequest request, Common common)
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
            AddReportDataTable(set1, "ReportData");
            AddCommonTimeSeriesTables(set1);

            Log.Info("Returning CommonDataSet");
            return set1;
        }

        public void AddRunReportRequestTable(DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            table.Columns.Add("RunReportRequest", typeof(object));
            table.Columns.Add("DllName", typeof(string));
            table.Columns.Add("DllFolder", typeof(string));
            dataSet.Tables.Add(table);

            DataRow row = table.NewRow();
            row["RunReportRequest"] = _RunReportRequest;
            row["DllName"] = _DllName;
            row["DllFolder"] = _DllFolder;
            table.Rows.Add(row);
        }

        public void AddCommonTimeSeriesTables(DataSet dataSet)
        {
            AddReportTimeSeriesInputsTable(dataSet, "TimeSeriesInputs", _RunReportRequest.Inputs);

            ReportRequestInputs inputs = _RunReportRequest.Inputs;
            if (inputs == null) return;

            foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
            {
                AddTimeSeriesInputDataTable(dataSet, timeseriesInput);
                AddTimeSeriesLocationDataTable(dataSet, timeseriesInput);
            }
        }

        public void AddReportDataTable(System.Data.DataSet dataSet, string tableName)
        {
            if (dataSet.Tables.Contains(tableName)) return;

            dataSet.Tables.Add(ReportDataTable(tableName));
        }

        public void AddReportTimeSeriesInputsTable(System.Data.DataSet dataSet, string tableName, ReportRequestInputs inputs)
        {
            Log.InfoFormat("AddReportTimeSeriesInputsTable {0}", tableName);
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            dataSet.Tables.Add(table);
            if (inputs == null) return;

            foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
                table.Columns.Add(timeseriesInput.Name, typeof(Guid));

            System.Data.DataRow row = table.NewRow();
            foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
                row[timeseriesInput.Name] = timeseriesInput.UniqueId;
            table.Rows.Add(row);
        }

        public void AddTimeSeriesLocationDataTable(System.Data.DataSet dataSet, TimeSeriesReportRequestInput timeseriesInput)
        {
            string tableName = string.Format("{0}Location", timeseriesInput.Name);
            if (dataSet.Tables.Contains(tableName)) return;

            string locationIdentifier = _Common.GetTimeSeriesDescription(timeseriesInput.UniqueId).LocationIdentifier;
            dataSet.Tables.Add(LocationDataTable(tableName, locationIdentifier));
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
            Log.InfoFormat("Create ReportDataTable {0}", tableName);
            DataTable pageHeader = new DataTable(tableName);

            pageHeader.Columns.Add("SelectedInterval", typeof(DateTimeOffsetInterval));
            pageHeader.Columns.Add("PeriodSelectedString", typeof(string));
            pageHeader.Columns.Add("PeriodSelectedInformation", typeof(string));
            pageHeader.Columns.Add("IReportData", typeof(IReportData));
            pageHeader.Columns.Add("Locale", typeof(string));
            pageHeader.Columns.Add("PageHeader1", typeof(string));
            pageHeader.Columns.Add("PageHeader2", typeof(string));
            pageHeader.Columns.Add("FooterDisclaimer", typeof(string));
            pageHeader.Columns.Add("ReportSubTitle", typeof(string));

            foreach (ReportJobParameter reportParameter in _RunReportRequest.Parameters)
                pageHeader.Columns.Add(reportParameter.Name, typeof(string));

            DataRow row = pageHeader.NewRow();

            row["SelectedInterval"] = _RunReportRequest.Interval;
            row["PeriodSelectedString"] = _Common.PeriodSelectedString(_Common.GetPeriodSelectedInUtcOffset(_Common.GetDefaultOffset()));
            row["PeriodSelectedInformation"] = _Common.GetPeriodSelectedInformation(_Common.GetPeriodSelectedInUtcOffset(_Common.GetDefaultOffset()));
            row["IReportData"] = _RunReportRequest.ReportData;
            row["Locale"] = _RunReportRequest.Locale;
            row["PageHeader1"] = GetPageHeader1();
            row["PageHeader2"] = GetPageHeader2();
            row["FooterDisclaimer"] = GetFooterDisclaimer();
            row["ReportSubTitle"] = GetReportSubTitle();

            foreach (ReportJobParameter reportParameter in _RunReportRequest.Parameters)
                row[reportParameter.Name] = reportParameter.Value;

            pageHeader.Rows.Add(row);

            return pageHeader;
        }

        public DataTable TimeSeriesDataTable(string tableName, Guid timeseriesUniqueId)
        {
            Log.InfoFormat("Create TimeSeriesDataTable {0}, {1}", tableName, timeseriesUniqueId);
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
            timeSeriesTable.Columns.Add("LastModified", typeof(DateTime));
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
            dataRow["Computation"] = tsd.Computation;
            dataRow["ComputationPeriod"] = tsd.ComputationPeriod;
            dataRow["TimeSeriesType"] = tsd.TimeSeriesType;
            dataRow["InterpolationType"] = tsd.InterpolationType;
            dataRow["InterpolationTypeString"] = GetLegacyInterpolationTypeString(tsd.InterpolationType);
            dataRow["LastModified"] = tsd.LastModified;
            dataRow["RawStartTime"] = tsd.RawStartTime;
            dataRow["RawEndTime"] = tsd.RawEndTime;
            dataRow["Publish"] = tsd.Publish;
            dataRow["Unit"] = tsd.Unit;
            dataRow["UnitSymbol"] = _Common.GetTimeSeriesUnitSymbol(timeseriesUniqueId);
            dataRow["UnitInformation"] = _Common.GetTimeSeriesUnitInformation(timeseriesUniqueId);
            dataRow["UtcOffset"] = tsd.UtcOffset;
            dataRow["UtcOffsetString"] = _Common.GetOffsetString(tsd.UtcOffset);
            dataRow["TimeSeriesInformation"] = _Common.GetTimeSeriesInformation(timeseriesUniqueId);
            dataRow["TimeSeriesInterval"] = _Common.GetTimeSeriesTimeRange(timeseriesUniqueId);
            dataRow["TimeSeriesTimeRangeString"] = _Common.GetTimeSeriesTimeRangeString(timeseriesUniqueId);
            dataRow["TimeSeriesTimeRangeInformation"] = _Common.GetTimeSeriesTimeRangeInformation(timeseriesUniqueId);

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
            Log.InfoFormat("Create LocationDataTable {0}, {1}", tableName, locationIdentifier);
            DataTable locationTable = new DataTable(tableName);
            locationTable.Columns.Add("LocationIdentifier", typeof(string));
            locationTable.Columns.Add("LocationName", typeof(string));

            DataRow dataRow = locationTable.NewRow();

            dataRow["LocationIdentifier"] = locationIdentifier;
            dataRow["LocationName"] = locationIdentifier; //***todo: make this location name eventually!

            locationTable.Rows.Add(dataRow);

            return locationTable;
        }

        public string GetReportSubTitle()
        {
            string reportTitle = _Common.GetParameterString("ReportTitle", "");
            string description = _Common.GetParameterString("Description", "");
            string subTitle = reportTitle + ((!string.IsNullOrEmpty(reportTitle) && !string.IsNullOrEmpty(description)) ? " - " : "") + description;
            Log.InfoFormat("ReportSubTitle = '{0}'", subTitle);
            return subTitle;
        }

        public string GetFooterDisclaimer()
        {
            return "DISCLAIMER - AQUARIUS is the leading software suite to acquire, process, model, and publish water data." +
                " Environmental monitoring agencies worldwide trust AQUARIUS to produce accurate water information in real-time." +
                " A modern design delivers the latest water science in an intuitive experience.";
        }

        public string GetPageHeader1()
        {
            return "Aquatic Informatics Data Management Software";
        }

        public string GetPageHeader2()
        {
            return "Faster Analysis. Better Decisions.";
        }

        public System.Data.DataTable GetLegacyTimeSeriesMetadataTable(string tableName, Guid timeseriesUniqueId)
        {
            System.Data.DataTable tableTS = new System.Data.DataTable(tableName);

            tableTS.Columns.Add("Identifier");
            tableTS.Columns.Add("ParameterName");
            tableTS.Columns.Add("Units");
            tableTS.Columns.Add("Guid");
            tableTS.Columns.Add("AncestorLabel1");
            tableTS.Columns.Add("AncestorName1");
            tableTS.Columns.Add("Comment");
            tableTS.Columns.Add("Description");
            tableTS.Columns.Add("Min", typeof(double));
            tableTS.Columns.Add("Max", typeof(double));
            tableTS.Columns.Add("Mean", typeof(double));
            tableTS.Columns.Add("TotalGaps", typeof(double));
            tableTS.Columns.Add("TotalSamples", typeof(double));
            tableTS.Columns.Add("SamplingRate", typeof(double));
            tableTS.Columns.Add("MaxGapInterval", typeof(double));
            tableTS.Columns.Add("StartTime", typeof(DateTime));
            tableTS.Columns.Add("EndTime", typeof(DateTime));
            tableTS.Columns.Add("FirstTime", typeof(double));
            tableTS.Columns.Add("LastTime", typeof(double));
            tableTS.Columns.Add("StartValue", typeof(double));
            tableTS.Columns.Add("EndValue", typeof(double));
            tableTS.Columns.Add("NaN", typeof(double));
            tableTS.Columns.Add("TimeZone");

            TimeSeriesDescription tsd = _Common.GetTimeSeriesDescription(timeseriesUniqueId);

            System.Data.DataRow rowTS = tableTS.NewRow();

            rowTS["Guid"] = timeseriesUniqueId.ToString();
            rowTS["Identifier"] = tsd.Identifier;
            rowTS["ParameterName"] = tsd.Parameter;
            rowTS["Units"] = tsd.Unit;
            rowTS["Comment"] = tsd.Comment;
            rowTS["Description"] = tsd.Description;
            rowTS["MaxGapInterval"] = 0;
            rowTS["NaN"] = double.NaN;
            rowTS["TimeZone"] = "UTC" + _Common.GetOffsetString(tsd.UtcOffset);
            rowTS["AncestorName1"] = "Location";

            string locationName = _Common.GetLocationName(tsd.LocationIdentifier);
            rowTS["AncestorLabel1"] = (!string.IsNullOrEmpty(locationName)) ? locationName : tsd.LocationIdentifier;

            tableTS.Rows.Add(rowTS);

            return tableTS;
        }
    }
}
