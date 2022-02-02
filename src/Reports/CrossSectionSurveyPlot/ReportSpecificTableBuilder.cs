using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework;
using ReportPluginFramework.Properties;
using ReportPluginFramework.ReportData;
using System.Collections.Generic;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.Enum;

namespace CrossSectionSurveyPlotNamespace
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
                settingsTable.Rows[0]["ReportTitle"] = "Cross-Section Survey Plot";

                RunFileReportRequest runReportRequest = (RunFileReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                Common CommonLibrary = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];
                DateTimeOffsetInterval periodSelected = (DateTimeOffsetInterval)dataSet.Tables["ReportData"].Rows[0]["PeriodSelectedAdjustedForReport"];

                DataTable crossSectionSurveyDataTable = new DataTable("CrossSectionSurveyPlotDataTable");
                dataSet.Tables.Add(crossSectionSurveyDataTable);

                string locIdentifier = (string)dataSet.Tables["InputLocation"].Rows[0]["LocationIdentifier"];
                TimeSpan utcOffset = (TimeSpan)dataSet.Tables["InputLocation"].Rows[0]["UtcOffset"];

                string relativeLocationFilter = CommonLibrary.GetParameterString("RelativeLocationFilterPartialName", "");
                string channelFilter = CommonLibrary.GetParameterString("ChannelFilterPartialName", "");
                string partyFilter = CommonLibrary.GetParameterString("PartyFilterPartialName", "");

                string filterInformationString = Resources.RelativeLocationFilter + ": " + relativeLocationFilter;
                filterInformationString += "        " + Resources.ChannelFilter + ": " + channelFilter;
                filterInformationString += "        " + Resources.PartyFilter + ": " + partyFilter;

                crossSectionSurveyDataTable.Rows.Add(crossSectionSurveyDataTable.NewRow());

                crossSectionSurveyDataTable.Columns.Add("FilterInformationString", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("RelativeLocationColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("ChannelColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("StartingPointColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("StartTimeColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("EndTimeColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("PartyColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("SurveyCommentColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("TotalPointsColumnHeader", typeof(string));
                crossSectionSurveyDataTable.Columns.Add("StageColumnHeader", typeof(string));

                crossSectionSurveyDataTable.Rows[0]["FilterInformationString"] = filterInformationString;

                string sortOrder = CommonLibrary.GetParameterString("SortOrder", "ReverseChronologicalOnStartTime");
                string dateFormat = CommonLibrary.GetParameterString("DateFormat", "yyyy-MM-dd HH:mm");

                string stageUnitSymbol = "";
                string stageUnitId = "";

                DataTable activityTable = dataSet.Tables.Add("CrossSectionSurveyActivity");
                activityTable.Columns.Add("ActivityNumber", typeof(int));
                activityTable.Columns.Add("RelativeLocation", typeof(string));
                activityTable.Columns.Add("Channel", typeof(string));
                activityTable.Columns.Add("StartingPoint", typeof(string));
                activityTable.Columns.Add("StartTime", typeof(string));
                activityTable.Columns.Add("EndTime", typeof(string));
                activityTable.Columns.Add("Party", typeof(string));
                activityTable.Columns.Add("SurveyComment", typeof(string));
                activityTable.Columns.Add("TotalPoints", typeof(string));
                activityTable.Columns.Add("Stage", typeof(string));
                activityTable.Columns.Add("DistanceUnitSymbol", typeof(string));
                activityTable.Columns.Add("ElevationUnitSymbol", typeof(string));
                activityTable.Columns.Add("YAxisLabel", typeof(string));
                activityTable.Columns.Add("XAxisLabel", typeof(string));

                DataTable pointsTable = dataSet.Tables.Add("CrossSectionSurveyPoints");
                pointsTable.Columns.Add("ActivityNumber", typeof(int));
                pointsTable.Columns.Add("PointOrder", typeof(string));
                pointsTable.Columns.Add("Distance", typeof(string));
                pointsTable.Columns.Add("Elevation", typeof(string));
                pointsTable.Columns.Add("DistanceUnit", typeof(string));
                pointsTable.Columns.Add("ElevationUnit", typeof(string));
                pointsTable.Columns.Add("DistanceDouble", typeof(double));
                pointsTable.Columns.Add("ElevationDouble", typeof(double));
                pointsTable.Columns.Add("PointComment", typeof(string));

                dataSet.Relations.Add("ActivityPoints", activityTable.Columns["ActivityNumber"], pointsTable.Columns["ActivityNumber"]);

                FieldVisitDataByLocationServiceRequest request = new FieldVisitDataByLocationServiceRequest();
                request.LocationIdentifier = locIdentifier;
                request.ApplyRounding = true;
                request.Activities = new List<ActivityType> { ActivityType.CrossSectionSurvey };
                request.IncludeCrossSectionSurveyProfile = true;
                request.IncludeInvalidActivities = false;

                FieldVisitDataByLocationServiceResponse response = publish.Get(request);
                List<FieldVisit> visits = response.FieldVisitData;

                if (sortOrder != "ReverseChronologicalOnStartTime")
                    visits.Sort((x, y) => x.StartTime.Value.CompareTo(y.StartTime.Value));
                else
                    visits.Sort((x, y) => y.StartTime.Value.CompareTo(x.StartTime.Value));

                int activityNumber = 0;
                foreach (FieldVisit visit in visits)
                {
                    if (visit.CrossSectionSurveyActivity == null) continue;

                    List<CrossSectionSurveyActivity> activities = visit.CrossSectionSurveyActivity;
                    if (sortOrder != "ReverseChronologicalOnStartTime")
                        activities.Sort((x, y) => {
                            int ret = x.StartTime.CompareTo(y.StartTime);
                            return (ret != 0) ? ret : string.Compare(x.RelativeLocation, y.RelativeLocation);
                        });
                    else
                        activities.Sort((x, y) => {
                            int ret = y.StartTime.CompareTo(x.StartTime);
                            return (ret != 0) ? ret : string.Compare(x.RelativeLocation, y.RelativeLocation);
                        });

                    foreach (CrossSectionSurveyActivity activity in activities)
                    {
                        if (periodSelected.Start.HasValue && (activity.StartTime < periodSelected.Start.Value)) continue;
                        if (periodSelected.End.HasValue && (activity.StartTime > periodSelected.End.Value)) continue;

                        if (!Common.MatchPartialNameFilter(relativeLocationFilter, activity.RelativeLocation)) continue;
                        if (!Common.MatchPartialNameFilter(channelFilter, activity.Channel)) continue;
                        if (!Common.MatchPartialNameFilter(partyFilter, activity.Party)) continue;

                        List<CrossSectionPoint> points = activity.CrossSectionPoints;

                        string distanceUnitId = "";
                        string elevationUnitId = "";

                        activityNumber++;

                        DataRow activitytRow = activityTable.NewRow();
                        activitytRow["ActivityNumber"] = activityNumber;
                        activitytRow["RelativeLocation"] = activity.RelativeLocation;
                        activitytRow["Channel"] = activity.Channel;
                        activitytRow["StartingPoint"] = activity.StartingPoint.ToString();
                        activitytRow["StartTime"] = activity.StartTime.ToOffset(utcOffset).ToString(dateFormat);
                        activitytRow["EndTime"] = activity.EndTime.ToOffset(utcOffset).ToString(dateFormat);
                        activitytRow["Party"] = activity.Party;
                        activitytRow["SurveyComment"] = activity.Comments;
                        activitytRow["TotalPoints"] = (points == null)? "" : points.Count.ToString();

                        if (string.IsNullOrEmpty(stageUnitId)) stageUnitId = activity.Stage.Unit;
                        string stageUnit = (stageUnitId == activity.Stage.Unit) ? "" : " " + CommonLibrary.GetUnitSymbol(activity.Stage.Unit);
                        activitytRow["Stage"] = activity.Stage.Display + stageUnit;

                        activityTable.Rows.Add(activitytRow);
                        if (points == null) continue;

                        points.Sort((x, y) => x.PointOrder.CompareTo(y.PointOrder));

                        for (int i = 0; i < points.Count; i++)
                        {
                            CrossSectionPoint point = points[i];

                            if (string.IsNullOrEmpty(distanceUnitId)) distanceUnitId = point.Distance.Unit;
                            if (string.IsNullOrEmpty(elevationUnitId)) elevationUnitId = point.Elevation.Unit;

                            string distanceUnit = (distanceUnitId == point.Distance.Unit) ? "" : " " + CommonLibrary.GetUnitSymbol(point.Distance.Unit);
                            string elevationUnit = (elevationUnitId == point.Elevation.Unit) ? "" : " " + CommonLibrary.GetUnitSymbol(point.Elevation.Unit);

                            DataRow row = pointsTable.NewRow();

                            row["ActivityNumber"] = activityNumber;
                            row["PointOrder"] = point.PointOrder.ToString();
                            row["Distance"] = point.Distance.Display + distanceUnit;
                            row["Elevation"] = point.Elevation.Display + elevationUnit;
                            row["DistanceUnit"] = distanceUnit;
                            row["ElevationUnit"] = elevationUnit;
                            row["DistanceDouble"] = (point.Distance.Numeric.HasValue)? point.Distance.Numeric.Value : double.NaN;
                            row["ElevationDouble"] = (point.Elevation.Numeric.HasValue)? + point.Elevation.Numeric.Value : double.NaN;
                            row["PointComment"] = point.Comments;

                            pointsTable.Rows.Add(row);
                        }

                        string distanceUnitSymbol = CommonLibrary.GetUnitSymbol(distanceUnitId);
                        string elevationUnitSymbol = CommonLibrary.GetUnitSymbol(elevationUnitId);

                        activitytRow["DistanceUnitSymbol"] = distanceUnitSymbol;
                        activitytRow["ElevationUnitSymbol"] = elevationUnitSymbol;
                        activitytRow["YAxisLabel"] = Resources.Elevation + " (" + elevationUnitSymbol + ")";
                        activitytRow["XAxisLabel"] = Resources.Distance + " (" + distanceUnitSymbol + ")";
                    }

                    stageUnitSymbol = CommonLibrary.GetUnitSymbol(stageUnitId);

                    crossSectionSurveyDataTable.Rows[0]["RelativeLocationColumnHeader"] = Resources.RelativeLocation;
                    crossSectionSurveyDataTable.Rows[0]["ChannelColumnHeader"] = Resources.Channel;
                    crossSectionSurveyDataTable.Rows[0]["StartingPointColumnHeader"] = Resources.StartingPoint;
                    crossSectionSurveyDataTable.Rows[0]["StartTimeColumnHeader"] = Resources.StartTime;
                    crossSectionSurveyDataTable.Rows[0]["EndTimeColumnHeader"] = Resources.EndTime;
                    crossSectionSurveyDataTable.Rows[0]["PartyColumnHeader"] = Resources.Party;
                    crossSectionSurveyDataTable.Rows[0]["SurveyCommentColumnHeader"] = Resources.CrossSectionSurveyComment;
                    crossSectionSurveyDataTable.Rows[0]["TotalPointsColumnHeader"] = Resources.TotalPoints;
                    crossSectionSurveyDataTable.Rows[0]["StageColumnHeader"] = Resources.Stage + " (" + stageUnitSymbol + ")";
                }
            }
            catch (Exception exp)
            {
                Log.Error("Error creating report specific data tables ", exp);
                throw exp;
            }
        }
    }
}
