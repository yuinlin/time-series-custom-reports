using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework;
using Server.Services.PublishService.ServiceModel.Dtos.FieldVisit;
using Server.Services.PublishService.ServiceModel.Dtos;
using System.Collections.Generic;

namespace BenchmarkHistoryNamespace
{
    public class ReportSpecificFunctions
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static bool TimeRangeOverlaps(DateTimeOffset start, DateTimeOffset? end, DateTimeOffsetInterval interval)
        {
            if (interval.End.HasValue && (start > interval.End.Value)) return false;
            if (interval.Start.HasValue && end.HasValue && (end < interval.Start.Value)) return false;
            return true;
        }
        public static double calculateDifference(LevelSurveyMeasurement lsm, List<ReferencePointPeriod> refPointHistory)
        {
            double level = lsm.MeasuredElevation.Numeric.Value;
            foreach (ReferencePointPeriod period in refPointHistory)
            {
                if (lsm.MeasurementTime >= period.ValidFrom)
                    return level - period.Elevation;
            }
            return double.NaN;
        }
    }
}
