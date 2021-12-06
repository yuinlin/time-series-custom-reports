using ReportPluginFramework;

namespace Reports
{
    public class TimeSeriesCharts : TimeSeriesChartsNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            TimeSeriesChartsNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
