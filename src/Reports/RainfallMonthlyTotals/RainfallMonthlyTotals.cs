using ReportPluginFramework.Beta;

namespace Reports
{
    public class RainfallMonthlyTotals : RainfallMonthlyTotalsNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RainfallMonthlyTotalsNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
