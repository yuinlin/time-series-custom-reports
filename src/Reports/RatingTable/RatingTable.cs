using ReportPluginFramework.Beta;

namespace Reports
{
    public class RatingTable : RatingTableNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            RatingTableNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
