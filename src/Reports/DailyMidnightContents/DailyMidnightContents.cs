using ReportPluginFramework.Beta;

namespace Reports
{
    public class DailyMidnightContents : DailyMidnightContentsNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMidnightContentsNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
