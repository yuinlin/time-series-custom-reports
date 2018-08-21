using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;
using ReportPluginFramework.Beta.ReportData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.Beta.ReportData.TimeSeriesData;
using ReportPluginFramework.Beta.ReportData.TimeSeriesDescription;
using ReportPluginFramework.Beta.ReportData.LocationDescription;
using ReportPluginFramework.Beta.ReportData.LocationData;
using System.Collections.Generic;

namespace RainfallMonthlyTotalsNamespace
{
    public class ReportSpecificTablesBuilder
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
