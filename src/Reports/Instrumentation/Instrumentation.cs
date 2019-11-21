using ReportPluginFramework;

namespace Reports
{
    public class Instrumentation : InstrumentationNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            InstrumentationNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
