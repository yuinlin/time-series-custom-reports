using ReportPluginFramework.Beta;

namespace Reports
{
    public class ContinuousDataProduction : ContinuousDataProductionNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            ContinuousDataProductionNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
