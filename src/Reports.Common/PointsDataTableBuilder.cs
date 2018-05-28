using System;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using ReportPluginFramework.Beta.ReportData.TimeSeriesData;

namespace Reports
{
    public class PointsDataTableBuilder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public PointsDataTableBuilder(Common common)
        {
            _Common = common;
        }

        public DataTable GetPointsTable(string tableName)
        {
            DataTable pointsTable = new DataTable(tableName);

            pointsTable.Columns.Add("NoGroupBy", typeof(int));
            pointsTable.Columns.Add("Year", typeof(int));
            pointsTable.Columns.Add("WaterYear", typeof(int));
            pointsTable.Columns.Add("Month", typeof(int));
            pointsTable.Columns.Add("Day", typeof(int));
            pointsTable.Columns.Add("Week", typeof(int));
            pointsTable.Columns.Add("TimeStamp", typeof(DateTimeOffset));
            pointsTable.Columns.Add("TimeStampDouble", typeof(double));
            pointsTable.Columns.Add("Value", typeof(double));
            pointsTable.Columns.Add("DisplayValue", typeof(string));

            return pointsTable;
        }

        public DataTable AddPointsToTable(DataTable groupBy, DataTable pointsTable, List<TimeSeriesPoint> points)
        {
            foreach (TimeSeriesPoint point in points)
            {
                DateTimeOffset dateTimeOffset = point.Timestamp;

                double pointValue = (point.Value.HasValue) ? pointValue = point.Value.Value : double.NaN;
                AddPointToTable(groupBy, pointsTable, dateTimeOffset, pointValue, pointValue.ToString());
            }

            RemoveUnneededGroupByRows(groupBy, pointsTable);

            return pointsTable;
        }

        public void RemoveUnneededGroupByRows(DataTable groupBy, DataTable pointsTable)
        {
            // after all points have been added, remove trailing group-by rows if contain no points, except leave at least one row

            foreach (DataRelation relation in groupBy.DataSet.Relations)
            {
                if ((relation.ParentTable == groupBy) && (relation.ChildTable == pointsTable))
                {
                    while (groupBy.Rows.Count > 1)
                    {
                        DataRow[] childRows = groupBy.Rows[groupBy.Rows.Count - 1].GetChildRows(relation);
                        if (childRows.Length == 0) groupBy.Rows.Remove(groupBy.Rows[groupBy.Rows.Count - 1]);
                        else break;
                    }
                }
            }
        }

        public void AddPointToTable(DataTable groupBy, DataTable pointsTable, DateTimeOffset timestamp, double value, string displayValue)
        {
            int weekNumber = -1;
            foreach (DataRow groupRow in groupBy.Rows)
            {
                DateTimeOffset startTime = (DateTimeOffset)groupRow["StartTime"];
                DateTimeOffset endTime = (DateTimeOffset)groupRow["EndTime"];

                if ((startTime <= timestamp) && (timestamp < endTime))
                {
                    weekNumber = (int)groupRow["Week"];
                    break;
                }
            }
            if (weekNumber == -1)
            {
                Log.DebugFormat("AddPointToTable - weekNumber is -1 which is invalid so SKIP, {0}, {1}", timestamp, value);
                return;
            }
            DataRow row = pointsTable.NewRow();
            row["NoGroupBy"] = 0;
            row["Year"] = timestamp.Year;
            row["WaterYear"] = (timestamp.Month >= _Common.GetWaterYearMonth()) ? timestamp.Year : timestamp.Year - 1;
            row["Month"] = timestamp.Month;
            row["Day"] = timestamp.Day;
            row["Week"] = weekNumber;
            row["TimeStamp"] = timestamp;
            row["TimeStampDouble"] = timestamp.DateTime.ToOADate();
            row["Value"] = value;
            row["DisplayValue"] = displayValue;

            pointsTable.Rows.Add(row);
        }
    }
}
