using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RestSharp;

namespace ghosts.tools.loadtestercore
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = "http://localhost:52388";
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                host = args[0].TrimEnd(Convert.ToChar("/"));

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
                client = new RestClient($"{host}/api/clientid");
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
                o = client.Execute(request);
                id = o.Content.Replace("\"", "");

                Console.WriteLine($"Id response was: {id}");

                Thread.Sleep(50);

                var i2 = 30;
                Console.Write($"Results ");
                while (i2 > 0)
                {
                    client = new RestClient($"{host}/api/clientresults");
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
                    request.AddParameter("undefined",
                        "{\r\n\t\"Log\": \"TIMELINE|" + DateTime.UtcNow.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Handler\\\":\\\"" +
                        commands.PickRandom() +
                        "\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|" +
                        DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +
                        "|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|" +
                        DateTime.Now.ToString() + "|{\\\"Handler\\\":\\\"" + commands.PickRandom() +
                        "\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);
                    o = client.Execute(request);

                    Console.Write($"{i2}, ");
                    i2--;

                    Thread.Sleep(50);
                }

                client = new RestClient($"{host}/api/clientresults");
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

                client = new RestClient($"{host}/api/clientupdates");
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
}