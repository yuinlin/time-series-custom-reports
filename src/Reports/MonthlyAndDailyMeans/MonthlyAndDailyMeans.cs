using ReportPluginFramework;

namespace Reports
{
    public class MonthlyAndDailyMeans : MonthlyAndDailyMeansNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            MonthlyAndDailyMeansNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
