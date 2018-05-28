using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;

namespace Reports
{
    public abstract class ReportPluginBase
    {
        public Assembly _Assembly = Assembly.GetExecutingAssembly();

        public void SetAssembly(Assembly assembly)
        {
            _Assembly = assembly;
        }
        public virtual DataSet GeneratePdfDataSet(RunReportRequest request)
        {
            Assembly.Load("Reports.Common");

            string location = System.IO.Path.GetDirectoryName(_Assembly.Location);
            string name = System.IO.Path.GetFileNameWithoutExtension(_Assembly.Location);

            return (new Reports.Common(request)).GetCommonDataSet(name, location);
        }

        public virtual DataSet GenerateCsvDataSet(RunReportRequest request)
        {
            return null;
        }
    }
}
