using ReportPluginFramework.Beta;

namespace Reports
{
    public class Instrumentation : InstrumentationNamespace.ReportPluginBase, IReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            InstrumentationNamespace.ReportSpecificTablesBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
