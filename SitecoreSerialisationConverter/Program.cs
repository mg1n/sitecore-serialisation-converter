using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Sitecore.DevEx.Serialization;
using Sitecore.DevEx.Serialization.Client;
using Sitecore.DevEx.Serialization.Client.Configuration;
using Sitecore.DevEx.Serialization.Client.Datasources.Filesystem.Configuration;
using Sitecore.DevEx.Serialization.Models;
using Sitecore.DevEx.Serialization.Models.Roles;
using SitecoreSerialisationConverter.Commands;
using SitecoreSerialisationConverter.Models;
using log4net.Config;

namespace SitecoreSerialisationConverter
{
    class Program
    {
        public static Settings Settings;
        public static List<AliasItem> AliasList;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static int ignoredMasterItemsCount { get; set;}
        public static int ignoredCoreItemsCount { get; set;}
        public static int missingIncludePathsCount { get; set;}
        public static int errorCount { get; set; }
        public static List<string> jsonFilePathList { get; set; }

        static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            XmlConfigurator.Configure(new FileInfo("log4net.config"));

            log.Info("*** Starting SitecoreSerialisationConverter... ***");

            Settings = config.GetRequiredSection("Settings").Get<Settings>();

            var solutionFolder = Settings.SolutionFolder;
            var tdsFiles = Directory.GetFiles(solutionFolder, "*.scproj", SearchOption.AllDirectories);
            var savePath = Settings.SavePath;
            bool useRelativeSavePath = Settings.UseRelativeSavePath;
            var relativeSavePath = Settings.RelativeSavePath;
            bool stripTDSFromName = Settings.StripTDSFromName;
            var projectNameToMatch = Settings.ProjectNameToMatch;

            //reset counters and json file list
            ignoredMasterItemsCount = 0;
            ignoredCoreItemsCount = 0;
            missingIncludePathsCount = 0;
            errorCount = 0;
            jsonFilePathList = new List<string>();

            if (savePath!=null && !string.IsNullOrEmpty(savePath) && !Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            foreach (var file in tdsFiles)
            {
                if (file.Contains(projectNameToMatch) || string.IsNullOrEmpty(projectNameToMatch)) {
                    ConvertSerialisationFile(file, savePath, useRelativeSavePath, relativeSavePath, stripTDSFromName); 
                }
                else
                {
                    log.Info($"Skipping Project: {file} ... as doesn't match ProjectName");
                }
            }

            log.Info(Environment.NewLine);
            log.Info($"*** Completed SitecoreSerialisationConverter ***");
            log.Info(Environment.NewLine);
            log.Info($"    JSON Files Created: {jsonFilePathList.Count}");
            log.Info($"    Ignored Master Items: {ignoredMasterItemsCount}");
            log.Info($"    Ignored Core Items: {ignoredCoreItemsCount}");
            log.Info($"    Missing Include Paths: {missingIncludePathsCount}");
            log.Info($"    Errors: {errorCount}");
            log.Info(Environment.NewLine);
            log.Info($"    JSON Files List:");
            log.Info("-------------------------");
            var jsonFiles = string.Join(",", jsonFilePathList);
            jsonFiles = jsonFiles.Replace(Path.GetDirectoryName(solutionFolder), "\".").Replace("\\","/");
            log.Info(jsonFiles.Replace(",", "\",\n"));
        }

        private static void ConvertSerialisationFile(string projectPath, string savePath, bool useRelativeSavePath, string relativeSavePath, bool stripTDSFromName)
        {
            XDocument project = XDocument.Load(projectPath);
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            if (project != null)
            {
                if (useRelativeSavePath)
                {
                    savePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath), @relativeSavePath));
                }

                AliasList = new List<AliasItem>();
                string projectName = project.Descendants(msbuild + "RootNamespace").Select(x => x.Value).FirstOrDefault();
                string database = project.Descendants(msbuild + "SitecoreDatabase").Select(x => x.Value).FirstOrDefault();

                if (stripTDSFromName)
                {
                    projectName = $"{projectName.Replace(".TDS.", ".")}";
                }

                log.Info(Environment.NewLine);
                log.Info($"---* Processing Project: {projectName} (database: {database}) *---");

                SerializationModuleConfiguration newConfigModule = new SerializationModuleConfiguration()
                {
                    Description = Settings.ProjectDescription,
                    Namespace = projectName,
                    Items = new SerializationModuleConfigurationItems()
                    {
                        Includes = new List<FilesystemTreeSpec>()
                    }
                };


                var moduleFileName = $"{newConfigModule.Namespace}.module.json";
                if (stripTDSFromName)
                {
                    moduleFileName = $"{moduleFileName.Replace(".TDS.", ".")}";
                }

                //check if json module already exists and skip if it does
                var jsonModuleName = $"{savePath}{moduleFileName}";
                jsonModuleName = jsonModuleName.Replace("\\serialization", "");

                if (File.Exists(jsonModuleName) && Settings.SkipCreateIfExists)
                {
                    log.Warn($"     Module File already exists: {jsonModuleName} - skipping.");
                    return;
                }
                else
                {
                    log.Warn($"     Building Module file: {jsonModuleName} ...");
                }

                var roles = new List<RolePredicateItem>();

                var ignoreSyncChildren = false;
                var ignoreDirectSyncChildren = false;

