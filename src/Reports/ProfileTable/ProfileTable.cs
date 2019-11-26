using ReportPluginFramework;

namespace Reports
{
    public class ProfileTable : ProfileTableNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            ProfileTableNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
