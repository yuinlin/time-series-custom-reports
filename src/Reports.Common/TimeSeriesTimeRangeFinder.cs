using System;
using System.Collections.Generic;
using System.Reflection;
using ReportPluginFramework;
using ReportPluginFramework.ReportData.TimeSeriesData;
using ReportPluginFramework.ReportData.TimeSeriesComputedStatistics;

namespace Reports
{
    public class TimeSeriesTimeRangeFinder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public TimeSeriesTimeRangeFinder(Common common)
        {
            _Common = common;
        }

        public DateTimeOffsetInterval FindTimeSeriesTimeRange(Guid timeseriesUniqueId)
        {
            List<TimeSeriesPoint> points = FindAnnualCountPoints(timeseriesUniqueId);
            if (points.Count == 0) return new DateTimeOffsetInterval();

            TimeSpan offset = points[0].Timestamp.Offset;

            int binAdjust = _Common.GetBinAdjustment(timeseriesUniqueId);

            DateTimeOffset FirstYear = new DateTimeOffset(points[0].Timestamp.Year + binAdjust, 1, 1, 0, 0, 0, offset);
            DateTimeOffset LastYear = new DateTimeOffset(points[points.Count - 1].Timestamp.Year + binAdjust, 1, 1, 0, 0, 0, offset);
            Log.DebugFormat("Found first point is in Year = {0}, last point is in Year = {1}", FirstYear, LastYear);

            points = FindDailyCountPoints(timeseriesUniqueId, FirstYear, FirstYear.AddYears(1).AddDays(1));
            if (points.Count == 0) return new DateTimeOffsetInterval();

            DateTimeOffset firstTime = FirstNonZeroPoint(points).Timestamp;
            DateTimeOffset queryFromTime = new DateTimeOffset(firstTime.Year, firstTime.Month, firstTime.Day, 0, 0, 0, offset);
            points = _Common.GetTimeSeriesPoints(timeseriesUniqueId, queryFromTime.AddDays(binAdjust), queryFromTime.AddDays(binAdjust + 1));
            if (points.Count == 0) return new DateTimeOffsetInterval();

            firstTime = points[0].Timestamp;
            Log.DebugFormat("Found first point at time = {0}", firstTime.ToString(Common._DateFormat));

            points = FindDailyCountPoints(timeseriesUniqueId, LastYear, LastYear.AddYears(1).AddDays(1));
            if (points.Count == 0) return new DateTimeOffsetInterval(firstTime, null);

            DateTimeOffset lastTime = LastNonZeroPoint(points).Timestamp;
            queryFromTime = new DateTimeOffset(lastTime.Year, lastTime.Month, lastTime.Day, 0, 0, 0, offset);
            points = _Common.GetTimeSeriesPoints(timeseriesUniqueId, queryFromTime.AddDays(binAdjust), queryFromTime.AddDays(binAdjust + 1));
            if (points.Count == 0) return new DateTimeOffsetInterval(firstTime, null);

            lastTime = points[points.Count - 1].Timestamp;
            Log.DebugFormat("Found last point at time = {0}", lastTime.ToString(Common._DateFormat));

            DateTimeOffsetInterval interval = new DateTimeOffsetInterval(firstTime, lastTime);
            return interval;
        }

         private List<TimeSeriesPoint> FindAnnualCountPoints(Guid timeseriesUniqueId)
        {
            Log.DebugFormat("FindAnnualCountPoints for uniqueId = {0} over entire range (StartTime and EndTime set to null)", timeseriesUniqueId);

            return _Common.GetComputedStatisticsPoints(timeseriesUniqueId, StatisticType.Count, StatisticPeriod.Annual, false, null);
        }

        private List<TimeSeriesPoint> FindDailyCountPoints(Guid timeseriesUniqueId, DateTimeOffset fromTime, DateTimeOffset toTime)
        {
            Log.DebugFormat("FindDailyCountPoints for uniqueId = {0} from {1} to {2}",
                timeseriesUniqueId, fromTime.ToString(Common._DateFormat), toTime.ToString(Common._DateFormat));

            return _Common.GetComputedStatisticsPoints(timeseriesUniqueId, fromTime, toTime, StatisticType.Count, StatisticPeriod.Daily, false, null);
        }

        private TimeSeriesPoint FirstNonZeroPoint(List<TimeSeriesPoint> points)
        {
            TimeSeriesPoint firstDataPoint = points[0];
            foreach (TimeSeriesPoint p in points)
                if (p.Value.HasValue && p.Value.Value > 0)
                {
                    firstDataPoint = p;
                    break;
                }
            Log.DebugFormat("FirstNonZeroPoint at timestamp = {0}, value = {1}", firstDataPoint.Timestamp.ToString(Common._DateFormat), firstDataPoint.Value);
            return firstDataPoint;
        }

        private TimeSeriesPoint LastNonZeroPoint(List<TimeSeriesPoint> points)
        {
            points.Reverse();
            TimeSeriesPoint lastPoint = FirstNonZeroPoint(points);
            Log.DebugFormat("LastNonZeroPoint at timestamp = {0}, value = {1}", lastPoint.Timestamp.ToString(Common._DateFormat), lastPoint.Value);
            return lastPoint;
        }
    }
}