                foreach (var sitecoreItem in project.Descendants(msbuild + "SitecoreItem"))
                {
                    try
                    {
                        var includePath = sitecoreItem?.Attribute("Include")?.Value;

                        log.Debug($"   - Processing Item: {includePath}");

                        if (includePath != null && !string.IsNullOrEmpty(includePath))
                        {
                            var deploymentType = sitecoreItem.Descendants(msbuild + "ItemDeployment").Select(x => x.Value).FirstOrDefault();
                            var childSynchronisation = sitecoreItem.Descendants(msbuild + "ChildItemSynchronization").Select(x => x.Value).FirstOrDefault();
                            var sitecoreName = sitecoreItem.Descendants(msbuild + "SitecoreName").Select(x => x.Value).FirstOrDefault();

                            //SitecoreName property is optional
                            if (!string.IsNullOrEmpty(sitecoreName))
                            {
                                AliasItem aliasItem = new AliasItem()
                                {
                                    AliasName = Path.GetFileNameWithoutExtension(includePath),
                                    SitecoreName = sitecoreName
                                };

                                AliasList.Add(aliasItem);
                            }

                            RenderItem(database, newConfigModule, ref ignoreSyncChildren, ref ignoreDirectSyncChildren, includePath, deploymentType, childSynchronisation, jsonModuleName);
                        }
                        else
                        {
                            log.Warn("     Include path is empty, skipping item.");
                            missingIncludePathsCount += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"     Error processing item: {sitecoreItem?.Attribute("Include")?.Value}");
                        log.Error(ex.Message);
                        errorCount += 1;
                    }   
                }

                foreach (var sitecoreRole in project.Descendants(msbuild + "SitecoreRole"))
                {
                    log.Debug($"   - Processing Role: {sitecoreRole.Attribute("Include")?.Value}");

                    var includePathSplit = sitecoreRole.Attribute("Include")?.Value?.Split('\\');

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

                if (roles.Count > 0)
                {
                    newConfigModule.Roles = roles;
                }

                if (newConfigModule.Items.Includes.Any() || newConfigModule.Roles.Any())
                {
                    WriteNewConfig(savePath, newConfigModule, stripTDSFromName, jsonModuleName);
                }
                else
                {
                    log.Warn($"     No Items or Roles to add - skipping creation of json module file.");
                }
            }
            else
            {
                log.Error($"     Failed to load project file: {projectPath}");
            }
        }

        private static void RenderItem(string database, SerializationModuleConfiguration newConfigModule, ref bool ignoreSyncChildren, ref bool ignoreDirectSyncChildren, string includePath, string deploymentType, string childSynchronisation, string jsonModuleName)
        {
            //if database is not set then default to master
            if(string.IsNullOrEmpty(database))
            {
                if (jsonModuleName.ToLower().Contains("master"))
                {
                    database = "master";
                }
                else if (jsonModuleName.ToLower().Contains("core"))
                {
                    database = "core";
                }
            }

            if (!string.IsNullOrEmpty(database)) {
                if (childSynchronisation == "NoChildSynchronization")
                {
                    var path = SafePath.Get(includePath);

                    if (database == "master")
                    {
                        var matchedMasterPaths = GetIgnoredRoutes.Master(Settings).Where(x => Regex.IsMatch(x, path, RegexOptions.IgnoreCase));

                        if (!matchedMasterPaths.Any())
                        {
                            AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);
                        }
                        else
                        {
                            log.Warn($"     Path is in ignore list - Ignoring Master Item: {includePath}");
                            ignoredMasterItemsCount += 1;
                        }
                    }
                    else if (database == "core")
                    {
                        var matchedCorePaths = GetIgnoredRoutes.Core(Settings).Where(x => Regex.IsMatch(x, path, RegexOptions.IgnoreCase));

                        if (!matchedCorePaths.Any())
                        {
                            AddItem(database, newConfigModule, includePath, deploymentType, childSynchronisation);
                        }
                        else
                        {
                            log.Warn($"     Path is in ignore list - Ignoring Core Item: {includePath}");
                            ignoredCoreItemsCount += 1;
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
            else
            {
                log.Error($"     Database empty: {includePath}");
            }  
        }

        private static void AddItem(string database, SerializationModuleConfiguration newConfigModule, string includePath, string deploymentType, string childSynchronisation)
        {
            includePath = PathAlias.Remove(includePath, AliasList);

            FilesystemTreeSpec newSpec = new FilesystemTreeSpec()
            {
                Name = SafeName.Get(includePath),
                Path = ItemPath.FromPathString(SafePath.Get(includePath)),
                AllowedPushOperations = PushOperation.Get(deploymentType),
                Scope = ProjectedScope.Get(childSynchronisation)
            };
            
            //if it's not default then set it.
            if (database != "master")
            {
                newSpec.Database = database;
            }

            //set defaults
            newConfigModule.Items.Includes.Add(newSpec);
        }

        private static void WriteNewConfig(string savePath, SerializationModuleConfiguration moduleConfiguration, bool stripTDSFromName, string jsonModuleName)
        {
            var path = Path.Combine(savePath, jsonModuleName);
            jsonFilePathList.Add(path);

            using (FileStream outputStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter textWriter = new StreamWriter(outputStream))
                {
                    JsonSerializer.Create(_serializerSettings).Serialize(textWriter, moduleConfiguration);
                }
            }

            log.Info($"---* Created {jsonModuleName} ***");
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
            Formatting = Newtonsoft.Json.Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            //if we do want to leave out the defaults.
            //DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
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
