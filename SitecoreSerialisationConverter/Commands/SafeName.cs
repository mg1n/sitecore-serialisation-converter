namespace SitecoreSerialisationConverter.Commands
{
    public static class SafeName
    {
        public static string Get(string proposedName)
        {
            if (!string.IsNullOrEmpty(proposedName))
            {
                return proposedName.Replace(@"\", "-").Replace(@" ", "-").Replace(".item", string.Empty).Replace(".yml", string.Empty);
            }

            return proposedName;
        }
    }
}
