using System;
using System.Collections.Generic;
using System.IO;
using Ghosts.Client.Universal.Infrastructure.Email;
using Ghosts.Domain.Code;
using Xunit;

namespace Ghosts.Client.Universal.Tests;

[Collection("Email footer configuration")]
public class EmailFooterTests
{
    [Fact]
    public void RandomBodyUsesFooterFileAndOmitsFooterWhenMissing()
    {
        var path = ApplicationDetails.ConfigurationFiles.EmailsFooter;
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var property = typeof(EmailConfiguration).Assembly.GetType("Ghosts.Client.Universal.Program")!
            .GetProperty("Configuration", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var configuration = property.GetValue(null);
        try
        {
            property.SetValue(null, new ClientConfiguration { Email = new ClientConfiguration.EmailSettings(), EmailContent = new Dictionary<string, string>() });
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "Regards {{from}} / {{id}} / {{now}}");
            var args = new List<object> { "eroberts@jarn.gov.au", "", "", "", "Subject", "random", "PlainText", "" };
            var email = new EmailConfiguration(args);
            Assert.Contains("Regards eroberts@jarn.gov.au / " + email.Id + " / ", email.Body);
            Assert.DoesNotContain("{{", email.Body);
            Assert.DoesNotContain("DoD Privacy Act", email.Body);
            File.WriteAllText(path, "Updated footer");
            Assert.EndsWith("Updated footer", new EmailConfiguration(args).Body);
            File.WriteAllText(path, "");
            var bodyWithoutFooter = new EmailConfiguration(args).Body;
            Assert.DoesNotContain("CONFIDENTIALITY NOTICE", bodyWithoutFooter ?? "");
            File.Delete(path);
            Assert.Equal(bodyWithoutFooter, new EmailConfiguration(args).Body);
            args[5] = "Explicit body";
            Assert.Equal("Explicit body", new EmailConfiguration(args).Body);
        }
        finally
        {
            property.SetValue(null, configuration);
            if (original == null) File.Delete(path);
            else File.WriteAllBytes(path, original);
        }
    }
}


[CollectionDefinition("Email footer configuration", DisableParallelization = true)]
public class EmailFooterCollection { }
