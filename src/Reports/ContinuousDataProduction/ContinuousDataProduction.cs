using ReportPluginFramework.Beta;

namespace Reports
{
    public class ContinuousDataProduction : ContinuousDataProductionNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            ContinuousDataProductionNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
