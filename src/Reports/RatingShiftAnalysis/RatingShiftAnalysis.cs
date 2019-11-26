using ReportPluginFramework;

namespace Reports
{
    public class RatingShiftAnalysis : RatingShiftAnalysisNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RatingShiftAnalysisNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
