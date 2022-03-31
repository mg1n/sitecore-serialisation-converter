using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Sitecore.DevEx.Serialization;
using Sitecore.DevEx.Serialization.Client;
using Sitecore.DevEx.Serialization.Client.Configuration;
using Sitecore.DevEx.Serialization.Client.Datasources.Filesystem.Configuration;
using Sitecore.DevEx.Serialization.Client.Services;
using Sitecore.DevEx.Serialization.Models;
using Sitecore.DevEx.Serialization.Models.Roles;

namespace SitecoreSerialisationConverter
{
    /// <summary>
    /// 1. Install Sitecore Serialise into your environment - https://doc.sitecore.com/xp/en/developers/101/developer-tools/install-sitecore-command-line-interface.html
    /// 2. Download and install CLI to Sitecore - https://dev.sitecore.net/Downloads/Sitecore_CLI.aspx
    /// 3. Login to CLI - https://doc.sitecore.com/xp/en/developers/101/developer-tools/log-in-to-a-sitecore-instance-with-sitecore-command-line-interface.html
    /// . Add new modules to config unless there is a wildcard match for anything.
    /// ...TODO - finish instructions end to end in getting setup
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var solutionFolder = @"C:\CE\SourceCode__Upgrade_102_v2\src\";
            var tdsFiles = Directory.GetFiles(solutionFolder, "*.scproj", SearchOption.AllDirectories);
            string savePath =
                @"C:\Temp\ConvertedSerialisationFiles";

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            foreach(var file in tdsFiles)
            {
                ConvertSerialisationFile(file, savePath);
            }
        }

        private static void ConvertSerialisationFile(string projectPath, string savePath)
        {
            Project project = new Project(projectPath);

            if (project != null)
            {
                var projectName = project.Properties.Where(x => x.Name == "MSBuildProjectName").FirstOrDefault()?.UnevaluatedValue;
                var database = project.Properties.Where(x => x.Name == "SitecoreDatabase").FirstOrDefault()?.UnevaluatedValue;

                SerializationModuleConfiguration newConfigModule = new SerializationModuleConfiguration()
                {
                    Description = "Please complete this!",
                    Namespace = projectName,
                    Items = new SerializationModuleConfigurationItems()
                    {
                        Includes = new List<FilesystemTreeSpec>()
                    }
                };

                var roles = new List<RolePredicateItem>();

                foreach (var item in project.Items)
                {
                    if (item.ItemType == "SitecoreItem")
                    {
                        var includePath = item.Xml.Include;
                        var deploymentType = item.Xml.Metadata.Where(x => x.Name == "ItemDeployment").FirstOrDefault()
                            ?.Value;
                        var childSynchronisation = item.Xml.Metadata.Where(x => x.Name == "ChildItemSynchronization")
                            .FirstOrDefault()
                            ?.Value;

                        FilesystemTreeSpec newSpec = new FilesystemTreeSpec()
                        {
                            Name = GetSafeName(includePath),
                            Path = ItemPath.FromPathString(GetSafePath(includePath)),
                            AllowedPushOperations = GetPushOperation(deploymentType),
                            Scope = GetProjectedScope(childSynchronisation)
                        };

                        //if it's not default then set it.
                        if (database != "master")
                        {
                            newSpec.Database = database;
                        }


                        //set defaults

                        newConfigModule.Items.Includes.Add(newSpec);
                    }
                    else if (item.ItemType == "SitecoreRole")
                    {
                        var includePathSplit = item.Xml.Include?.Split('\\');

                        if (includePathSplit.Length == 3)
                        {
                            var domain = includePathSplit[1];
                            var roleName = includePathSplit[2];

                            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(roleName))
                            {
                                roles.Add(new RolePredicateItem()
                                {
                                    Domain = domain,
                                    Pattern = roleName.Replace(".role", string.Empty)
                                });
                            }
                        }
                    }
                }

                if (roles.Count > 0)
                {
                    newConfigModule.Roles = roles;
                }

                //FilesystemSerializationModuleConfigurationManager configManager =
                //    new FilesystemSerializationModuleConfigurationManager(new DummyLoggerFactory(), new ModuleConfigurationHandler());
                WriteNewConfig(savePath, newConfigModule);
                //Console.WriteLine("test");
            }


        }

        private static void WriteNewConfig(string savePath, SerializationModuleConfiguration moduleConfiguration)
        {
            var path = Path.Combine(savePath, moduleConfiguration.Namespace + ".module.json");
            using (FileStream outputStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter textWriter = new StreamWriter(outputStream))
                {
                    JsonSerializer.Create(_serializerSettings).Serialize(textWriter, moduleConfiguration);
                }
            }
        }

        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new JsonItemPathConverter(),
                new JsonItemPathMatchConverter(),
                new FilesystemTreeSpecRuleConverter(),
                new StringEnumConverter(new CamelCaseNamingStrategy())
            },
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            //if we do want to leave out the defaults.
            //DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private static TreeScope GetProjectedScope(string childSyncSetting)
        {
            if (!string.IsNullOrEmpty(childSyncSetting))
            {
                switch (childSyncSetting)
                {
                    case "NoChildSynchronization":
                        return TreeScope.SingleItem;
                    case "KeepAllChildrenSynchronized":
                        return TreeScope.ItemAndChildren;
                    default:
                        return TreeScope.SingleItem;
                }
            }

            return TreeScope.SingleItem;
        }

        private static AllowedPushOperations GetPushOperation(string deploymentType)
        {
            if (!string.IsNullOrEmpty(deploymentType))
            {
                switch (deploymentType)
                {
                    case "AlwaysUpdate":
                        return AllowedPushOperations.CreateUpdateAndDelete;
                    case "DeployOnce":
                        return AllowedPushOperations.CreateOnly;
                    default:
                        return AllowedPushOperations.CreateAndUpdate;
                }
            }

            return AllowedPushOperations.CreateAndUpdate;
        }

        private static string GetSafePath(string currentPath)
        {
            if (!string.IsNullOrEmpty(currentPath))
            {
                return $"/{currentPath.Replace(@"\", @"/")}";
            }

            return currentPath;
        }

        private static string GetSafeName(string proposedName)
        {
            if (!string.IsNullOrEmpty(proposedName))
            {
                return proposedName.Replace(@"\", "-").Replace(".item", string.Empty);
            }

            return proposedName;
        }
    }

    public class DummyLoggerFactory : ILoggerFactory
    {
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            //throw new NotImplementedException();
            return new DummyLogger();
        }

        public void AddProvider(ILoggerProvider provider)
        {
            //throw new NotImplementedException();
        }
    }

    public class DummyLogger : ILogger, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            //throw new NotImplementedException();

        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
            //throw new NotImplementedException();
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            //throw new NotImplementedException();
            return this;
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }

    public class FilesystemTreeSpecRuleConverter : JsonConverter<TreeSpecRule>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, TreeSpecRule value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override TreeSpecRule ReadJson(JsonReader reader, Type objectType, TreeSpecRule existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            FilesystemTreeSpecRule filesystemTreeSpecRule = new FilesystemTreeSpecRule();
            serializer.Populate(reader, filesystemTreeSpecRule);
            return filesystemTreeSpecRule;
        }
    }
}
