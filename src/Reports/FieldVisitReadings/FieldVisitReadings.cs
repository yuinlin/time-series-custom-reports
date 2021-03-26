using ReportPluginFramework;

namespace Reports
{
    public class FieldVisitReadings : FieldVisitReadingsNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            FieldVisitReadingsNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
