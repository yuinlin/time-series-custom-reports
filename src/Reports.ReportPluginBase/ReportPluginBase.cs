using System.Data;
using System.Reflection;
using ReportPluginFramework;

namespace Reports
{
    public abstract class ReportPluginBase
    {
        public virtual DataSet GeneratePdfDataSet(RunReportRequest request)
        {
            Assembly.Load("Reports.Common");

            return (new Reports.Common(request)).GetCommonDataSet();
        }

        public virtual DataSet GenerateCsvDataSet(RunReportRequest request)
        {
            return null;
        }
    }
}
