using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CommandLine;
using ghosts.tools.loadtestercore;
using Newtonsoft.Json;
using RestSharp;

namespace ghosts.tools.loadtestercore
{
    public class TransferLogDump
    {
        public string Log { get; set; }
    }

    internal static class CommandLineFlagManager
    {
        internal static bool Parse(IEnumerable<string> args)
        {
            var options = new OptionFlags();
            var parser = new Parser(with =>
            {
                with.EnableDashDash = true;
                with.CaseSensitive = false;
                with.AutoVersion = false;
                with.IgnoreUnknownArguments = true;
                with.AutoHelp = false;
                with.HelpWriter = null;
            });
            var parserResults = parser
                .ParseArguments<OptionFlags>(args)
                .WithParsed(o => options = o);

            if (!string.IsNullOrEmpty(options.Host))
            {
                options.Host = options.Host.TrimEnd(Convert.ToChar("/"));
                Console.WriteLine($"Host set to {options.Host}");
            }
            else
            {
                options.Host = "http://localhost:5000";
            }

            if (!string.IsNullOrEmpty(options.UpdatesFile))
            {
                Console.WriteLine($"Client updates file set to {options.UpdatesFile}");
            }
            else
            {
                options.UpdatesFile = "clientupdates.log";
            }
            
            Program.Options = options;
            
            return true;
        }
    }
}


public class OptionFlags
{
    [Option('h', "host", Required = false, HelpText = "Set host for GHOSTS C2/API.")]
    public string Host { get; set; }

    [Option('u', "updates_file", Required = false, HelpText = "Set file to upload as client updates")]
    public string UpdatesFile { get; set; }
}

class Program
{
    public static OptionFlags Options { get; set; }

    static void Main(string[] args)
    {
        if (!CommandLineFlagManager.Parse(args))
            return;
        
        RestClient client;
        IRestResponse o;
        string id;
        RestRequest request;

        var commands = new List<string>();
        commands.Add("BrowserChrome");
        commands.Add("BrowserFirefox");
        commands.Add("Word");
        commands.Add("Excel");
        commands.Add("Outlook");
        commands.Add("PowerPoint");
        commands.Add("Clicks");
        commands.Add("BrowserIE");
        var rnd = new Random();

        var i = 0;
        while (true)
        {
            client = new RestClient($"{Program.Options.Host}/api/clientid");
            request = new RestRequest(Method.GET);
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("ghosts-user", "clubber.lang");
            request.AddHeader("ghosts-ip", $"127.1.1.{i}");
            request.AddHeader("ghosts-domain", $"domain-{i}");
            request.AddHeader("ghosts-host", $"host-{i}");
            request.AddHeader("ghosts-resolvedhost", $"resolvedHost.{i}");
            request.AddHeader("ghosts-fqdn", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-name", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-version", "7.0.0.0");
            o = client.Execute(request);
            id = o.Content.Replace("\"", "");

            Console.WriteLine($"Id response was: {id}");

            Thread.Sleep(50);

            var i2 = 30;
            Console.Write($"Results ");
            while (i2 > 0)
            {
                client = new RestClient($"{Options.Host}/api/clientresults");
                request = new RestRequest(Method.POST);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("ghosts-user", "clubber.lang");
                request.AddHeader("ghosts-ip", $"127.1.1.{i}");
                request.AddHeader("ghosts-domain", $"domain-{i}");
                request.AddHeader("ghosts-host", $"host-{i}");
                request.AddHeader("ghosts-resolvedhost", $"resolvedHost.{i}");
                request.AddHeader("ghosts-fqdn", $"flag01.hq.win10.user-test-vpn-{i}");
                request.AddHeader("ghosts-name", $"flag01.hq.win10.user-test-vpn-{i}");
                request.AddHeader("ghosts-version", "2.6.0.0");
                request.AddHeader("ghosts-id", id);

                var r = new TransferLogDump();

                if (!File.Exists(Options.UpdatesFile))
                    File.Create(Options.UpdatesFile);
                
                var data = File.ReadLines(Options.UpdatesFile);

                var sb = new StringBuilder();
                foreach (var d in data)
                {
                    sb.AppendLine(d);
                }

                r.Log = sb.ToString();

                var payload = JsonConvert.SerializeObject(r);

                request.AddParameter("undefined", payload, ParameterType.RequestBody);

                // request.AddParameter("undefined",
                //     "{\r\n\t\"Log\": \"TIMELINE|" + DateTime.UtcNow.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Handler\\\":\\\"" +
                //     commands.PickRandom() +
                //     "\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|" +
                //     DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +
                //     "|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|" +
                //     DateTime.Now.ToString() + "|{\\\"Handler\\\":\\\"" + commands.PickRandom() +
                //     "\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);

                o = client.Execute(request);

                Console.Write($"{i2}, ");
                i2--;

                Thread.Sleep(50);
            }

            client = new RestClient($"{Options.Host}/api/clientresults");
            request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("ghosts-user", "clubber.lang");
            request.AddHeader("ghosts-ip", $"127.1.1.{i}");
            request.AddHeader("ghosts-domain", $"domain-{i}");
            request.AddHeader("ghosts-host", $"host-{i}");
            request.AddHeader("ghosts-resolvedhost", $"resolvedHost.{i}");
            request.AddHeader("ghosts-fqdn", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-name", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-version", "2.6.0.0");
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("undefined",
                "{\"Log\":\"HEALTH|" + DateTime.UtcNow.ToString("MM/dd/yy H:mm:ss tt") +
                "|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":946,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"],\\\"Stats\\\":{\\\"Memory\\\":0.907363832,\\\"Cpu\\\":97.98127,\\\"DiskSpace\\\":0.479912162}}\"}",
                ParameterType.RequestBody);
            o = client.Execute(request);

            Console.WriteLine($"Health response was: {o.ResponseStatus}");
            Thread.Sleep(50);

            client = new RestClient($"{Options.Host}/api/clientupdates");
            request = new RestRequest(Method.GET);
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("ghosts-user", "clubber.lang");
            request.AddHeader("ghosts-ip", $"127.1.1.{i}");
            request.AddHeader("ghosts-domain", $"domain-{i}");
            request.AddHeader("ghosts-host", $"host-{i}");
            request.AddHeader("ghosts-resolvedhost", $"resolvedHost.{i}");
            request.AddHeader("ghosts-fqdn", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-name", $"flag01.hq.win10.user-test-vpn-{i}");
            request.AddHeader("ghosts-version", "2.6.0.0");
            request.AddHeader("ghosts-id", id);
            request.AddParameter("undefined", "{\"Log\":\"\"}", ParameterType.RequestBody);
            o = client.Execute(request);

            Console.WriteLine($"Updates response was: {o.ResponseStatus}");
            Thread.Sleep(500);
            i++;
        }
    }
}

public static class EnumerableExtension
{
    public static T PickRandom<T>(this IEnumerable<T> source)
    {
        return source.PickRandom(1).Single();
    }

    public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
    {
        return source.Shuffle().Take(count);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        return source.OrderBy(x => Guid.NewGuid());
    }
}