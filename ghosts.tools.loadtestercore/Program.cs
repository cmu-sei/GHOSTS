using System;
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

            var i = 0;
            while (true)
            {
                //client = new RestClient("http://localhost:59460/api/clientid");
                //request = new RestRequest(Method.GET);
                //request.AddHeader("Cache-Control", "no-cache");
                //request.AddHeader("Content-Type", "application/json");
                //request.AddHeader("user", "bobby.tables");
                //request.AddHeader("ip", $"1127.9.8.{i}");
                //request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                //request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                //o = client.Execute(request);
                //id = o.Content.Replace("\"", "");

                id = "141b6982-1f35-4f6a-9907-8260e10d8a82";

                Thread.Sleep(50);

                client = new RestClient("http://localhost:5000/api/clientresults");
                request = new RestRequest(Method.POST);
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Content-Type", "application/json");
                //request.AddHeader("user", "bobby.tables");
                //request.AddHeader("ip", $"1127.9.8.{i}");
                //request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                //request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                request.AddHeader("id", id);
                request.AddParameter("undefined", "{\r\n\t\"Log\": \"TIMELINE|"+ DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +" PM|{\\\"Handler\\\":\\\"BrowserChrome\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|"+ DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") +"|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|"+ DateTime.Now.ToString() +"|{\\\"Handler\\\":\\\"BrowserChrome\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);
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
                //request.AddHeader("id", id);
                request.AddParameter("undefined", "{\r\n\t\"Log\": \"TIMELINE|" + DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Handler\\\":\\\"BrowserChrome\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"https:\\/\\/nec-hr.region.army.mil\\\"}\\r\\nHEALTH|" + DateTime.Now.ToString("MM/dd/yy H:mm:ss tt") + "|{\\\"Internet\\\":true,\\\"Permissions\\\":false,\\\"ExecutionTime\\\":101,\\\"Errors\\\":[],\\\"LoggedOnUsers\\\":[\\\"Dustin\\\"]}\\r\\nTIMELINE|" + DateTime.Now.ToString() + "|{\\\"Handler\\\":\\\"BrowserChrome\\\",\\\"Command\\\":\\\"random\\\",\\\"CommandArg\\\":\\\"http:\\/\\/www.dma.mil\\\"}\"\r\n}", ParameterType.RequestBody);
                o = client.Execute(request);

                Console.WriteLine($"Response was: {o.ResponseStatus}");
                Thread.Sleep(50);

                //client = new RestClient("http://localhost:59460/api/clientupdates");
                //request = new RestRequest(Method.GET);
                //request.AddHeader("Cache-Control", "no-cache");
                //request.AddHeader("Content-Type", "application/json");
                ////request.AddHeader("user", "bobby.tables");
                ////request.AddHeader("ip", $"1127.9.8.{i}");
                ////request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                ////request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                //request.AddHeader("id", id);
                ////request.AddParameter("undefined", "{\"Log\":\"\"}", ParameterType.RequestBody);
                //o = client.Execute(request);

                //Thread.Sleep(50);

                //client = new RestClient("http://localhost:59460/api/clientupdates");
                //request = new RestRequest(Method.GET);
                //request.AddHeader("Cache-Control", "no-cache");
                //request.AddHeader("Content-Type", "application/json");
                //request.AddHeader("user", "bobby.tables");
                //request.AddHeader("ip", $"1127.9.8.{i}");
                //request.AddHeader("fqdn", $"flag02.hq.win10.user-test-vpn-{i}");
                //request.AddHeader("name", $"flag02.hq.win10.user-test-vpn-{i}");
                ////request.AddHeader("id", id);
                ////request.AddParameter("undefined", "{\"Log\":\"\"}", ParameterType.RequestBody);
                //o = client.Execute(request);
                
                //Console.WriteLine($"Response was: {o.ResponseStatus}");
                //Thread.Sleep(500);
                i++;
            }
        }
    }
}