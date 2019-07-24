using ReportPluginFramework.Beta;

namespace Reports
{
    public class TimeSeriesPlot : TimeSeriesPlotNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            TimeSeriesPlotNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
