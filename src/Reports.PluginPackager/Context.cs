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

        public List<string> Exception { get; } = new List<string>();

        public List<string> Exclude { get; } = new List<string>
        {
            "*.xml",
            "*.pdb",
            "*.dll.config",
            "ReportPluginFramework.dll",
            "ServiceStack.*",
            "ComponentFactory.*",
            "PerpetuumSoft.*",
        };
    }
}
