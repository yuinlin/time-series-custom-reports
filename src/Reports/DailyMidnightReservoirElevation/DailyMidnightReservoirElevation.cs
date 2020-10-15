using ReportPluginFramework;

namespace Reports
{
    public class DailyMidnightReservoirElevation : DailyMidnightReservoirElevationNamespace.ReportPluginBase, IFileReport
    {
        public override void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
            DailyMidnightReservoirElevationNamespace.ReportSpecificTableBuilder.AddReportSpecificTables(dataSet);
        }
    }
}
