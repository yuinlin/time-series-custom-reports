using ReportPluginFramework;

namespace Reports
{
    public class SB88 : SB88Namespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            SB88Namespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
