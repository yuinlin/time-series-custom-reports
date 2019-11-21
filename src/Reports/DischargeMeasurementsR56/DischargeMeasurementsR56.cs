using ReportPluginFramework;

namespace Reports
{
    public class DischargeMeasurementsR56 : DischargeMeasurementsR56Namespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DischargeMeasurementsR56Namespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
