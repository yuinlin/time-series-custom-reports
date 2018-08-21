using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;

namespace Reports
{
    public abstract class ReportPluginBase
    {
        public Assembly _Assembly = Assembly.GetExecutingAssembly();

        public virtual DataSet GeneratePdfDataSet(RunReportRequest request)
        {
            Assembly.Load("PerpetuumSoft.Reporting.MSChart");

            string location = System.IO.Path.GetDirectoryName(_Assembly.Location);
            string name = System.IO.Path.GetFileNameWithoutExtension(_Assembly.Location);

            System.Data.DataSet dataSet = (new Common(request)).GetCommonDataSet(name, location);

            AddReportSpecificTables(dataSet);

            return dataSet;
        }

        public virtual DataSet GenerateCsvDataSet(RunReportRequest request)
        {
            return null;
        }

        public virtual void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
        }
    }
}
