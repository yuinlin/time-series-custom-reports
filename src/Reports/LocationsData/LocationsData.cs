using ReportPluginFramework;

namespace Reports
{
    public class LocationsData : LocationsDataNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            LocationsDataNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
