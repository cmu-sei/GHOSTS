using System;
using System.Net;
using System.Threading;

namespace tools_addmachines
{
    class Program
    {
        static void Main(string[] args)
        {
            var random = new Random();
            var init = Convert.ToInt32(Console.ReadLine());
            var o = init;
            while (o > 0)
            {
                var url = "http://localhost:5000/api/clientid";
                WebRequest req = WebRequest.Create(url);
                req.Headers.Add("ghosts-name", "ted");
                req.Headers.Add("ghosts-fqdn", "fqdn");
                req.Headers.Add("ghosts-user", "test user");

                req.Headers.Add("ghosts-host", "host");
                req.Headers.Add("ghosts-domain", "domain");
                req.Headers.Add("ghosts-resolvedhost", "localhost");
                req.Headers.Add("ghosts-ip", $"192.168.0.{random.Next(1, 255)}");
                req.Headers.Add("ghosts-version", "8.0");

                WebResponse resp = req.GetResponse();
                System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream());

                Console.WriteLine(sr.ReadToEnd().Trim());

                o--;

                Thread.Sleep(500);
            }

            Console.WriteLine($"{init} machines created via the api");
        }
    }
}
