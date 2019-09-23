using ReportPluginFramework.Beta;

namespace Reports
{
    public class DailyMeanDischarge : DailyMeanDischargeNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMeanDischargeNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
