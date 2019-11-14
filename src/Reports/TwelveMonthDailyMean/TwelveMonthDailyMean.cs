using ReportPluginFramework.Beta;

namespace Reports
{
    public class TwelveMonthDailyMean : TwelveMonthDailyMeanNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            TwelveMonthDailyMeanNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
