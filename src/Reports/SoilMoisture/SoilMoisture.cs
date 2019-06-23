using ReportPluginFramework.Beta;

namespace Reports
{
    public class SoilMoisture : SoilMoistureNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            SoilMoistureNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
