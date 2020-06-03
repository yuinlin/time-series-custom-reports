using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using ReportPluginFramework.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.ReportData.TimeSeriesData;
using ReportPluginFramework.ReportData.TimeSeriesDescription;
using System.Collections.Generic;

namespace DirectDischargeSiteNamespace
{
    public class ReportSpecificTableBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddReportSpecificTables(DataSet dataSet)
        {
            try
            {
                /*
                dataSet.Tables["DischargeTimeSeriesFromRatingCurve"].TableName = "SourceData";
                dataSet.Tables["DischargeTimeSeriesFromRatingCurveLocation"].TableName = "SourceDataLocation";
                dataSet.Tables["DischargeTimeSeriesFromRatingCurveLocationExtendedAttributes"].TableName = "SourceDataLocationExtendedAttributes";

                RunFileReportRequest runReportRequest = (RunFileReportRequest)dataSet.Tables["RunReportRequest"].Rows[0]["RunReportRequest"];
                IPublishGateway publish = runReportRequest.Publish;
                Guid TimeSeriesUniqueId = (Guid)dataSet.Tables["SourceData"].Rows[0]["UniqueId"];
                string timeseriesType = (string)dataSet.Tables["SourceData"].Rows[0]["TimeSeriesType"];

                if (timeseriesType != "ProcessorDerived") throw (new Exception("Input is not a derived time-series. Input signal must be a rating model derived time-series."));

                Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest upchain = new Server.Services.PublishService.ServiceModel.RequestDtos.UpchainProcessorListByTimeSeriesServiceRequest();
                upchain.TimeSeriesUniqueId = TimeSeriesUniqueId;
                List<Server.Services.PublishService.ServiceModel.Dtos.Processor> processors = publish.Get(upchain).Processors;

                string procType = "";
                foreach (Server.Services.PublishService.ServiceModel.Dtos.Processor processor in processors)
                {
                    procType = processor.ProcessorType;

                    Log.InfoFormat("ProcessorType = '{0}'", procType);
                    if (procType == "ratingmodel")
                        break;
                    else continue;
                }
                if (procType != "ratingmodel")
                    throw (new Exception("Input is not a rating model derived time-series. Input signal must be a rating model derived time-series."));
*/
                Common common = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];

                DataTable settingsTable = dataSet.Tables["ReportSettings"];
                settingsTable.Columns.Add("ReportTitle", typeof(string));
                settingsTable.Rows[0]["ReportTitle"] = "Direct Discharge Site";

                DataTable table = new DataTable("DirectDischargeSiteDataTable");

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
