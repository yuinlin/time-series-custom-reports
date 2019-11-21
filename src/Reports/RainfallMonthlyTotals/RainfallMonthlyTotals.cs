using ReportPluginFramework;

namespace Reports
{
    public class RainfallMonthlyTotals : RainfallMonthlyTotalsNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RainfallMonthlyTotalsNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
