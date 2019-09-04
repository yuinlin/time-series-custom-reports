using ReportPluginFramework.Beta;

namespace Reports
{
    public class DiscreteData : DiscreteDataNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DiscreteDataNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
