using System.Data;
using System.Reflection;
using ReportPluginFramework;
using PerpetuumSoft.Reporting.Components;
using PerpetuumSoft.Reporting.DOM;
using System.IO;
using PerpetuumSoft.Reporting.Export.Pdf;
using PerpetuumSoft.Reporting.Export.Csv;
using PerpetuumSoft.Reporting.Export.OpenXML;
using PerpetuumSoft.Reporting.Export.Graph;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using PerpetuumSoft.Reporting.Rendering;

namespace Reports
{
    public abstract class ReportPluginBase
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Assembly _Assembly = Assembly.GetExecutingAssembly();

        public virtual FileReportOutput GenerateReport(RunFileReportRequest request)
        {
            FileReportOutput fileReportOutput = new FileReportOutput();

            string fileName = GenerateReportIntoFile(request);
            fileReportOutput.OutputFilePath = fileName;

            return fileReportOutput;
        }

        private string GenerateReportIntoFile(RunFileReportRequest request)
        {
            Thread.CurrentThread.CurrentCulture.NumberFormat = CultureInfo.InvariantCulture.NumberFormat;

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
            
            string tempFolderPath = Path.GetTempPath();

            if (!Directory.Exists(tempFolderPath))
            {
                throw new DirectoryNotFoundException("Temp folder directory does not exist.");
            }
            
            string tempFileName = Path.Combine(tempFolderPath, Path.GetRandomFileName());
            string outputFileName = Path.ChangeExtension(tempFileName, outputFormat);

            Log.DebugFormat("GenerateReport - the name of file for export report to write to is set to {0}", outputFileName);

            try
            {
                switch (outputFormat.ToUpper())
                {
                    case "PDF":
                        {
                            PdfExportFilter exportFilter = new PdfExportFilter();

                            exportFilter.Compress = true;
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to PDF into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "CSV":
                        {
                            CsvExportFilter exportFilter = new CsvExportFilter();

                            exportFilter.Separator = ",";
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to CSV into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "XLSX":
                        {
                            ExcelExportFilter exportFilter = new ExcelExportFilter();
                            exportFilter.ExportAsData = true;
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to XLSX into tempFileName = {0}", outputFileName);
                            break;
                        }
                    case "PNG":
                        {
                            PngExportFilter exportFilter = new PngExportFilter();
                            exportFilter.PagesRange = "1";
                            exportFilter.Export(document, outputFileName, false);
                            Log.DebugFormat("GenerateReport after export document to PNG into tempFileName = {0}", outputFileName);
                            break;
                        }
                    default:
                        {
                            Log.DebugFormat("Unable to export report - unknown export format '{0}'", outputFormat);
                            break;
                        }
                }
            }
            catch (System.Exception exp)
            {
                Log.Debug("Exception during export report", exp);
                throw exp;
            }

            return outputFileName;
        }

        private void InlineReportSlot_RenderingError(object sender, RenderingErrorEventArgs e)
        {
            Log.Debug("Report Render Error");

            if (e.Exception is ScriptException scriptException)
                Log.Error($"Render ScriptException: {scriptException.Describe(true)}");

            Log.Error("Render Error: ", e.Exception);
            Log.Error("Render Error (innerException): ", e.Exception.InnerException);

            throw new System.Exception("Report Rendering Error", e.Exception);
        }

        public virtual DataSet GenerateDataSet(RunFileReportRequest request)
        {
            Assembly.Load("PerpetuumSoft.Reporting.MSChart");

            string location = System.IO.Path.GetDirectoryName(_Assembly.Location);
            string name = System.IO.Path.GetFileNameWithoutExtension(_Assembly.Location);
            FileVersionInfo fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            Log.InfoFormat("Report plugin folder = {0}, plugin name = {1}, plugin version = {2}", location, name, fileVersionInfo.ProductVersion);

           System.Data.DataSet reportTables = (new Common(request)).GetCommonDataSet(name, location);

            AddReportSpecificTables(reportTables);
            return reportTables;
        }

        public virtual void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
        }
    }
}
