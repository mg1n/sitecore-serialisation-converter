using Sitecore.DevEx.Serialization.Client;

namespace SitecoreSerialisationConverter.Commands
{
    public static class PushOperation
    {
        public static AllowedPushOperations Get(string deploymentType)
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
    }
}
