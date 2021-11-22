using ReportPluginFramework;

namespace Reports
{
    public class DailyMeanFlow : DailyMeanFlowNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMeanFlowNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
