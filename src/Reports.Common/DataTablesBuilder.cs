using System;
using System.Collections.Generic;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesDescription;
using ReportPluginFramework.Beta.ReportData.LocationDescription;
using ReportPluginFramework.Beta.ReportData.LocationData;
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

        public List<string> CommonReportParameters = new List<string> { "ReportTitle", "Description", "Comment" };

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
            AddReportSettingsTable(set1, "ReportSettings");
            AddReportDataTable(set1, "ReportData");
            AddInputsTables(set1);

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
            dataSet.Tables.Add(table);

            DataRow row = table.NewRow();
            row["RunReportRequest"] = _RunReportRequest;
            row["DllName"] = _DllName;
            row["DllFolder"] = _DllFolder;
            row["CommonLibrary"] = _Common;
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

        public void AddReportTimeSeriesInputsTable(System.Data.DataSet dataSet, string tableName, ReportRequestInputs inputs)
        {
            Log.DebugFormat("AddReportTimeSeriesInputsTable {0}", tableName);
            if (dataSet.Tables.Contains(tableName)) return;

            DataTable table = new DataTable(tableName);
            dataSet.Tables.Add(table);
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Guid", typeof(Guid));

            if (inputs == null) return;

            foreach (TimeSeriesReportRequestInput timeseriesInput in inputs.TimeSeriesInputs)
            {
                DataRow row = table.NewRow();
                row["Name"] = timeseriesInput.Name;
                row["Guid"] = timeseriesInput.UniqueId;
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

            DataRow row = table.NewRow();

            row["SelectedInterval"] = _RunReportRequest.Interval;
            row["PeriodSelectedString"] = _Common.PeriodSelectedString(_Common.GetPeriodSelectedInUtcOffset(_Common.GetDefaultOffset()));
            row["PeriodSelectedInformation"] = _Common.GetPeriodSelectedInformation(_Common.GetPeriodSelectedInUtcOffset(_Common.GetDefaultOffset()));
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
            DataRow dataRow = locationTable.NewRow();

            LocationDescription locDescription = _Common.GetLocationDescriptionByIdentifier(locationIdentifier);
            LocationDataResponse locData = _Common.GetLocationData(locationIdentifier);

            dataRow["UniqueId"] = locDescription.UniqueId;
            dataRow["LocationIdentifier"] = locationIdentifier;
            dataRow["LocationName"] = _Common.GetLocationName(locationIdentifier);
            dataRow["UtcOffset"] = locDescription.UtcOffset;
            dataRow["UtcOffsetString"] = _Common.GetOffsetString(locDescription.UtcOffset);
            dataRow["Description"] = locData.Description;
            dataRow["Latitude"] = locData.Latitude;
            dataRow["Longitude"] = locData.Longitude;
            dataRow["Elevation"] = locData.Elevation;
            dataRow["ElevationUnit"] = locData.ElevationUnits;
            dataRow["ElevationUnitSymbol"] = _Common.GetUnitSymbol(locData.ElevationUnits);
            dataRow["LocationType"] = locData.LocationType;
            dataRow["IsExternal"] = locData.IsExternal;

            locationTable.Rows.Add(dataRow);

            return locationTable;
        }

        public DataTable LocationExtendedAttributesTable(string tableName, string locationIdentifier)
        {
            Log.DebugFormat("Create LocationExtendedAttributesTable {0}, {1}", tableName, locationIdentifier);
            DataTable locationExtendedAttributesTable = new DataTable(tableName);

            LocationDataResponse locData = _Common.GetLocationData(locationIdentifier);
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

        public string GetReportSubTitle()
        {
            string reportTitle = _Common.GetParameterString("ReportTitle", "");
            string description = _Common.GetParameterString("Description", "");
            string subTitle = reportTitle + ((!string.IsNullOrEmpty(reportTitle) && !string.IsNullOrEmpty(description)) ? " - " : "") + description;
            Log.DebugFormat("ReportSubTitle = '{0}'", subTitle);
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
    }
}
