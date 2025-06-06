﻿// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net;
using Ghosts.Domain;

namespace ghosts.client.universal.Health
{
    public static class HealthManager
    {
        public static ResultHealth Check(ConfigHealth config)
        {
            var r = new ResultHealth();

            //check connectivity (internet)
            foreach (var url in config.CheckUrls)
            {
                var request = WebRequest.Create(url);

                var watch = System.Diagnostics.Stopwatch.StartNew();

                //TODO - this only gets running user, there may be other users on the box
                if (!r.LoggedOnUsers.Contains(Environment.UserName))
                    r.LoggedOnUsers.Add(Environment.UserName);

                try
                {
                    using var response = (HttpWebResponse)request.GetResponse();
                    using var stream = response.GetResponseStream();
                    if (stream != null)
                    {
                        // ignore
                        // using (var reader = new StreamReader(stream))
                        // {
                        //     //html = reader.ReadToEnd(); //if we wanted to read html
                        // }
                    }
                }
                catch (WebException e)
                {
                    r.Errors.Add($"Connection error - web exception: {e.Message} to {request.RequestUri}");
                }
                catch (Exception e)
                {
                    r.Errors.Add($"Connection error - general exception: {e.Message} to {request.RequestUri}");
                }

                watch.Stop();

                r.ExecutionTime = watch.ElapsedMilliseconds;
                r.Internet = r.Errors.Count == 0;
                r.Stats = MachineHealth.Run();
            }

            return r;
        }
    }
}
