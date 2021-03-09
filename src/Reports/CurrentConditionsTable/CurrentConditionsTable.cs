using ReportPluginFramework;

namespace Reports
{
    public class CurrentConditionsTable : CurrentConditionsTableNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            CurrentConditionsTableNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
