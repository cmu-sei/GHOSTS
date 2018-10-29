// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Ghosts.Domain
{
    /// <summary>
    /// a client's specific health configuration
    /// </summary>
    public class ConfigHealth
    {
        public List<string> CheckUrls { get; set; }
        public int Sleep { get; set; }

        public string HealthConfigFile;

        public FileInfo FilePath()
        {
            return new FileInfo(HealthConfigFile);
        }

        public ConfigHealth(string healthConfig)
        {
            this.HealthConfigFile = healthConfig;
            this.CheckUrls = new List<string>();
        }

        public ConfigHealth Load()
        {
            var raw = File.ReadAllText(HealthConfigFile);
            var obj = JsonConvert.DeserializeObject<ConfigHealth>(raw);
            return obj;
        }
    }
}
