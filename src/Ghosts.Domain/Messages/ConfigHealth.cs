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
        public string HealthConfigFile;

        public ConfigHealth(string healthConfig)
        {
            HealthConfigFile = healthConfig;
            CheckUrls = new List<string>();
        }

        public List<string> CheckUrls { get; set; }
        public int Sleep { get; set; }

        public FileInfo FilePath()
        {
            return new FileInfo(HealthConfigFile);
        }

        public ConfigHealth Load()
        {
            var raw = File.ReadAllText(HealthConfigFile);
            var obj = JsonConvert.DeserializeObject<ConfigHealth>(raw);
            return obj;
        }
    }
}
