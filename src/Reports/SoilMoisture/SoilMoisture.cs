using ReportPluginFramework;

namespace Reports
{
    public class SoilMoisture : SoilMoistureNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            SoilMoistureNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
