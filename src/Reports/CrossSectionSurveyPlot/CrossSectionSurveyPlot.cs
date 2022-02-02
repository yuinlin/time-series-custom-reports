using ReportPluginFramework;

namespace Reports
{
    public class CrossSectionSurveyPlot : CrossSectionSurveyPlotNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            CrossSectionSurveyPlotNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
