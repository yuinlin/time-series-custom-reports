using ReportPluginFramework;

namespace Reports
{
    public class GroundwaterLevelStatisticsChart : GroundwaterLevelStatisticsChartNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            GroundwaterLevelStatisticsChartNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
