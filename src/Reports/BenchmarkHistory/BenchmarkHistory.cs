using ReportPluginFramework;

namespace Reports
{
    public class BenchmarkHistory : BenchmarkHistoryNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            BenchmarkHistoryNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
