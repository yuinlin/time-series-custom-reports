using ReportPluginFramework;

namespace Reports
{
    public class AutomatedSnowWeatherStationGraph : AutomatedSnowWeatherStationGraphNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            AutomatedSnowWeatherStationGraphNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
