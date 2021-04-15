using System.Collections.Generic;

namespace Reports.PluginPackager
{
    public class Context
    {
        public string AssemblyPath { get; set; }
        public string OutputPath { get; set; }
        public string DeployedFolderName { get; set; }
        public bool Subfolders { get; set; } = true;
        public List<string> Include { get; } = new List<string> {"*.*"};

        public List<string> ForceInclude { get; } = new List<string>
        {
            "PerpetuumSoft.Reporting.Export.Csv.dll",
            "PerpetuumSoft.Reporting.Export.OpenXML.dll"
        };

        public List<string> Exclude { get; } = new List<string>
        {
            "*.xml",
            "*.pdb",
            "*.dll.config",
            "ReportPluginFramework.dll",
            "Server.Services.PublishService.ServiceModel.dll",
            "ServiceStack.*",
            "NodaTime.*",
            "ComponentFactory.*",
            "PerpetuumSoft.*",
            "NewtonSoft.*",
            "System.*",
            "Microsoft.*"
        };
    }
}
