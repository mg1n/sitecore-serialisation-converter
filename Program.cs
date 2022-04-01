using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var solutionFolder = @"C:\Projects\Customers\Sitecore\helix-basic-tds\src\";
            var tdsFiles = Directory.GetFiles(solutionFolder, "*.scproj", SearchOption.AllDirectories);
            string savePath = @"C:\Temp\ConvertedSerialisationFiles";

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            foreach (var file in tdsFiles)
            {
                ConvertSerialisationFile(file, savePath);
            }
        }

        private static void ConvertSerialisationFile(string projectPath, string savePath)
        {
            Project project = new Project(projectPath);

            if (project != null)
            {
                var projectName = project.Properties.FirstOrDefault(x => x.Name == "MSBuildProjectName")?.UnevaluatedValue;
                var database = project.Properties.FirstOrDefault(x => x.Name == "SitecoreDatabase")?.UnevaluatedValue;

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

                var ignoreSyncChildren = false;
                var ignoreDirectSyncChildren = false;

                foreach (var item in project.Items)
                {
                    if (item.ItemType == "SitecoreItem")
                    {
                        var includePath = item.Xml.Include;
                        var deploymentType = item.Xml.Metadata.FirstOrDefault(x => x.Name == "ItemDeployment")?.Value;
                        var childSynchronisation = item.Xml.Metadata.FirstOrDefault(x => x.Name == "ChildItemSynchronization")?.Value;

                        RenderItem(database, newConfigModule, ref ignoreSyncChildren, ref ignoreDirectSyncChildren, includePath, deploymentType, childSynchronisation);
                    }
                    else if (item.ItemType == "SitecoreRole")
                    {
                        var includePathSplit = item.Xml.Include?.Split('\\');

                        if (includePathSplit?.Length == 3)
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

                if (!newConfigModule.Items.Includes.Any())
                {
                    return;
                }

                WriteNewConfig(savePath, newConfigModule);
            }
        }

        private static void RenderItem(string database, SerializationModuleConfiguration newConfigModule, ref bool ignoreSyncChildren, ref bool ignoreDirectSyncChildren, string includePath, string deploymentType, string childSynchronisation)
        {
            if (childSynchronisation == "NoChildSynchronization")
            {
                var path = GetSafePath(includePath);

                if (database == "master")
                {
                    var matchedMasterPaths = GetIgnoredMasterRoutes().Where(x => Regex.IsMatch(x, path, RegexOptions.IgnoreCase));

                    if (!matchedMasterPaths.Any())
                    {
                        AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);

                    }
                    else
                    {
                        Console.WriteLine("Master ignored path: " + path);
                    }
                }
                else if (database == "core")
                {
                    var matchedCorePaths = GetIgnoredCoreRoutes().Where(x => Regex.IsMatch(x, path, RegexOptions.IgnoreCase));

                    if (!matchedCorePaths.Any())
                    {
                        AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);


                    }
                    else
                    {
                        Console.WriteLine("Core ignored path: " + path);
                    }
                }
            }

            if (childSynchronisation == "KeepAllChildrenSynchronized" && !ignoreSyncChildren)
            {
                ignoreSyncChildren = true;

                AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);
            }

            if (childSynchronisation != "KeepAllChildrenSynchronized")
            {
                ignoreSyncChildren = false;
            }

            if (childSynchronisation == "KeepDirectDescendantsSynchronized" && !ignoreDirectSyncChildren)
            {
                ignoreDirectSyncChildren = true;

                AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);
            }

            if (childSynchronisation != "KeepDirectDescendantsSynchronized")
            {
                ignoreDirectSyncChildren = false;
            }
        }

        private static List<string> GetIgnoredMasterRoutes()
        {
            List<string> ignoredMasterRoutes = new List<string>()
            {
                "/sitecore/layout",
                "/sitecore/layout/Renderings",
                "/sitecore/layout/Renderings/Foundation",
                "/sitecore/layout/Renderings/Feature",
                "/sitecore/layout/Renderings/Project",
                "/sitecore/templates",
                "/sitecore/templates/Foundation",
                "/sitecore/templates/Feature",
                "/sitecore/templates/Project",
                "/sitecore/layout/Placeholder Settings",
                "/sitecore/layout/Placeholder Settings/Foundation",
                "/sitecore/layout/Placeholder Settings/Feature",
                "/sitecore/layout/Placeholder Settings/Project",
                "/sitecore/media library",
                "/sitecore/system",
                "/sitecore/system/Settings",
                "/sitecore/system/Settings/Rules",
                "/sitecore/system/Settings/Rules/Insert Options",
                "/sitecore/system/Settings/Rules/Insert Options/Rules",
                "/sitecore/templates/Branches",
                "/sitecore/templates/Branches/Foundation/",
                "/sitecore/templates/Branches/Feature/",
                "/sitecore/templates/Branches/Project/",
                "/sitecore/content",
                "/sitecore/layout/Layouts",
                "/sitecore/layout/Layouts/Project"
            };

            return ignoredMasterRoutes;
        }

        private static List<string> GetIgnoredCoreRoutes()
        {
            List<string> ignoredCoreRoutes = new List<string>()
            {
                "/sitecore/content",
                "/sitecore/content/Applications",
                "/sitecore/content/Applications/WebEdit",
                "/sitecore/content/Applications/WebEdit/Custom Experience Buttons"
            };

            return ignoredCoreRoutes;
        }

        private static void AddItem(string database, SerializationModuleConfiguration newConfigModule, string includePath, string deploymentType, string childSynchronisation)
        {
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
                        return TreeScope.ItemAndDescendants;
                    case "KeepDirectDescendantsSynchronized":
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
                        return AllowedPushOperations.CreateOnly;
                }
            }

            return AllowedPushOperations.CreateAndUpdate;
        }

        private static string GetSafePath(string currentPath)
        {
            if (!string.IsNullOrEmpty(currentPath))
            {
                return $"/{currentPath.Replace(@"\", @"/").Replace(".yml", string.Empty)}";
            }

            return currentPath;
        }

        private static string GetSafeName(string proposedName)
        {
            if (!string.IsNullOrEmpty(proposedName))
            {
                return proposedName.Replace(@"\", "-").Replace(".item", string.Empty).Replace(".yml", string.Empty);
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
