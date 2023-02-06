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
    /// <summary>
    /// Simple class for storing credentials. This version is not meant to be secure in any
    /// way. The Version slot string is unused at the moment but is there in case this implementation
    /// is extended in the future. This object expects a JSON string that looks like:
    /// {
    ///         'Version': '1.0',
    ///         'Data': {
    ///            'credkey1': {'username':'user1','password':'pw1base64'},
    ///            'credkey2': {'username':'user2','password':'pw2base64'},
    ///            ....
    ///            'credkeyN': {'username':'userN','password':'pwNbase64'},
    ///          }
    ///}
    ///
    /// The credkey is simply some unique string that identifies the credential.
    /// The password is assumed to be UTF8 that is base64 encoded.
    ///
    /// </summary>
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

        public string GetDomain(string credId)
        {
            return GetProperty(credId, "domain");
        }

        public string GetUsername(string credId)
        {
            return GetProperty(credId, "username");
        }

        public string GetPassword(string credId)
        {
            var val = GetProperty(credId, "password");
            if (val != null) return Encoding.UTF8.GetString(Convert.FromBase64String(val));
            else return null;
        }


    }


}