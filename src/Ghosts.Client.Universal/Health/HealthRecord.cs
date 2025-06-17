// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Ghosts.Domain;

namespace Ghosts.Client.Universal.Health;

public static class HealthManager
{
    public static async Task<ResultHealth> Check(ConfigHealth config)
    {
        var r = new ResultHealth();
        var client = new HttpClient();

        foreach (var url in config.CheckUrls)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (!r.LoggedOnUsers.Contains(Environment.UserName))
                r.LoggedOnUsers.Add(Environment.UserName);

            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    r.Errors.Add(
                        $"Connection error - status code: {(int)response.StatusCode} ({response.ReasonPhrase}) to {url}");
                }
                // else
                // {
                //     // inspect response.Content.Headers or html if needed?
                // }
            }
            catch (HttpRequestException e)
            {
                r.Errors.Add($"Connection error - HTTP request exception: {e.Message} to {url}");
            }
            catch (Exception e)
            {
                r.Errors.Add($"Connection error - general exception: {e.Message} to {url}");
            }

            watch.Stop();

            r.ExecutionTime = watch.ElapsedMilliseconds;
            r.Internet = r.Errors.Count == 0;
            r.Stats = MachineHealth.Run();
        }

        return r;
    }
}
