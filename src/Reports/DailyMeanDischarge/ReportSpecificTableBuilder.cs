using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.Beta.ReportData.TimeSeriesData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesDescription;
using ReportPluginFramework.Beta.ReportData.LocationDescription;
using ReportPluginFramework.Beta.ReportData.LocationData;
using System.Collections.Generic;

namespace DailyMeanDischargeNamespace
{
    public class ReportSpecificTablesBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddReportSpecificTables(DataSet dataSet)
        {
            try
            {
                RunReportRequest runReportRequest = (RunReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                Guid TimeSeriesUniqueId = (Guid)dataSet.Tables["SourceData"].Rows[0]["UniqueId"];
                string timeseriesType = (string)dataSet.Tables["SourceData"].Rows[0]["TimeSeriesType"];

                if (timeseriesType != "ProcessorDerived") throw (new Exception("Input is not a derived time-series. Input signal must be a daily mean derived time-series."));

                Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest upchain = new Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest();
                upchain.TimeSeriesUniqueId = TimeSeriesUniqueId;
                List<Server.Services.PublishService.ServiceModel.Dtos.Processor> processors = publish.Get(upchain).Processors;

                foreach (Server.Services.PublishService.ServiceModel.Dtos.Processor processor in processors)
                {
                    string procType = processor.ProcessorType;
                    if (procType != "statistics")
                    {
                        throw (new Exception("Input is not a statistics derived time-series. Input signal must be a daily mean derived time-series."));
                    }
                    else
                    {
                        string requireCoverage = null;
                        string timeStep = null;
                        string timeStepCount = null;
                        string statType = null;

                        string coverageAmountPercent = "";

                        Dictionary<string, string> settings = processor.Settings;
                        foreach (var item in settings)
                        {
                            string key = item.Key;
                            string val = item.Value;

                            if (key == "StatisticType") statType = item.Value;
                            if (key == "RequireMinimumCoverage") requireCoverage = item.Value;
                            if (key == "TimeStep") timeStep = item.Value;
                            if (key == "TimeStepCount") timeStepCount = item.Value;
                            if (key == "MinimumCoverageRequiredForPartialGradeAsPercent") coverageAmountPercent = item.Value;

                        }
                        if (("Daily" != timeStep) || ("1" != timeStepCount) || ("Mean" != statType))
                        {
                            throw (new Exception("Input is not a daily mean derived time-series. Input signal must be a daily mean derived time-series."));
                        }

                        ReportJobParameter coverageItem = null;
                        foreach (ReportJobParameter item in runReportRequest.Parameters)
                        {
                            if (item.Name == "DataCoverageThreshold")
                            {
                                coverageItem = item;
                                break;
                            }
                        }
                        if (coverageItem != null) runReportRequest.Parameters.Remove(coverageItem);

                        ReportJobParameter parm = new ReportJobParameter();
                        parm.Name = "DataCoverageThreshold";
                        parm.Value = ("true" == requireCoverage) ? coverageAmountPercent : "";
                        runReportRequest.Parameters.Add(parm);

                        break;
                    }
                }

                Common common = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];

                DataTable settingsTable = dataSet.Tables["ReportSettings"];
                settingsTable.Columns.Add("ReportTitle", typeof(string));
                settingsTable.Rows[0]["ReportTitle"] = "Daily Mean Discharge";

                DataTable table = new DataTable("DailyMeanDischargeDataTable");

                dataSet.Tables.Add(table);
            }
            catch (Exception exp)
            {
                Log.Error("Error creating report specific data tables ", exp);
                throw exp;
            }
        }
    }
}
