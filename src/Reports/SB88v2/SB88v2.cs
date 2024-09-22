using ReportPluginFramework;

namespace Reports
{
    public class SB88v2 : SB88v2Namespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            SB88v2Namespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
