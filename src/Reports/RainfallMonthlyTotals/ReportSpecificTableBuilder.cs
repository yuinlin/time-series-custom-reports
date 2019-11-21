using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using ReportPluginFramework.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.ReportData.TimeSeriesData;
using ReportPluginFramework.ReportData.TimeSeriesDescription;
using System.Collections.Generic;

namespace RainfallMonthlyTotalsNamespace
{
    public class ReportSpecificTableBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddReportSpecificTables(DataSet dataSet)
        {
            try
            {
                Common common = (Common)dataSet.Tables["RunReportRequest"].Rows[0]["CommonLibrary"];

                DataTable settingsTable = dataSet.Tables["ReportSettings"];
                settingsTable.Columns.Add("ReportTitle", typeof(string));
                settingsTable.Rows[0]["ReportTitle"] = "Rainfall - Monthly Totals";

                DataTable table = new DataTable("RainfallMonthlyTotalsDataTable");

                dataSet.Tables.Add(table);
            }
            catch (Exception exp)
            {
                Log.Error("Error creating report specific data tables ", exp);
                throw exp;
            }
        }
    }
}
