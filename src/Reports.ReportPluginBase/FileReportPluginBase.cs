using System.Data;
using System.Reflection;
using ReportPluginFramework.Beta;
using PerpetuumSoft.Reporting.Components;
using PerpetuumSoft.Reporting.DOM;
using System.IO;
using PerpetuumSoft.Reporting.Export.Pdf;
using PerpetuumSoft.Reporting.Export.Csv;
using PerpetuumSoft.Reporting.Export.OpenXML;
using PerpetuumSoft.Reporting.Export.Graph;

namespace Reports
{
    public abstract class FileReportPluginBase : ReportPluginBase
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public virtual FileReportOutput GenerateReport(RunFileReportRequest request)
        {
            FileReportOutput fileReportOutput = new FileReportOutput();

            string fileName = GenerateReportIntoFile(request);
            fileReportOutput.OutputFilePath = fileName;

            return fileReportOutput;
        }

        public string GenerateReportIntoFile(RunFileReportRequest request)
        {
            DataSet set = GenerateDataSet(request);
            Log.Debug("GenerateReport - after generate dataset");

            string dllFolder = (string)set.Tables["RunReportRequest"].Rows[0]["DllFolder"];
            string dllName = (string)set.Tables["RunReportRequest"].Rows[0]["DllName"];

            string templateFileName = Path.Combine(dllFolder, dllName + ".rst");

            Log.DebugFormat("GenerateReport rst template file name is {0}", templateFileName);

            string xmlTemplate = File.ReadAllText(templateFileName);

            Log.Debug("GenerateReport after read rst template file");

            ReportManager reportManager = new ReportManager();
            reportManager.DataSources.Add(set.DataSetName, set);
            reportManager.OwnerForm = null;
            Log.Debug("GenerateReport after init ReportManager");

            InlineReportSlot inlineReportSlot = new InlineReportSlot();
            inlineReportSlot.DocumentStream = xmlTemplate;
            inlineReportSlot.RenderingError += InlineReportSlot_RenderingError;
            Log.Debug("GenerateReport after init InlineReportSlot");

            reportManager.Reports.Add(inlineReportSlot);
            Document document = inlineReportSlot.RenderDocument();

            Log.Debug("GenerateReport after RenderDocument");
            string outputFormat = request.OutputFormat;
            Log.DebugFormat("GenerateReport after RenderDocument, document name is {0}, page count is {1}, outputFormat is {2}", 
                document.Name, document.Pages.Count, outputFormat);

            string tempFileName = Path.GetTempFileName();
            tempFileName = Path.ChangeExtension(tempFileName, outputFormat);
            try
            {
                File.Delete(tempFileName);
            }
            catch { }

            Log.DebugFormat("GenerateReport - the name of file to return is {0}", tempFileName);

            switch (outputFormat)
            {
                case "PDF":
                    {
                        PdfExportFilter exportFilter = new PdfExportFilter();

                        exportFilter.Compress = true;
                        exportFilter.Export(document, tempFileName, false);
                        Log.DebugFormat("GenerateReport after export document to PDF into tempFileName = {0}", tempFileName);
                        break;
                    }
                case "CSV":
                    {
                        CsvExportFilter exportFilter = new CsvExportFilter();

                        exportFilter.Separator = ",";
                        exportFilter.Export(document, tempFileName, false);
                        Log.DebugFormat("GenerateReport after export document to CSV into tempFileName = {0}", tempFileName);
                        break;
                    }
                case "XLSX":
                    {
                        ExcelExportFilter exportFilter = new ExcelExportFilter();
                        exportFilter.ExportAsData = true;
                        exportFilter.Export(document, tempFileName, false);
                        Log.DebugFormat("GenerateReport after export document to XLSX into tempFileName = {0}", tempFileName);
                        break;
                    }
                case "PNG":
                    {
                        PngExportFilter exportFilter = new PngExportFilter();
                        exportFilter.Export(document, tempFileName, false);
                        Log.DebugFormat("GenerateReport after export document to XLSX into tempFileName = {0}", tempFileName);
                        break;
                    }
                default:
                    {
                        Log.DebugFormat("Unable to export report - unknown export format '{0}'", outputFormat);
                        break;
                    }
            }
            return tempFileName;
        }

        private void InlineReportSlot_RenderingError(object sender, RenderingErrorEventArgs e)
        {
            Log.Debug("Report Render Error");

            Log.Error("Render Error: ", e.Exception);
            Log.Error("Render Error (innerException): ", e.Exception.InnerException);

            throw new System.Exception("Report Rendering Error", e.Exception);
        }

        public virtual DataSet GenerateDataSet(RunFileReportRequest request)
        {
            Log.Debug("GenerateDataSet - call GeneratePdfDataSet");
            DataSet reportTables = GeneratePdfDataSet(request);
            Log.DebugFormat("GenerateDataSet - add OutputFormat = '{0}'", request.OutputFormat);
            AddOutputFormat(request, reportTables);
            Log.Debug("returning from GenerateDataSet");

            return reportTables;
        }

        public virtual void AddOutputFormat(RunFileReportRequest request, System.Data.DataSet dataSet)
        {
            DataTable table = dataSet.Tables["ReportData"];
            table.Columns.Add("OutputFormat", typeof(string));
            table.Rows[0]["OutputFormat"] = request.OutputFormat;

            string outputFormatMessage = string.Format("{0}OutputFormat: {1}", System.Environment.NewLine, System.Environment.NewLine);
            outputFormatMessage += string.Format("OutputFormat = '{0}'{1}", request.OutputFormat, System.Environment.NewLine);
            Log.Info(outputFormatMessage);

            DataTable runReportRequestTable = dataSet.Tables["RunReportRequest"];
            string message = (string)runReportRequestTable.Rows[0]["ReportInputInformation"];
            runReportRequestTable.Rows[0]["ReportInputInformation"] = message + outputFormatMessage; ;
        }
    }
}
