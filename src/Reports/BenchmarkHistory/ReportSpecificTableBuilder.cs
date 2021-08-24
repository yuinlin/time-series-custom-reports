using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Properties;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.Enum;
using System.Collections.Generic;

namespace BenchmarkHistoryNamespace
{
    public class ReportSpecificTableBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddReportSpecificTables(DataSet dataSet)
        {
            try
            {
                Common common = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];

                DataTable settingsTable = dataSet.Tables["ReportSettings"];
                settingsTable.Columns.Add("ReportTitle", typeof(string));
                settingsTable.Rows[0]["ReportTitle"] = Resources.BenchmarkHistory;

                DataTable table = new DataTable("BenchmarkHistoryDataTable");
                dataSet.Tables.Add(table);

                RunFileReportRequest runReportRequest = (RunFileReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                string locationIdentifier = (string)dataSet.Tables["LocationInput"].Rows[0]["Identifier"];

                string dllName = (string)dataSet.Tables["RunReportRequest"].Rows[0]["DllName"];
                Log.DebugFormat("{0} - Document GenerateScript input location = {1}", dllName, locationIdentifier);

                ///////////////////////////////////////////////////////////////
                DateTimeOffsetInterval reportPeriod = (DateTimeOffsetInterval)dataSet.Tables["ReportPeriods"].Rows[0]["NoGroupBy"];

                int formatPrecision = common.GetParameterInt("FormatPrecision", 3);
                string formatType = common.GetParameterString("FormatType", "Fixed");
                bool formatFixed = (formatType == "Fixed");

                int groupSizeLimit = 6;

                DataTable benchmarks = dataSet.Tables.Add("Benchmarks");

                benchmarks.Columns.Add("Name", typeof(string));
                benchmarks.Columns.Add("Elevation", typeof(string));
                benchmarks.Columns.Add("DateConstructed", typeof(string));
                benchmarks.Columns.Add("DateRemoved", typeof(string));
                benchmarks.Columns.Add("Status", typeof(string));
                benchmarks.Columns.Add("Description", typeof(string));

                DataTable groupBy = dataSet.Tables.Add("GroupBy");
                groupBy.Columns.Add("GroupNumber", typeof(int));
                for (int i = 0; i < groupSizeLimit; i++)
                {
                    groupBy.Columns.Add("Name" + (i + 1).ToString(), typeof(string));
                }

                DataTable benchmarkHistory = dataSet.Tables.Add("BenchmarkHistory");

                benchmarkHistory.Columns.Add("GroupNumber", typeof(int));
                benchmarkHistory.Columns.Add("DateTime", typeof(string));
                benchmarkHistory.Columns.Add("Party", typeof(string));
                benchmarkHistory.Columns.Add("Comments", typeof(string));
                for (int i = 0; i < groupSizeLimit; i++)
                {
                    benchmarkHistory.Columns.Add("Elevation" + (i + 1).ToString(), typeof(string));
                    benchmarkHistory.Columns.Add("Correction" + (i + 1).ToString(), typeof(string));
                }

                dataSet.Relations.Add("GroupByBenchmarkHistory", groupBy.Columns["GroupNumber"], benchmarkHistory.Columns["GroupNumber"]);

                LocationDataServiceResponse locData = common.GetLocationData(locationIdentifier);

                List<ReferencePoint> refPoints = new List<ReferencePoint>();
                foreach (ReferencePoint refPoint in locData.ReferencePoints)
                {
                    List<ReferencePointPeriod> refPointPeriods = refPoint.ReferencePointPeriods;

                    if (refPointPeriods.Count == 0) continue;

                    refPointPeriods.Sort((x, y) => x.ValidFrom.CompareTo(y.ValidFrom));
                    DateTimeOffset validFrom = refPointPeriods[0].ValidFrom;

                    if (ReportSpecificFunctions.TimeRangeOverlaps(validFrom, refPoint.DecommissionedDate, reportPeriod))
                        refPoints.Add(refPoint);
                }

                if (refPoints.Count == 0) return;

                Dictionary<Guid, List<ReferencePointPeriod>> refPointHistories = new Dictionary<Guid, List<ReferencePointPeriod>>();
                Dictionary<Guid, int> refPointNumbers = new Dictionary<Guid, int>();

                for (int i = 0; i < refPoints.Count; i++)
                {
                    ReferencePoint refPoint = refPoints[i];

                    refPointNumbers.Add(refPoint.UniqueId, i);

                    DataRow row = benchmarks.NewRow();

                    row["Name"] = refPoint.Name;
                    row["Description"] = refPoint.Description;

                    if (refPoint.DecommissionedDate.HasValue)
                        row["DateRemoved"] = refPoint.DecommissionedDate.Value.ToString("yyyy-MM-dd");

                    string status = (refPoint.DecommissionedDate.HasValue) ? Resources.InactiveLowerCase : Resources.ActiveLowerCase;
                    status += (refPoint.PrimarySinceDate.HasValue) ? " " + Resources.PrimaryLowerCase : "";

                    row["Status"] = status;

                    DateTimeOffset? primarySince = refPoint.PrimarySinceDate;

                    List<ReferencePointPeriod> refPointPeriods = refPoint.ReferencePointPeriods;
                    refPointPeriods.Sort((x, y) => x.ValidFrom.CompareTo(y.ValidFrom));

                    DateTimeOffset validFrom = refPointPeriods[0].ValidFrom;
                    row["DateConstructed"] = validFrom.ToString("yyyy-MM-dd");

                    double elevation = refPointPeriods[refPointPeriods.Count - 1].Elevation;
                    row["Elevation"] = Common.FormatDoubleValue(elevation, formatFixed, formatPrecision, "");

                    refPointPeriods.Reverse();
                    refPointHistories[refPoint.UniqueId] = refPointPeriods;

                    benchmarks.Rows.Add(row);
                }

                int numberOfGroups = (int)Math.Ceiling((double)benchmarks.Rows.Count / groupSizeLimit);

                FieldVisitDataByLocationServiceRequest fdRequest = new FieldVisitDataByLocationServiceRequest();
                fdRequest.LocationIdentifier = locationIdentifier;
                fdRequest.Activities = new List<ActivityType> { ActivityType.LevelSurvey };
                FieldVisitDataByLocationServiceResponse fdResponse = publish.Get(fdRequest);
                List<FieldVisit> fieldVisitData = fdResponse.FieldVisitData;

                try
                {
                    fieldVisitData.Sort((x, y) => y.StartTime.Value.CompareTo(x.StartTime.Value));
                }
                catch { }


                for (int i = 0; i < numberOfGroups; i++)
                {
                    DataRow groupByRow = dataSet.Tables["GroupBy"].NewRow();
                    groupByRow["GroupNumber"] = i;

                    for (int j = 0; j < groupSizeLimit; j++)
                    {
                        int index = (i * groupSizeLimit) + j;
                        if (index < refPoints.Count)
                        {
                            ReferencePoint refPoint = refPoints[index];
                            groupByRow["Name" + (j + 1).ToString()] = refPoint.Name;
                        }
                    }

                    dataSet.Tables["GroupBy"].Rows.Add(groupByRow);
                }

                foreach (FieldVisit fieldVisit in fieldVisitData)
                {
                    if (!ReportSpecificFunctions.TimeRangeOverlaps(fieldVisit.StartTime.Value, fieldVisit.StartTime.Value, reportPeriod))
                        continue;

                    string dateTime = fieldVisit.StartTime.Value.ToString("yyyy-MM-dd HH:mm");
                    string comments = fieldVisit.LevelSurveyActivity.Comments;
                    string party = fieldVisit.LevelSurveyActivity.Party;

                    int fieldVisitFirstRow = dataSet.Tables["BenchmarkHistory"].Rows.Count;

                    for (int i = 0; i < numberOfGroups; i++)
                    {
                        DataRow row = dataSet.Tables["BenchmarkHistory"].NewRow();
                        row["GroupNumber"] = i;
                        row["DateTime"] = dateTime;
                        row["Party"] = party;
                        row["Comments"] = comments;
                        dataSet.Tables["BenchmarkHistory"].Rows.Add(row);
                    }

                    List<LevelSurveyMeasurement> levelMeasurements = fieldVisit.LevelSurveyActivity.LevelMeasurements;

                    foreach (LevelSurveyMeasurement lsm in levelMeasurements)
                    {
                        if (!lsm.MeasuredElevation.Numeric.HasValue) continue;

                        Guid id = lsm.ReferencePointUniqueId;

                        if (!refPointNumbers.ContainsKey(id)) continue;

                        int number = refPointNumbers[id];
                        int theGroupNumber = (int)Math.Floor((double)number / groupSizeLimit);
                        int thePosition = (int)(number % groupSizeLimit);

                        double level = lsm.MeasuredElevation.Numeric.Value;
                        double correction = ReportSpecificFunctions.calculateDifference(lsm, refPointHistories[id]);

                        DataRow row = dataSet.Tables["BenchmarkHistory"].Rows[fieldVisitFirstRow + theGroupNumber];

                        row["Elevation" + (1 + thePosition).ToString()] = Common.FormatDoubleValue(level, formatFixed, formatPrecision, "");
                        row["Correction" + (1 + thePosition).ToString()] = Common.FormatDoubleValue(correction, formatFixed, formatPrecision, "");
                    }
                }

                List<DataRow> emptyRows = new List<DataRow>();
                for (int i = 0; i < dataSet.Tables["BenchmarkHistory"].Rows.Count; i++)
                {
                    DataRow row = dataSet.Tables["BenchmarkHistory"].Rows[i];

                    int emptyCount = 0;
                    for (int j = 0; j < groupSizeLimit; j++)
                    {
                        bool empty = string.IsNullOrEmpty(row["Elevation" + (j + 1).ToString()].ToString());
                        if (empty) emptyCount++;
                    }
                    if (emptyCount == groupSizeLimit) emptyRows.Add(row);
                }

                for (int i = 0; i < emptyRows.Count; i++)
                    dataSet.Tables["BenchmarkHistory"].Rows.Remove(emptyRows[i]);
            }
            catch (Exception exp)
            {
                Log.Error("Error creating report specific data tables ", exp);
                throw exp;
            }
        }
    }
}
