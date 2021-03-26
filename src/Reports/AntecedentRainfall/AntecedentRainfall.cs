using ReportPluginFramework;

namespace Reports
{
    public class AntecedentRainfall : AntecedentRainfallNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            AntecedentRainfallNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
