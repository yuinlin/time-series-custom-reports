using ReportPluginFramework.Beta;

namespace Reports
{
    public class RainfallSummary : RainfallSummaryNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RainfallSummaryNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
