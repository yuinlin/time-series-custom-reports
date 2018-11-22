using ReportPluginFramework.Beta;

namespace Reports
{
    public class FieldActivity : FieldActivityNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            FieldActivityNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
