using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using System.Collections.Generic;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.Enum;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit.ActiveMeters;

namespace InstrumentationNamespace
{
    public class ReportSpecificTablesBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddReportSpecificTables(DataSet dataSet)
        {
            try
            {
                Common common = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];

                DataTable settingsTable = dataSet.Tables["ReportSettings"];
                settingsTable.Columns.Add("ReportTitle", typeof(string));
                settingsTable.Rows[0]["ReportTitle"] = "Instrumentation";

                DataTable table = new DataTable("InstrumentationDataTable");

                dataSet.Tables.Add(table);

                RunReportRequest runReportRequest = (RunReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                string locationIdentifier = (string)dataSet.Tables["LocationInput"].Rows[0]["Identifier"];

                string dllName = (string)dataSet.Tables["RunReportRequest"].Rows[0]["DllName"];
                Log.DebugFormat("{0} - Document GenerateScript input location = {1}", dllName, locationIdentifier);

                ///////////////////////////////////////////////////////////////
                DateTimeOffsetInterval reportPeriod = (DateTimeOffsetInterval)dataSet.Tables["ReportPeriods"].Rows[0]["NoGroupBy"];

                string sortOrder = common.GetParameterString("SortOrder", "Reverse Chronological on Measurement Time");

                DataTable table1 = dataSet.Tables.Add("Instrumentation");

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
                table1.Columns.Add("Method", typeof(string));
                table1.Columns.Add("InstrumentType", typeof(string));
                table1.Columns.Add("Software", typeof(string));
                table1.Columns.Add("Firmware", typeof(string));
                table1.Columns.Add("SerialNumber", typeof(string));
                table1.Columns.Add("Manufacturer", typeof(string));
                table1.Columns.Add("Model", typeof(string));

                ActiveMetersAndCalibrationsServiceRequest amcRequest = new ActiveMetersAndCalibrationsServiceRequest();
                ActiveMetersAndCalibrationsServiceResponse amcResponse = publish.Get(amcRequest);
                List<ActiveMeterDetails> activeMeterDetailsList = amcResponse.ActiveMeterDetails;

                FieldVisitDataByLocationServiceRequest fdRequest = new FieldVisitDataByLocationServiceRequest();
                fdRequest.LocationIdentifier = locationIdentifier;
                fdRequest.Activities = new List<ActivityType> { ActivityType.Reading, ActivityType.DischargeSummary, ActivityType.DischargePointVelocity, ActivityType.DischargeAdcp };
                fdRequest.ApplyRounding = true;
                fdRequest.IncludeVerticals = true;
                FieldVisitDataByLocationServiceResponse fdResponse = publish.Get(fdRequest);
                List<FieldVisit> fieldVisitData = fdResponse.FieldVisitData;

                try
                {
                    if (sortOrder != "Reverse Chronological on Measurement Time")
                        fieldVisitData.Sort((x, y) => x.StartTime.Value.CompareTo(y.StartTime.Value));
                    else
                        fieldVisitData.Sort((x, y) => y.StartTime.Value.CompareTo(x.StartTime.Value));
                }
                catch { }

                foreach (FieldVisit fieldVisit in fieldVisitData)
                {
                    if (!ReportSpecificFunctions.TimeRangeOverlaps(fieldVisit.StartTime.Value, fieldVisit.StartTime.Value, reportPeriod))
                        continue;

                    string controlCondition = (fieldVisit.ControlConditionActivity != null) ? fieldVisit.ControlConditionActivity.ControlCondition.ToString() : "";

                    var activities = fieldVisit.DischargeActivities;

                    if (activities != null)
                    {
                        if (sortOrder != "Reverse Chronological on Measurement Time")
                            activities.Sort((x, y) => x.DischargeSummary.MeasurementTime.CompareTo(y.DischargeSummary.MeasurementTime));
                        else
                            activities.Sort((x, y) => y.DischargeSummary.MeasurementTime.CompareTo(x.DischargeSummary.MeasurementTime));

                        foreach (DischargeActivity dischargeActivity in activities)
                        {
                            DataRow row = table1.NewRow();
                            row["Date"] = (fieldVisit.StartTime.HasValue) ? fieldVisit.StartTime.Value.ToString("yyyy-MM-dd") : "";
                            row["ControlCondition"] = controlCondition;
                            row["FieldVisitComments"] = fieldVisit.Remarks;
                            row["ActivityType"] = ActivityType.DischargeSummary.ToString();
                            row["Method"] = dischargeActivity.DischargeSummary.DischargeMethod;

                            DischargeSummary dischargeSummary = dischargeActivity.DischargeSummary;
                            row["Time"] = dischargeSummary.MeasurementTime.ToString("HH:mm");
                            row["Timezone"] = dischargeSummary.MeasurementTime.ToString("zzz");
                            string discharge = (dischargeSummary.Discharge != null) ? dischargeSummary.Discharge.Numeric.ToString() : "";
                            string dischargeUnit = (dischargeSummary.Discharge != null) ? dischargeSummary.Discharge.Unit : "";
                            if (dischargeUnit != "m^3/s") discharge += " " + dischargeUnit;
                            row["Discharge"] = discharge;

                            string stage = (dischargeSummary.MeanGageHeight != null) ? dischargeSummary.MeanGageHeight.Numeric.ToString() : "";
                            string stageUnit = (dischargeSummary.MeanGageHeight != null) ? dischargeSummary.MeanGageHeight.Unit : "";
                            if (!string.IsNullOrEmpty(stage) && (stageUnit != "m")) stage += " " + stageUnit;
                            row["Stage"] = stage;
                            row["ActivityRemarks"] = dischargeSummary.Comments;

                            string serialNumber = "";
                            string model = "";
                            string manufacturer = "";
                            string instrumentType = "";
                            string software = "";
                            string firmware = "";

                            if ((dischargeActivity.PointVelocityDischargeActivities != null) && (dischargeActivity.PointVelocityDischargeActivities.Count > 0))
                            {
                                PointVelocityDischargeActivity pointVelocityDischargeActivity = dischargeActivity.PointVelocityDischargeActivities[0];
                                if ((pointVelocityDischargeActivity.Verticals != null) && (pointVelocityDischargeActivity.Verticals.Count > 0))
                                {
                                    Vertical vertical = pointVelocityDischargeActivity.Verticals[0];
                                    serialNumber = vertical.CurrentMeter.SerialNumber;
                                    model = vertical.CurrentMeter.Model;
                                    manufacturer = vertical.CurrentMeter.Manufacturer;

                                    foreach (ActiveMeterDetails activeMeterDetails in activeMeterDetailsList)
                                    {
                                        if ((activeMeterDetails.Manufacturer == manufacturer) &&
                                         (activeMeterDetails.SerialNumber == serialNumber) &&
                                         (activeMeterDetails.Model == model))
                                        {
                                            instrumentType = (activeMeterDetails.MeterType.HasValue) ? activeMeterDetails.MeterType.Value.ToString() : "";
                                            software = activeMeterDetails.SoftwareVersion;
                                            firmware = activeMeterDetails.FirmwareVersion;

                                            break;
                                        }
                                    }
                                }
                            }

                            if ((dischargeActivity.AdcpDischargeActivities != null) && (dischargeActivity.AdcpDischargeActivities.Count > 0))
                            {
                                AdcpDischargeActivity adcpDischargeActivity = dischargeActivity.AdcpDischargeActivities[0];

                                instrumentType = adcpDischargeActivity.AdcpDeviceType;
                                software = adcpDischargeActivity.SoftwareVersion;
                                firmware = adcpDischargeActivity.FirmwareVersion;
                                manufacturer = adcpDischargeActivity.Manufacturer;
                                serialNumber = adcpDischargeActivity.SerialNumber;
                                model = adcpDischargeActivity.Model;

                                Log.DebugFormat("AdcpDischargeActivity: {0}, {1}, {2}", instrumentType, software, firmware);
                            }

                            row["Software"] = software;
                            row["InstrumentType"] = instrumentType;
                            row["Firmware"] = firmware;
                            row["Manufacturer"] = manufacturer;
                            row["Model"] = model;
                            row["SerialNumber"] = serialNumber;
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
