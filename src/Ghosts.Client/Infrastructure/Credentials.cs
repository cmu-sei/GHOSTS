using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using NLog;
using Ghosts.Domain;


namespace Ghosts.Client.Infrastructure
{
    class Credentials
    {

        public string Version { get; set; }
        public Dictionary<string, Dictionary<string, string>> Data { get; set; }


        public string GetProperty(string credId, string prop)
        {
            
            if (this.Data != null && this.Data.ContainsKey(credId))
            {
                if (this.Data[credId].ContainsKey(prop)) return this.Data[credId][prop];
            }
            return null;
        }
    }

       
}
