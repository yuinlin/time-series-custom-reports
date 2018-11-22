using ReportPluginFramework.Beta;

namespace Reports
{
    public class BenchmarkHistory : BenchmarkHistoryNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            BenchmarkHistoryNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
