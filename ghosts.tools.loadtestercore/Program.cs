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
                client = new RestClient("http://localhost:5000/api/clientid");
                request = new RestRequest(Method.GET);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("user", "bobby.tables");
                request.AddHeader("ip", $"1127.9.8.{i}");
                request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                o = client.Execute(request);
                id = o.Content.Replace("\"", "");

                id = "141b6982-1f35-4f6a-9907-8260e10d8a82";

                Thread.Sleep(50);

                client = new RestClient("http://localhost:5000/api/clientresults");
                request = new RestRequest(Method.POST);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("user", "bobby.tables");
                request.AddHeader("ip", $"1127.9.8.{i}");
                request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("id", id);
                request.AddParameter("undefined", "{\r\n\t\"Log\": \"TIMELINE|"+ DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +" PM|{\\\"Handler\\\":\\\""+ commands.PickRandom() +"\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|"+ DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +"|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|"+ DateTime.Now.ToString() +"|{\\\"Handler\\\":\\\""+ commands.PickRandom() +"\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);
                o = client.Execute(request);

                Console.WriteLine($"Response was: {o.ResponseStatus}");
                Thread.Sleep(50);

                client = new RestClient("http://localhost:5000/api/clientresults");
                request = new RestRequest(Method.POST);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("user", "bobby.tables");
                request.AddHeader("ip", $"1127.9.8.{i}");
                request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("id", id);
                request.AddParameter("undefined", "{\r\n\t\"Log\": \"TIMELINE|" + DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Handler\\\":\\\""+ commands.PickRandom() +"\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|" + DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|" + DateTime.Now.ToString() + "|{\\\"Handler\\\":\\\""+ commands.PickRandom() +"\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);
                o = client.Execute(request);

                Console.WriteLine($"Response was: {o.ResponseStatus}");
                Thread.Sleep(50);

                client = new RestClient("http://localhost:5000/api/clientresults");
                request = new RestRequest(Method.POST);
                request.AddHeader("cache-control", "no-cache");
                request.AddHeader("name", "flag02.hq.win10.user-test-vpn-001");
                request.AddHeader("fqdn", "flag02.hq.win10.user-test-vpn-001");
                request.AddHeader("ip", "127.0.0.1");
                request.AddHeader("user", "bobby.tables");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("undefined", "{\"Log\":\"HEALTH|2/6/2019 9:08:30 PM|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":946,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"],\\\"Stats\\\":{\\\"Memory\\\":0.907363832,\\\"Cpu\\\":97.98127,\\\"DiskSpace\\\":0.479912162}}\"}", ParameterType.RequestBody);
                o = client.Execute(request);
                
                Thread.Sleep(50);

                client = new RestClient("http://localhost:5000/api/clientupdates");
                request = new RestRequest(Method.GET);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("user", "bobby.tables");
                request.AddHeader("ip", $"1127.9.8.{i}");
                request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("id", id);
                request.AddParameter("undefined", "{\"Log\":\"\"}", ParameterType.RequestBody);
                o = client.Execute(request);
                
                Console.WriteLine($"Response was: {o.ResponseStatus}");
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