using ReportPluginFramework;

namespace Reports
{
    public class RatingShiftAnalysis : RRTShiftReportNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RRTShiftReportNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
