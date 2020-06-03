using ReportPluginFramework;

namespace Reports
{
    public class DirectDischargeSite : DirectDischargeSiteNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DirectDischargeSiteNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
