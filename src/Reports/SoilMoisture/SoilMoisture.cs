using ReportPluginFramework.Beta;

namespace Reports
{
    public class SoilMoisture : SoilMoistureNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            SoilMoistureNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
