using System;
using System.Collections.Generic;
using System.IO;
using ReportPluginFramework;
using Server.Services.PublishService.ServiceModel.RequestDtos;
using Server.Services.PublishService.ServiceModel.Dtos;

namespace HydrologicalCommaSeparated
{
    public class HydrologicalCommaSeparated : IFileReport
    {
        public string _DateFormat = "yyyy-MM-ddTHH:mm:ss";
        public TimeSeriesDescription _TimeSeriesDescription;
        public RunFileReportRequest _RunReportRequest;
        public string _SensorType;

        public FileReportOutput GenerateReport(RunFileReportRequest request)
        {
            InitReport(request);

            ThrowIfInvalidInputTimeSeries();

            var inputFields = GetInputFields();

            var fileName = WriteValuesToFile(inputFields);

            return MakeReportOutput(fileName);
        }

        public void InitReport(RunFileReportRequest request)
        {
            _RunReportRequest = request;
            _TimeSeriesDescription = GetInputTimeSeriesDescription();
            _SensorType = MapSensorType();
        }
        public TimeSeriesDescription GetInputTimeSeriesDescription()
        {
            var request = new TimeSeriesDescriptionListByUniqueIdServiceRequest();
            request.TimeSeriesUniqueIds = new List<Guid> { _RunReportRequest.Inputs.TimeSeriesInputs[0].UniqueId };
            return _RunReportRequest.Publish.Get(request).TimeSeriesDescriptions[0];
        }

        public string MapSensorType()
        {
            var parameter = _TimeSeriesDescription.Parameter;
            switch (parameter)
            {
                case "Stage": return "WL";
                case "Precip Increm": return "RN";
                case "Discharge": return "DI";
                case "Flow": return "DI";
                case "Voltage": return "SV";
                case "Total Storage": return "WS";
                default: return "";
            }
        }

        public void ThrowIfInvalidInputTimeSeries()
        {
            if (string.IsNullOrEmpty(_SensorType))
            {
                var errMessage = string.Format("Input time-series has invalid Parameter '{0}'. ", _TimeSeriesDescription.Parameter);
                errMessage += "Input signal must have parameter 'Stage', 'Precip Increm', 'Discharge', 'Flow', 'Voltage' or 'Total Storage'.";

                throw (new Exception(errMessage));
            }
        }
        public InputFields GetInputFields()
        {
            InputFields inputFields = new InputFields();
            inputFields.AgencyId = _RunReportRequest.Parameters.GetString("AgencyId");
            inputFields.SiteIdType = _RunReportRequest.Parameters.GetString("SiteIdType");
            inputFields.SiteId = _RunReportRequest.Parameters.GetString("SiteId");
            inputFields.SensorDataType = _RunReportRequest.Parameters.GetString("SensorDataType");
            inputFields.SensorTypeParam1 = _RunReportRequest.Parameters.GetString("SensorTypeParam1");
            inputFields.SensorTypeParam2 = _RunReportRequest.Parameters.GetString("SensorTypeParam2");
            return inputFields;
        }

        public string WriteValuesToFile(InputFields inputFields)
        {
            var timeSeriesPoints = GetPoints();

            var fileName = GetOutputFileName();

            var file = File.AppendText(fileName);

            WriteFileHeader(file, inputFields, timeSeriesPoints.Count);

            WritePoints(file, inputFields, timeSeriesPoints);

            file.Close();

            return fileName;
        }

        public List<TimeAlignedPoint> GetPoints()
        {
            var pointsRequest = new TimeAlignedDataServiceRequest();
            pointsRequest.ApplyRounding = false;
            pointsRequest.IncludeGapMarkers = false;
            pointsRequest.TimeSeriesUniqueIds = new List<Guid> { _TimeSeriesDescription.UniqueId };
            pointsRequest.QueryFrom = _RunReportRequest.Interval.Start;
            pointsRequest.QueryTo = _RunReportRequest.Interval.End;

            return _RunReportRequest.Publish.Get(pointsRequest).Points;
        }

        public string GetOutputFileName()
        {
            var tempFileName = Path.GetTempFileName();
            var csvFileName = Path.ChangeExtension(tempFileName, "hcs");
            try
            {
                File.Delete(tempFileName);
            }
            catch { }
            try
            {
                File.Delete(csvFileName);
            }
            catch { }
            return csvFileName;
        }
        public void WriteFileHeader(StreamWriter file, InputFields inputFields, int numberOfRecords)
        {
            var tsOffset = TimeSpan.FromHours(_TimeSeriesDescription.UtcOffset);
            var offset = ((tsOffset < TimeSpan.Zero) ? "-" : "") + tsOffset.ToString(@"hh\:mm");

            file.WriteLine(string.Format("# HEADER: Agency Id: {0}", inputFields.AgencyId));
            file.WriteLine(string.Format("# HEADER: File Generation Date: {0}", DateTimeOffset.Now.ToString(_DateFormat)));
            file.WriteLine("# HEADER: File Format: BOM-HCS");
            file.WriteLine("# HEADER: File Format Version: 2.0");
            file.WriteLine("# HEADER: Generated by (system): AQUARIUS");
            file.WriteLine(string.Format("# HEADER: Number of Records: {0}", numberOfRecords));
            file.WriteLine(string.Format("# HEADER: Local ObsTime Offset: {0}", offset));
            file.WriteLine("# HEADER: Data Fields: IndexNo, SensorType, " +
                "SensorDataType, SiteIdType, SiteId, ObservationTimestamp, " +
                "RealValue, Unit, SensorTypeParam1, SensorTypeParam2, Quality, Comment");
        }

        public void WritePoints(StreamWriter file, InputFields inputFields, List<TimeAlignedPoint> timeSeriesPoints)
        {
            var quality = "1";
            var unit = MapUnit();

            for (int i = 0; i < timeSeriesPoints.Count; i++)
            {
                var timeSeriesPoint = timeSeriesPoints[i];
                var line = string.Format("{0},\"{1}\",{2},\"{3}\",\"{4}\",\"{5}\",{6},\"{7}\",\"{8}\",{9},{10},\"{11}\"",
                    i + 1, _SensorType, inputFields.SensorDataType, inputFields.SiteIdType, inputFields.SiteId,
                    timeSeriesPoint.Timestamp.ToString(_DateFormat),
                    (timeSeriesPoint.NumericValue1.HasValue) ? FormatValue(timeSeriesPoint.NumericValue1.Value) : "",
                    unit, inputFields.SensorTypeParam1, inputFields.SensorTypeParam2, quality, "");

                file.WriteLine(line);
            }
        }

        public string MapUnit()
        {
            var parameter = _TimeSeriesDescription.Parameter;
            switch (parameter)
            {
                case "Stage": return "metres";
                case "Precip Increm": return "mm";
                case "Discharge": return "m3/s";
                case "Flow": return "Ml/d";
                case "Voltage": return "V";
                case "Total Storage": return "Ml";
                default: return "";
            }
        }
        public string FormatValue(double value)
        {
            if (double.IsNaN(value)) return "";

            return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        public FileReportOutput MakeReportOutput(string fileName)
        {
            var fileReportOutput = new FileReportOutput();
            fileReportOutput.OutputFilePath = fileName;
            return fileReportOutput;
        }
    }

    public class InputFields
    {
        public string AgencyId { get; set; }
        public string SiteId { get; set; }
        public string SiteIdType { get; set; }
        public string SensorDataType { get; set; }
        public string SensorTypeParam1 { get; set; }
        public string SensorTypeParam2 { get; set; }
    }
}
