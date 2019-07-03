using ReportPluginFramework.Beta;

namespace Reports
{
    public class USGSReservoir : USGSReservoirNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            USGSReservoirNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
