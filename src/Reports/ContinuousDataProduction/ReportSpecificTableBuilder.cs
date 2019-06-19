using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.Beta.ReportData.TimeSeriesData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesDescription;
using System.Collections.Generic;

namespace ContinuousDataProductionNamespace
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
                settingsTable.Rows[0]["ReportTitle"] = "Continuous Data Production";

                DataTable table = new DataTable("ContinuousDataProductionDataTable");

                dataSet.Tables.Add(table);
                RunReportRequest runReportRequest = (RunReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                Guid timeSeriesUniqueId = (Guid)dataSet.Tables["SourceData"].Rows[0]["UniqueId"];

                string dllName = (string)dataSet.Tables["RunReportRequest"].Rows[0]["DllName"];
                Log.DebugFormat("{0} - Document GenerateScript input = {1}", dllName, timeSeriesUniqueId);

                ///////////////////////////////////////////////////////////////

                string timeseriesType = (string)dataSet.Tables["SourceData"].Rows[0]["TimeSeriesType"];

                if (timeseriesType != "ProcessorDerived") throw (new Exception("Input is not a derived time-series. Input signal must be a rating curve derived time-series."));

                Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest upchain = new Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest();
                upchain.TimeSeriesUniqueId = timeSeriesUniqueId;
                List<Server.Services.PublishService.ServiceModel.Dtos.Processor> processors = publish.Get(upchain).Processors;

                string ratingModelIdentifier = "";

                foreach (Server.Services.PublishService.ServiceModel.Dtos.Processor processor in processors)
                {
                    if (string.IsNullOrEmpty(processor.InputRatingModelIdentifier))
                        continue;

                    if (processor.InputTimeSeriesUniqueIds.Count == 0)
                        continue;

                    ratingModelIdentifier = processor.InputRatingModelIdentifier;
                    break;
                }

                if (string.IsNullOrEmpty(ratingModelIdentifier)) throw (new Exception("Input is not a rating model derived time-series. Input time-series must be a derived from a rating model."));

                double tableIncrement = common.GetParameterDouble("TableIncrement", 0.1);
                int httpRepeatCallLimit = common.GetParameterInt("HttpRepeatCallLimit", 4);
                int httpCallLimitSize = common.GetParameterInt("HttpCallLimitSize", 2000);
                int httpCallTimeoutInSeconds = common.GetParameterInt("HttpCallTimeoutInSeconds", 30);              

                TimeSpan timezone = TimeSpan.FromHours(common.GetTimeSeriesDescription(timeSeriesUniqueId).UtcOffset);
                DateTimeOffsetInterval selectedInterval = (DateTimeOffsetInterval)dataSet.Tables["ReportData"].Rows[0]["SelectedInterval"];

                DateTimeOffset ReportTime = DateTimeOffset.Now;

                if (selectedInterval.End.HasValue)
                    ReportTime = selectedInterval.End.Value.ToOffset(timezone);
                else
                    ReportTime = DateTimeOffset.Now.ToOffset(timezone);

                string locationIdentifier = common.GetTimeSeriesDescription(timeSeriesUniqueId).LocationIdentifier;

                int numberOfHistoricalPoints = ReportSpecificFunctions.GetNumberOfHistoricalPointsAvailable(
                    locationIdentifier, httpRepeatCallLimit, httpCallTimeoutInSeconds);

                settingsTable.Columns.Add("NumberOfHistoricalPoints", typeof(int));
                settingsTable.Rows[0]["NumberOfHistoricalPoints"] = numberOfHistoricalPoints;
              
            }
            catch (Exception exp)
            {
                Log.Error("Error creating report specific data tables ", exp);
                throw exp;
            }
        }
    }
}

