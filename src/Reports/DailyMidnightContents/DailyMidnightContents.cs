using ReportPluginFramework.Beta;

namespace Reports
{
    public class DailyMidnightContents : DailyMidnightContentsNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMidnightContentsNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
