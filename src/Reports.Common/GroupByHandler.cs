using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ReportPluginFramework.Beta;
using System.Data;

namespace Reports
{
    public class GroupByHandler
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _WaterYearMonth = 10;
        private Common _Common;

        public GroupByHandler(Common common)
        {
            _Common = common;
            _WaterYearMonth = common.GetWaterYearMonth();
        }

        public DateTimeOffsetInterval AdjustIntervalToGroupBy(DateTimeOffsetInterval SelectedPeriod, DateTimeOffsetInterval TimeSeriesRange, string GroupBy, TimeSpan timeseriesOffset)
        {
            Log.DebugFormat("AdjustIntervalToGroupBy selected interval = {0}, timeseries range = {1}, groupBy = {2}, tsOffset = {3}",
                _Common.TimeRangeString(SelectedPeriod), _Common.TimeSeriesRangeString(TimeSeriesRange), GroupBy, timeseriesOffset);

            DateTimeOffsetInterval reportPeriod = SelectedPeriod;

            try
            {
                DateTimeOffsetInterval selectedInterval = _Common.GetIntervalInUtcOffset(SelectedPeriod, timeseriesOffset);
                DateTimeOffsetInterval trimmedPeriodSelected = GetTrimmedPeriodSelected(selectedInterval);
                DateTimeOffsetInterval overlappedInterval = GetIntervalOfOverlap(trimmedPeriodSelected, TimeSeriesRange, timeseriesOffset);

                reportPeriod = AdjustIntervalToGroupBy(overlappedInterval, GroupBy);
            }
            catch (Exception exp)
            {
                Log.Error("Error in AdjustIntervalToGroupBy", exp);
            }
            Log.DebugFormat("AdjustIntervalToGroupBy returns report period = {0}", _Common.TimeRangeString(reportPeriod));

            return reportPeriod;
        }

        public static DateTimeOffsetInterval GetTrimmedPeriodSelected(DateTimeOffsetInterval periodSelected)
        {
            DateTimeOffset? endTime = periodSelected.End;
            if (endTime.HasValue)
            {
                if ((endTime.Value.Hour == 0) && (endTime.Value.Minute == 0) && (endTime.Value.Second == 0))
                    endTime = endTime.Value.AddMilliseconds(-1); // avoid having endTime be at exact start of day/month/year
            }
            DateTimeOffset? startTime = periodSelected.Start;
            if (startTime.HasValue)
            {
                startTime = new DateTimeOffset(startTime.Value.Year, startTime.Value.Month, startTime.Value.Day, 0, 0, 0, startTime.Value.Offset);
            }
            if (startTime.HasValue && endTime.HasValue && endTime.Value < startTime.Value)
                endTime = startTime.Value;
            return new DateTimeOffsetInterval(startTime, endTime);
        }

