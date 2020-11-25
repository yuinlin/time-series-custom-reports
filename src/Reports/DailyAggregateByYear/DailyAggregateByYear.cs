using ReportPluginFramework;

namespace Reports
{
    public class DailyAggregateByYear : DailyAggregateByYearNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyAggregateByYearNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
