using ReportPluginFramework;

namespace Reports
{
    public class AnnualMaximumAndMinimum : AnnualMaximumAndMinimumNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            AnnualMaximumAndMinimumNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
