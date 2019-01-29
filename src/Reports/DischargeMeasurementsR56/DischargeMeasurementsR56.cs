using ReportPluginFramework.Beta;

namespace Reports
{
    public class DischargeMeasurementsR56 : DischargeMeasurementsR56Namespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DischargeMeasurementsR56Namespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
