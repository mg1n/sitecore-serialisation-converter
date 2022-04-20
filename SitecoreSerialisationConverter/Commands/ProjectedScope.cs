using Sitecore.DevEx.Serialization;

namespace SitecoreSerialisationConverter.Commands
{
    public static class ProjectedScope
    {
        public static TreeScope Get(string childSyncSetting)
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
    }
}
