// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Code.Email
{
    public class EmailAddresses
    {
        public List<string> Emails { get; set; }

        public EmailAddresses()
        {
            this.Emails = new List<string>();
        }
    }

    public static class EmailListManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static List<string> GetDomainList()
        {
            var fileName = ApplicationDetails.ConfigurationFiles.EmailsDomain;

            if (!File.Exists(fileName))
            {
                //generate file
                var ps1 = new PowerShellCommands();
                var emails = ps1.GetDomainEmailAddresses();
                if (!emails.Any())
                    throw new FileNotFoundException("Email list could not be generated");

                //save to local disk
                using (var file = File.CreateText(fileName))
                {
                    var serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(file, emails);
                    return emails;
                }
            }

            var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(fileName));
            return list;
        }
        
        public static List<string> GetOutsideList()
        {
            var fileName = ApplicationDetails.ConfigurationFiles.EmailsOutside;

            if (!File.Exists(fileName))
                throw new FileNotFoundException($"Email outside list not found at {fileName}");

            var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(fileName));
            return list;
        }
    }
}
