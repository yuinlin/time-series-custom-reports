using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;

namespace Reports.PluginPackager
{
    public class Program
    {
        private static ILog _log;

        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            try
            {
                ConfigureLogging();

                var context = ParseArgs(args);
                new Program(context).Run();

                Environment.ExitCode = 0;
            }
            catch (ExpectedException exception)
            {
                _log.Error(exception.Message);
            }
            catch (ReflectionTypeLoadException exception)
            {
                _log.Error($"Reflection load errors: {string.Join("\n", exception.LoaderExceptions.Select(e => e.Message))}", exception);
            }
            catch (Exception exception)
            {
                _log.Error("Unhandled exception", exception);
            }
        }

        private static void ConfigureLogging()
        {
            using (var stream = new MemoryStream(LoadEmbeddedResource("log4net.config")))
            using (var reader = new StreamReader(stream))
            {
                var xml = new XmlDocument();
                xml.LoadXml(reader.ReadToEnd());

                log4net.Config.XmlConfigurator.Configure(xml.DocumentElement);

                _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            }
        }

        private static byte[] LoadEmbeddedResource(string path)
        {
            var resourceName = $"{GetProgramName()}.{path}";

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new ExpectedException($"Can't load '{resourceName}' as embedded resource.");

                using (var binaryReader = new BinaryReader(stream))
                {
                    return binaryReader.ReadBytes((int)stream.Length);
                }
            }
        }

        private static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        }

        private static Context ParseArgs(string[] args)
        {
            var context = new Context();

            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            var options = new[]
            {
                new Option {Key = nameof(context.AssemblyPath), Setter = value => context.AssemblyPath = value, Getter = () => context.AssemblyPath, Description = "Path to the plugin assembly."},
                new Option {Key = nameof(context.OutputPath), Setter = value => context.OutputPath = value, Getter = () => context.OutputPath, Description = "Path to packaged output file, usually with a '.report' extension"},
                new Option {Key = nameof(context.DeployedFolderName), Setter = value => context.DeployedFolderName = value, Getter = () => context.DeployedFolderName, Description = "Name of the deployed folder"},
                new Option {Key = nameof(context.Subfolders), Setter = value => context.Subfolders = bool.Parse(value), Getter = () => context.Subfolders.ToString(), Description = "Include all subfolders"},
                new Option {Key = nameof(context.Include), Setter = value => AddToList(value, context.Include), Getter = () => string.Join(", ", context.Include), Description = "Include file or DOS wildcard pattern"},
                new Option {Key = nameof(context.Exclude), Setter = value => AddToList(value, context.Exclude), Getter = () => string.Join(", ", context.Exclude), Description = "Exclude file or DOS wildcard pattern"},
                new Option {Key = nameof(context.Exception), Setter = value => AddToList(value, context.Exception), Getter = () => string.Join(", ", context.Exception), Description = "Exception to exclude list"},
            };

            var usageMessage
                    = $"Package a report plugin into a deployable .report file."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] pluginFolderOrAssembly"
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (File.Exists(arg))
                    {
                        context.AssemblyPath = arg;
                        continue;
                    }

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{usageMessage}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrEmpty(context.AssemblyPath))
                throw new ExpectedException($"You must specify the /{nameof(context.AssemblyPath)} option.");

            if (string.IsNullOrEmpty(context.OutputPath))
                throw new ExpectedException($"You must specify the /{nameof(context.OutputPath)} option.");

            return context;
        }

        private static IEnumerable<string> ResolveOptionsFromFile(string arg)
        {
            if (!arg.StartsWith("@"))
                return new[] { arg };

            var path = arg.Substring(1);

            if (!File.Exists(path))
                throw new ExpectedException($"Options file '{path}' does not exist.");

            return File.ReadAllLines(path)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !s.StartsWith("#") && !s.StartsWith("//"));
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private static void AddToList(string value, List<string> values)
        {
            if (value.Equals("~"))
            {
                values.Clear();
            }
            else
            {
                values.Add(value);
            }
        }

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new Packager
            {
                Context = _context
            }.CreatePackage();
        }
    }
}
