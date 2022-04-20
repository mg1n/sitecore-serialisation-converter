namespace SitecoreSerialisationConverter.Commands
{
    public static class SafePath
    {
        public static string Get(string currentPath)
        {
            if (!string.IsNullOrEmpty(currentPath))
            {
                return $"/{currentPath.Replace(@"\", @"/").Replace(".item", string.Empty).Replace(".yml", string.Empty)}";
            }

            return currentPath;
        }
    }
}
