using ReportPluginFramework;

namespace Reports
{
    public class FieldActivity : FieldActivityNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            FieldActivityNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
