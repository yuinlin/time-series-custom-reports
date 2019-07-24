using ReportPluginFramework.Beta;

namespace Reports
{
    public class MeasurementList : MeasurementListNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            MeasurementListNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
