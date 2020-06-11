using System;
using System.Net;
using System.Threading;

namespace tools_addmachines
{
    class Program
    {
        static void Main(string[] args)
        {
            var init = Convert.ToInt32(Console.ReadLine());
            var o = init;
            while (o > 0)
            {
                var url = "http://localhost:59460/api/clientid";
                WebRequest req = WebRequest.Create(url);
                req.Headers.Add("name", Guid.NewGuid().ToString());
                req.Headers.Add("fqdn", Guid.NewGuid().ToString());
                req.Headers.Add("ip", "0.0.0.0");
                req.Headers.Add("user", "test user");

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
