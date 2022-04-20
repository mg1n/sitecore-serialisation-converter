using SitecoreSerialisationConverter.Models;
using System.Collections.Generic;

namespace SitecoreSerialisationConverter.Commands
{
    public static class GetIgnoredRoutes
    {
        public static List<string> Master(Settings settings)
        {
            List<string> ignoredMasterRoutes = settings.IgnoredRoutes.Master;

            return ignoredMasterRoutes;
        }

        public static List<string> Core(Settings settings)
        {
            List<string> ignoredCoreRoutes = settings.IgnoredRoutes.Core;

            return ignoredCoreRoutes;
        }
    }
}
