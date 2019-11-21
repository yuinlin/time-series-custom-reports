using ReportPluginFramework;

namespace Reports
{
    public class MeasurementList : MeasurementListNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            MeasurementListNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