        public DateTimeOffsetInterval GetIntervalOfOverlap(DateTimeOffsetInterval SelectedPeriod, DateTimeOffsetInterval TimeSeriesRange, TimeSpan utcOffset)
        {
            DateTimeOffset? StartTime = null;
            DateTimeOffset? EndTime = null;

            if (SelectedPeriod.Start.HasValue && TimeSeriesRange.Start.HasValue)
                StartTime = (SelectedPeriod.Start.Value > TimeSeriesRange.Start.Value) ? SelectedPeriod.Start.Value.ToOffset(utcOffset) :
                    (SelectedPeriod.End.HasValue && (SelectedPeriod.End.Value < TimeSeriesRange.Start.Value)) ?
                    SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.Start.Value;
            else
                StartTime = (SelectedPeriod.Start.HasValue) ? SelectedPeriod.Start.Value.ToOffset(utcOffset) : TimeSeriesRange.Start;

            if (SelectedPeriod.End.HasValue && TimeSeriesRange.End.HasValue)
                EndTime = (SelectedPeriod.End.Value < TimeSeriesRange.End.Value) ? SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.End.Value;
            else
                EndTime = (SelectedPeriod.End.HasValue) ? SelectedPeriod.End.Value.ToOffset(utcOffset) : TimeSeriesRange.End;

            if (!TimeSeriesRange.Start.HasValue && !TimeSeriesRange.End.HasValue)
            {
                // TimeSeries has no range - no points in it and will be no useful time range therefore collapse time range
                if (StartTime.HasValue)
                    EndTime = StartTime.Value;
                else if (EndTime.HasValue)
                    StartTime = EndTime.Value;
            }

            DateTimeOffset now = DateTimeOffset.Now.ToOffset(utcOffset);

            DateTimeOffset TheStartTime = (StartTime.HasValue) ? StartTime.Value.ToOffset(utcOffset) : now;
            DateTimeOffset TheEndTime = (EndTime.HasValue) ? EndTime.Value.ToOffset(utcOffset) : now;

            TheEndTime = (TheEndTime > TheStartTime) ? TheEndTime : TheStartTime; // avoid improper interval

            return new DateTimeOffsetInterval(TheStartTime, TheEndTime);
        }
        public DateTimeOffsetInterval AdjustIntervalToGroupBy(DateTimeOffsetInterval timeRange, string GroupBy)
        {
            Log.DebugFormat("Begin GroupBy '{0}' adjustments for interval {1} - {2}", GroupBy, timeRange.Start, timeRange.End);

            if (!timeRange.Start.HasValue || !timeRange.End.HasValue)
            {
                Log.DebugFormat("AdjustIntervalToGroupBy unable to adjust timeRange interval = {0} because start or end has no value, return unadjusted", _Common.TimeRangeString(timeRange));
                return timeRange;
            }

            try
            {
                DateTimeOffset TheStartTime = timeRange.Start.Value;
                DateTimeOffset TheEndTime = timeRange.End.Value;
                TimeSpan tsOffset = TheStartTime.Offset;

                if (GroupBy == "Year")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, 1, 1, 0, 0, 0, tsOffset);
                    TheEndTime = (new DateTimeOffset(TheEndTime.Year, 1, 1, 0, 0, 0, tsOffset)).AddYears(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "WaterYear")
                {
                    int startYear = (TheStartTime.Month >= _WaterYearMonth) ? TheStartTime.Year : TheStartTime.Year - 1;
                    TheStartTime = new DateTimeOffset(startYear, _WaterYearMonth, 1, 0, 0, 0, tsOffset);

                    int endYear = (TheEndTime.Month >= _WaterYearMonth) ? TheEndTime.Year : TheEndTime.Year - 1;
                    TheEndTime = (new DateTimeOffset(endYear, _WaterYearMonth, 1, 0, 0, 0, tsOffset)).AddYears(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "Month")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, 1, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, 1, 0, 0, 0, tsOffset).AddMonths(1).AddMilliseconds(-1);
                }
                else if (GroupBy == "Week")
                {
                    int numberOfDays = ((int)Math.Ceiling(((TheEndTime - TheStartTime).TotalDays + 1) / 7.0)) * 7;
                    TheEndTime = TheStartTime.AddDays(numberOfDays);

                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddMilliseconds(-1);
                }
                else if (GroupBy == "Day")
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddDays(1).AddMilliseconds(-1);
                }
                else
                {
                    TheStartTime = new DateTimeOffset(TheStartTime.Year, TheStartTime.Month, TheStartTime.Day, 0, 0, 0, tsOffset);
                    TheEndTime = new DateTimeOffset(TheEndTime.Year, TheEndTime.Month, TheEndTime.Day, 0, 0, 0, tsOffset).AddDays(1).AddMilliseconds(-1);
                }

                Log.DebugFormat("End GroupBy '{0}' adjustments for interval {1} - {2}", GroupBy, TheStartTime, TheEndTime);
                timeRange = new DateTimeOffsetInterval(TheStartTime, TheEndTime);
            }
            catch (Exception exp)
            {
                Log.Error("Error in AdjustIntervalToGroupBy", exp);
            }
            Log.DebugFormat("AdjustIntervalToGroupBy returns timeRange interval = {0}", _Common.TimeRangeString(timeRange));

            return timeRange;
        }

        public DataRelation AddGroupRelation(string GroupBy, string relationName, DataTable groupBy, DataTable pointsTable)
        {
            DataSet dataSet = groupBy.DataSet;
            DataRelation relation;
            if (GroupBy == "Year")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["Year"], pointsTable.Columns["Year"]);
            }
            else if (GroupBy == "WaterYear")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["WaterYear"], pointsTable.Columns["WaterYear"]);
            }
            else if (GroupBy == "Month")
            {
                relation = dataSet.Relations.Add(relationName,
                  new DataColumn[] { groupBy.Columns["Year"], groupBy.Columns["Month"] },
                  new DataColumn[] { pointsTable.Columns["Year"], pointsTable.Columns["Month"] });
            }
            else if (GroupBy == "Day")
            {
                relation = dataSet.Relations.Add(relationName,
                  new DataColumn[] { groupBy.Columns["Year"], groupBy.Columns["Month"], groupBy.Columns["Day"] },
                  new DataColumn[] { pointsTable.Columns["Year"], pointsTable.Columns["Month"], pointsTable.Columns["Day"] });
            }
            else if (GroupBy == "Week")
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["Week"], pointsTable.Columns["Week"]);
            }
            else
            {
                relation = dataSet.Relations.Add(relationName, groupBy.Columns["NoGroupBy"], pointsTable.Columns["NoGroupBy"]);
            }

            return relation;
        }

        public DataTable GetGroupByTable(string tableName)
        {
            DataTable groupBy = new DataTable(tableName);

            groupBy.Columns.Add("NoGroupBy", typeof(int));
            groupBy.Columns.Add("Year", typeof(int));
            groupBy.Columns.Add("WaterYear", typeof(int));
            groupBy.Columns.Add("Month", typeof(int));
            groupBy.Columns.Add("Day", typeof(int));
            groupBy.Columns.Add("Week", typeof(int));
            groupBy.Columns.Add("StartTime", typeof(DateTimeOffset));
            groupBy.Columns.Add("EndTime", typeof(DateTimeOffset));

            return groupBy;
        }

        public void AddRowsToGroupBy(DataTable groupBy, string GroupBy, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            if (GroupBy == "Year")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, 1, 1, 0, 0, 0, startTime.Offset);

                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddYears(1).AddMilliseconds(-1));
                    time = time.AddYears(1);
                }
            }
            else if (GroupBy == "WaterYear")
            {
                int firstYear = (startTime.Month >= _WaterYearMonth) ? startTime.Year : startTime.Year - 1;
                DateTimeOffset time = new DateTimeOffset(firstYear, _WaterYearMonth, 1, 0, 0, 0, startTime.Offset);

                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddYears(1).AddMilliseconds(-1));
                    time = time.AddYears(1);
                }
            }
            else if (GroupBy == "Month")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, 1, 0, 0, 0, startTime.Offset);
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddMonths(1).AddMilliseconds(-1));
                    time = time.AddMonths(1);
                }
            }
            else if (GroupBy == "Day")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, 0, 0, 0, startTime.Offset);
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, 0, time, time.AddDays(1).AddMilliseconds(-1));
                    time = time.AddDays(1);
                }
            }
            else if (GroupBy == "Week")
            {
                DateTimeOffset time = new DateTimeOffset(startTime.Year, startTime.Month, startTime.Day, 0, 0, 0, startTime.Offset);
                int weekNumber = 1;
                while ((groupBy.Rows.Count < 1) || (time < endTime))
                {
                    AddGroupByRow(groupBy, weekNumber++, time, time.AddDays(7).AddMilliseconds(-1));
                    time = time.AddDays(7);
                }
            }
            else
            {
                AddGroupByRow(groupBy, 0, startTime, endTime);
            }
        }

        private void AddGroupByRow(DataTable groupBy, int weekNumber, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            DataRow row = groupBy.NewRow();

            row["NoGroupBy"] = 0;
            row["Year"] = startTime.Year;
            row["WaterYear"] = (startTime.Month >= _WaterYearMonth) ? startTime.Year : startTime.Year - 1;
            row["Month"] = startTime.Month;
            row["Day"] = startTime.Day;
            row["Week"] = weekNumber;
            row["StartTime"] = startTime;
            row["EndTime"] = endTime;

            Log.DebugFormat("AddGroupByRow: year = {0}, wateryear = {1}, month = {2}, day = {3}, week = {4}, starttime = {5}, endtime = {6}",
              row["Year"], row["WaterYear"], row["Month"], row["Day"], row["Week"], row["StartTime"], row["EndTime"]);

            groupBy.Rows.Add(row);
        }
    }
}
