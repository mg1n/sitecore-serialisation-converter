# Sitecore TDS serialisation converter

## Sitecore CLI setup
* Install Sitecore Serialise into your environment - https://doc.sitecore.com/xp/en/developers/102/developer-tools/install-sitecore-command-line-interface.html
* Download and install CLI to Sitecore - https://dev.sitecore.net/Downloads/Sitecore_CLI.aspx
* Login to CLI - https://doc.sitecore.com/xp/en/developers/102/developer-tools/log-in-to-a-sitecore-instance-with-sitecore-command-line-interface.html


## SitecoreSerialisationConverter setup
Update the following settings within the appSettings.json file:
* ProjectDescription: Enter your project description.
* SolutionFolder: Enter the path to your solution folder (for example C:/Projects/helix-basic-tds/src/).
* SavePath: If relative save path is not used this is the loaction where module.json files will be exported (C:/Temp/ConvertedSerialisationFiles). This is useful to test file output in a single location.
* UseRelativeSavePath: Default is set to false. Change to true to output files to relative locations with the solution. This assumes convenstions have been followed for TDS folder setup.
* RelativeSavePath: Used if relative save path is true the default value is `../../`. This will output module.json files to modules root directories.


## Module configuration sitecore.json
Configuration will be required to point Sitecore CLI Serialization to the locations of the module.json files.
* If not using a relative path or when testing file output the modules setting in sitecore.json will need to be updated to the location of module.json files for example `SerializationTest/*.module.json`.
* For Helix setup the modules setting within the sitecore.json file should be set as `src/*/*/*.module.json` for example.

