using System;
using System.Data;
using System.Collections.Generic;
using System.Reflection;

using ReportPluginFramework;
using ReportPluginFramework.ReportData;

using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.ResponseDtos;
using Server.Services.PublishService.ServiceModel.Dtos;



namespace Reports
{
    public class TimeSeriesInstMinMaxFinder
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Common _Common;

        public TimeSeriesInstMinMaxFinder(Common common)
        {
            _Common = common;
        }
        public TimeAlignedPoint[] GetInstMinMaxPoints(Guid inputGuid, string interval, bool extrema, DateTimeOffset StartTime, DateTimeOffset EndTime)
        {
            int hoursToExtend = 12; // amount of extra time for finding the interpolated boundary value at start and end times
            List<TimeAlignedPoint> points = _Common.GetTimeAlignedPoints(new List<Guid> { inputGuid },
                StartTime.AddHours(-1 * hoursToExtend), EndTime.AddHours(hoursToExtend));

            DateTimeOffset[] binEnds = GetDayEnds(StartTime, EndTime);

            if (interval == "Monthly")
                binEnds = GetMonthEnds(StartTime, EndTime);
            if (interval == "Yearly")
                binEnds = GetYearEnds(StartTime, EndTime);

            return GetInstMinMaxPoints(points, extrema, StartTime, binEnds);
        }


        public static DateTimeOffset[] GetDayEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> midnights = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddDays(1);
            while (time <= theEndTime.AddDays(1))
            {
                midnights.Add(time);
                time = time.AddDays(1);
            }

            return midnights.ToArray();
        }


        public static DateTimeOffset[] GetMonthEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> times = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddMonths(1);
            while (time <= theEndTime.AddDays(1))
            {
                times.Add(time);
                time = time.AddMonths(1);
            }

            return times.ToArray();
        }

        public static DateTimeOffset[] GetYearEnds(DateTimeOffset theStartTime, DateTimeOffset theEndTime)
        {
            List<DateTimeOffset> times = new List<DateTimeOffset>();
            DateTimeOffset time = theStartTime.AddYears(1);
            while (time <= theEndTime.AddDays(1))
            {
                times.Add(time);
                time = time.AddYears(1);
            }

            return times.ToArray();
        }

        public static TimeAlignedPoint[] GetInstMinMaxPoints(List<TimeAlignedPoint> inputPoints,
          bool extrema, DateTimeOffset theStartTime, DateTimeOffset[] binEnds)
        {
            bool MIN = true;
            bool MAX = false;

            TimeAlignedPoint[] pointsOfExtrema = new TimeAlignedPoint[binEnds.Length];
            for (int i = 0; i < binEnds.Length; i++) pointsOfExtrema[i] = new TimeAlignedPoint();

            if (inputPoints.Count == 0) return pointsOfExtrema;

            int j = 0;
            double leftBinValue = double.NaN;

            while ((j < inputPoints.Count) && inputPoints[j].Timestamp < theStartTime) j++;

            if (j >= inputPoints.Count) return pointsOfExtrema;

            if ((inputPoints[j].Timestamp >= theStartTime) && (j > 0))
                leftBinValue = calcInterpolatedPointValue(inputPoints[j - 1], inputPoints[j], theStartTime);

            for (int i = 0; i < binEnds.Length; i++)
            {
                DateTimeOffset currentTime = (i > 0) ? binEnds[i - 1] : theStartTime;
                double currentVal = leftBinValue;

                Log.DebugFormat("i = {0}, j = {1}, leftBinValue = {2}, time = {3}", i, j, leftBinValue, currentTime);

                for (; j < inputPoints.Count; j++)
                {
                    TimeAlignedPoint inputPoint = inputPoints[j];

                    if (inputPoint.Timestamp > binEnds[i])
                        break;

                    if ((inputPoint.NumericValue1.HasValue) &&
                      (double.IsNaN(currentVal) ||
                      ((extrema == MIN) && (inputPoint.NumericValue1.Value < currentVal)) ||
                      ((extrema == MAX) && (inputPoint.NumericValue1.Value > currentVal))))
                    {
                        currentTime = inputPoint.Timestamp;
                        currentVal = inputPoint.NumericValue1.Value;
                    }
                }

                double rightBinValue = double.NaN;

                if ((j > 0) && (j < inputPoints.Count) && (inputPoints[j].Timestamp > binEnds[i]))
                {
                    DateTimeOffset binEnd = binEnds[i];
                    rightBinValue = calcInterpolatedPointValue(inputPoints[j - 1], inputPoints[j], binEnd);
                    Log.DebugFormat("checking boundary extrema i = {0}, j = {1}, binEnd = {2}, rightBinValue = {3}", i, j, binEnd, rightBinValue);
                    if (!double.IsNaN(rightBinValue) &&
                      (double.IsNaN(currentVal) ||
                      ((extrema == MIN) && (rightBinValue < currentVal)) ||
                      ((extrema == MAX) && (rightBinValue > currentVal))))
                    {
                        currentTime = binEnd;
                        currentVal = rightBinValue;
                        Log.DebugFormat("checking boundary extrema i = {0}, j = {1}, currentTime = {2}, currentVal = {3}", i, j, currentTime, currentVal);
                    }
                }

                if (!double.IsNaN(currentVal))
                {
                    pointsOfExtrema[i].Timestamp = currentTime;
                    pointsOfExtrema[i].NumericValue1 = currentVal;
                    Log.DebugFormat("setting extrema i = {0}, j = {1}, currentTime = {2}, currentVal = {3}", i, j, currentTime, currentVal);
                }

                leftBinValue = rightBinValue;
            }

            return pointsOfExtrema;
        }

        public static double calcInterpolatedPointValue(TimeAlignedPoint point1, TimeAlignedPoint point2, DateTimeOffset theX)
        {
            double y1 = double.NaN;
            double y2 = double.NaN;

            if (goodValuePoint(point1)) y1 = point1.NumericValue1.Value;
            if (goodValuePoint(point2)) y2 = point2.NumericValue1.Value;

            return calcInterpolatedValue(point1.Timestamp, y1, point2.Timestamp, y2, theX);
        }

        public static bool goodValuePoint(TimeAlignedPoint point)
        {
            return point.NumericValue1.HasValue; // possible enhancement to check grade code
        }

        public static double calcInterpolatedValue(DateTimeOffset x1, double y1, DateTimeOffset x2, double y2, DateTimeOffset theX)
        {
            if ((x1 <= theX) && (theX <= x2))
            {
                if (x1 == theX)
                    return y1;

                if (x2 == theX)
                    return y2;

                if (!double.IsNaN(y1) && !double.IsNaN(y2))
                {
                    double timeFraction = ((double)(theX - x1).Ticks) / ((double)(x2 - x1).Ticks);

                    double calc = y1 + ((y2 - y1) * timeFraction);

                    return calc;
                }
            }
            return double.NaN;
        }
    }
}
