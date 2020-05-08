// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Ghosts.Api.Infrastructure
{
    public static class WebRequestReader
    {
        public static Machine GetMachine(HttpContext context)
        {
            try
            {
                var m = new Machine
                {
                    Name = context.Request.Headers["ghosts-name"],
                    FQDN = context.Request.Headers["ghosts-fqdn"],
                    Host = context.Request.Headers["ghosts-host"],
                    Domain = context.Request.Headers["ghosts-domain"],
                    ResolvedHost = context.Request.Headers["ghosts-resolvedhost"],
                    HostIp = context.Request.Headers["ghosts-ip"],
                    CurrentUsername = context.Request.Headers["ghosts-user"],
                    ClientVersion = context.Request.Headers["ghosts-version"],
                    IPAddress = context.Connection.RemoteIpAddress.ToString(),
                    StatusUp = Machine.UpDownStatus.Up
                };

                m.Name = m.Name.ToLower();
                m.FQDN = m.FQDN.ToLower();
                m.Host = m.Host.ToLower();
                m.ResolvedHost = m.ResolvedHost.ToLower();

                return m;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}