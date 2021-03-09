using ReportPluginFramework;

namespace Reports
{
    public class CurrentConditionsPlot : CurrentConditionsPlotNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            CurrentConditionsPlotNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
