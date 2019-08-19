using ReportPluginFramework.Beta;

namespace Reports
{
    public class ProfileTable : ProfileTableNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            ProfileTableNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
