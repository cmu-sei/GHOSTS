// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure.Email;

public static class EmailListManager
{
    public static List<string> GetDomainList()
    {
        var fileName = ClientConfigurationResolver.EmailDomain;

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
                var serializer = new JsonSerializer {Formatting = Formatting.Indented};
                serializer.Serialize(file, emails);
                return emails;
            }
        }

        var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(fileName));
        return list;
    }
        
    public static List<string> GetOutsideList()
    {
        var fileName = ClientConfigurationResolver.EmailOutside;

        if (!File.Exists(fileName))
            throw new FileNotFoundException($"Email outside list not found at {fileName}");

        var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(fileName));
        return list;
    }
}