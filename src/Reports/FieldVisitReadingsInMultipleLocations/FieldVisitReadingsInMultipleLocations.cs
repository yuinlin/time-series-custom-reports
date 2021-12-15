using ReportPluginFramework;

namespace Reports
{
    public class FieldVisitReadingsInMultipleLocations : FieldVisitReadingsInMultipleLocationsNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            FieldVisitReadingsInMultipleLocationsNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
