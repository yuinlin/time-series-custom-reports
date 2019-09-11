using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Properties;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.Enum;
using System.Collections.Generic;

namespace FieldActivityNamespace
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
                settingsTable.Rows[0]["ReportTitle"] = Resources.FieldActivity;

                DataTable table = new DataTable("FieldActivityDataTable");

                dataSet.Tables.Add(table);

                RunFileReportRequest runReportRequest = (RunFileReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                string locationIdentifier = (string)dataSet.Tables["LocationInput"].Rows[0]["Identifier"];

                string dllName = (string)dataSet.Tables["RunReportRequest"].Rows[0]["DllName"];
                Log.DebugFormat("{0} - Document GenerateScript input location = {1}", dllName, locationIdentifier);

                ///////////////////////////////////////////////////////////////
                DateTimeOffsetInterval reportPeriod = (DateTimeOffsetInterval)dataSet.Tables["ReportPeriods"].Rows[0]["NoGroupBy"];

                string sortOrder = common.GetParameterString("SortOrder", "ReverseChronologicalOnMeasurementTime");

                DataTable table1 = dataSet.Tables.Add("FieldActivity");

                table1.Columns.Add("Date", typeof(string));
                table1.Columns.Add("Time", typeof(string));
                table1.Columns.Add("Timezone", typeof(string));
                table1.Columns.Add("ActivityType", typeof(string));
                table1.Columns.Add("Stage", typeof(string));
                table1.Columns.Add("StageUnit", typeof(string));
                table1.Columns.Add("Discharge", typeof(string));
                table1.Columns.Add("DischargeUnit", typeof(string));
                table1.Columns.Add("ControlCondition", typeof(string));
                table1.Columns.Add("ActivityRemarks", typeof(string));
                table1.Columns.Add("FieldVisitComments", typeof(string));

                FieldVisitDataByLocationServiceRequest fdRequest = new FieldVisitDataByLocationServiceRequest();
                fdRequest.LocationIdentifier = locationIdentifier;
                fdRequest.ApplyRounding = true;
                fdRequest.Activities = new List<ActivityType> { ActivityType.Reading, ActivityType.DischargeSummary, ActivityType.ControlCondition };
                FieldVisitDataByLocationServiceResponse fdResponse = publish.Get(fdRequest);
                List<FieldVisit> fieldVisitData = fdResponse.FieldVisitData;

                try
                {
                    if (sortOrder != "ReverseChronologicalOnMeasurementTime")
                        fieldVisitData.Sort((x, y) => x.StartTime.Value.CompareTo(y.StartTime.Value));
                    else
                        fieldVisitData.Sort((x, y) => y.StartTime.Value.CompareTo(x.StartTime.Value));
                }
                catch { }

                foreach (FieldVisit fieldVisit in fieldVisitData)
                {
                    if (!ReportSpecificFunctions.TimeRangeOverlaps(fieldVisit.StartTime.Value, fieldVisit.StartTime.Value, reportPeriod))
                        continue;

                    if (fieldVisit.DischargeActivities != null)
                    {
                        var activities = fieldVisit.DischargeActivities;

                        if (sortOrder != "ReverseChronologicalOnMeasurementTime")
                            activities.Sort((x, y) => x.DischargeSummary.MeasurementTime.CompareTo(y.DischargeSummary.MeasurementTime));
                        else
                            activities.Sort((x, y) => y.DischargeSummary.MeasurementTime.CompareTo(x.DischargeSummary.MeasurementTime));

                        foreach (DischargeActivity dischargeActivity in activities)
                        {
                            if (dischargeActivity.DischargeSummary == null) continue;

                            DataRow row = table1.NewRow();
                            row["Date"] = (fieldVisit.StartTime.HasValue) ? fieldVisit.StartTime.Value.ToString("yyyy-MM-dd") : "";
                            row["ControlCondition"] = (fieldVisit.ControlConditionActivity != null) ? fieldVisit.ControlConditionActivity.ControlCondition.ToString() : "";
                            row["FieldVisitComments"] = fieldVisit.Remarks;
                            row["ActivityType"] = Resources.DischargeSummary;

                            DischargeSummary dischargeSummary = dischargeActivity.DischargeSummary;
                            row["Time"] = dischargeSummary.MeasurementTime.ToString("HH:mm");
                            row["Timezone"] = dischargeSummary.MeasurementTime.ToString("zzz");
                            string discharge = (dischargeSummary.Discharge != null) ? dischargeSummary.Discharge.Display : "";
                            string dischargeUnit = (dischargeSummary.Discharge != null) ? dischargeSummary.Discharge.Unit : "";
                            if (dischargeUnit != "m^3/s") discharge += " " + dischargeUnit;
                            row["Discharge"] = discharge;

                            string stage = (dischargeSummary.MeanGageHeight != null) ? dischargeSummary.MeanGageHeight.Display : "";
                            string stageUnit = (dischargeSummary.MeanGageHeight != null) ? dischargeSummary.MeanGageHeight.Unit : "";
                            if (!string.IsNullOrEmpty(stage) && (stageUnit != "m")) stage += " " + stageUnit;
                            row["Stage"] = stage;
                            row["ActivityRemarks"] = dischargeSummary.Comments;

                            table1.Rows.Add(row);

                        }
                    }
                    if ((fieldVisit.InspectionActivity != null) && (fieldVisit.InspectionActivity.Readings != null))
                    {
                        foreach (Reading reading in fieldVisit.InspectionActivity.Readings)
                        {
                            if (!reading.Publish || !reading.IsValid) continue;
                            if ("Stage" != reading.Parameter) continue;

                            DataRow row = table1.NewRow();
                            row["Date"] = (fieldVisit.StartTime.HasValue) ? fieldVisit.StartTime.Value.ToString("yyyy-MM-dd") : "";
                            row["ControlCondition"] = (fieldVisit.ControlConditionActivity != null) ? fieldVisit.ControlConditionActivity.ControlCondition.ToString() : "";
                            row["FieldVisitComments"] = fieldVisit.Remarks;
                            row["ActivityType"] = Resources.Reading;

                            row["Time"] = (reading.Time.HasValue) ? reading.Time.Value.ToString("HH:mm") : "";
                            row["Timezone"] = (reading.Time.HasValue) ? reading.Time.Value.ToString("zzz") : "";
                            string stage = reading.Value.Display;
                            string stageUnit = reading.Unit;
                            if (stageUnit != "m") stage += " " + stageUnit;
                            row["Stage"] = stage;
                            row["ActivityRemarks"] = reading.Comments;

                            table1.Rows.Add(row);
                        }
                    }
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
