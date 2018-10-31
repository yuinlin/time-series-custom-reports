using ReportPluginFramework.Beta;

namespace Reports
{
    public class DailyMeanDischarge : DailyMeanDischargeNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMeanDischargeNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
