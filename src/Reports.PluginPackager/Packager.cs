using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using ServiceStack;
using ServiceStack.Text;

namespace Reports.PluginPackager
{
    public class Packager
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public void CreatePackage()
        {
            ResolveDefaults();

            CreateOutputPackage();
        }

        private void ResolveDefaults()
        {
            ResolveAssemblyPath();
            ResolveDeployedFolderName();
        }

        private void ResolveAssemblyPath()
        {
            if (!File.Exists(Context.AssemblyPath))
                throw new ExpectedException($"'{Context.AssemblyPath}' does not exist.");

            var jsonPath = Path.ChangeExtension(Context.AssemblyPath, ".json");

            if (!File.Exists(jsonPath))
                throw new ExpectedException($"The required JSON file '{jsonPath}' does not exist.");
        }

        private void ResolveDeployedFolderName()
        {
            if (!string.IsNullOrWhiteSpace(Context.DeployedFolderName))
                return;

            var filename = Path.GetFileNameWithoutExtension(Context.AssemblyPath);

            if (string.IsNullOrEmpty(filename))
                throw new Exception($"Can't parse filename from '{Context.AssemblyPath}'");

            var tidiedName = new Regex(@"(plugin|plug-in)", RegexOptions.IgnoreCase).Replace(filename, string.Empty);

            Context.DeployedFolderName = tidiedName.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private void CreateOutputPackage()
        {
            var directory = Path.GetDirectoryName(Context.OutputPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Log.Info($"Creating '{Context.OutputPath}' ...");

            File.Delete(Context.OutputPath);

            using (var zipArchive = ZipFile.Open(Context.OutputPath, ZipArchiveMode.Create))
            {
                Log.Info("Adding manifest.json ...");
                var manifestEntry = zipArchive.CreateEntry("manifest.json");
                using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    var manifest = CreateManifest();

                    writer.Write(manifest.ToJson().IndentJson());
                }

                // ReSharper disable once AssignNullToNotNullAttribute
                var assemblyFolder = new DirectoryInfo(Path.GetDirectoryName(Context.AssemblyPath));
                var includeRegexes = Context.Include.Select(CreateRegexFromDosPattern).ToList();
                var excludeRegexes = Context.Exclude.Select(CreateRegexFromDosPattern).ToList();

                foreach (var file in assemblyFolder.GetFiles("*", SearchOption.AllDirectories))
                {
                    var filename = file.Name;
                    var relativePath = file.FullName.Substring(assemblyFolder.FullName.Length + 1);

                    if (excludeRegexes.Any(r => r.IsMatch(filename)))
                    {
                        Log.Info($"Excluding '{relativePath}' ...");
                        continue;
                    }

                    if (includeRegexes.Any() && !includeRegexes.Any(r => r.IsMatch(filename)))
                    {
                        Log.Info($"Skipping '{relativePath}' ...");
                        continue;
                    }

                    Log.Info($"Adding '{relativePath}' ...");
                    zipArchive.CreateEntryFromFile(file.FullName, relativePath);
                }
            }

            Log.Info($"Successfully created '{Context.OutputPath}'.");
        }

        private Manifest CreateManifest()
        {
            return new Manifest
            {
                AssemblyName = Path.GetFileNameWithoutExtension(Context.AssemblyPath),
                FolderName = Context.DeployedFolderName
            };
        }

        private static Regex CreateRegexFromDosPattern(string pattern)
        {
            if (pattern.EndsWith(".*"))
                pattern = pattern.Substring(0, pattern.Length - 2);

            pattern = pattern
                .Replace(".", "\\.")
                .Replace("*", ".*");

            return new Regex(pattern, RegexOptions.IgnoreCase);
        }
    }
}
