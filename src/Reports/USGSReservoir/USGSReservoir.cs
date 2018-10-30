using ReportPluginFramework.Beta;

namespace Reports
{
    public class USGSReservoir : USGSReservoirNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            USGSReservoirNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
