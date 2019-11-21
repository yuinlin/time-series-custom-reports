using ReportPluginFramework;

namespace Reports
{
    public class USGSDailyMean : USGSDailyMeanNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            USGSDailyMeanNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
