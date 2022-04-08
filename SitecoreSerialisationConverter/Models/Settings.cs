using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SitecoreSerialisationConverter.Models
{
    public class Settings
    {
        public string ProjectDescription { get; set; } 
        public string SolutionFolder { get; set; }
        public string SavePath { get; set; }
        public bool UseRelativeSavePath { get; set; }
        public string RelativeSavePath { get; set; }
        public IgnoredRoutes IgnoredRoutes { get; set; } = null!;
    }
}
