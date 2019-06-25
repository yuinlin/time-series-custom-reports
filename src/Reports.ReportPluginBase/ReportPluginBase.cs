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
    public abstract class ReportPluginBase
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Assembly _Assembly = Assembly.GetExecutingAssembly();

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

            Log.DebugFormat("GenerateReport - the name of file for export report to write to is set to {0}", tempFileName);

            try
            {
                switch (outputFormat.ToUpper())
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
            }
            catch (System.Exception exp)
            {
                Log.Debug("Exception during export report", exp);
                throw exp;
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
            Assembly.Load("PerpetuumSoft.Reporting.MSChart");

            string location = System.IO.Path.GetDirectoryName(_Assembly.Location);
            string name = System.IO.Path.GetFileNameWithoutExtension(_Assembly.Location);

            System.Data.DataSet reportTables = (new Common(request)).GetCommonDataSet(name, location);

            AddReportSpecificTables(reportTables);
            return reportTables;
        }
        public virtual void AddReportSpecificTables(System.Data.DataSet dataSet)
        {
        }
    }
}
