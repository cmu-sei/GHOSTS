// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    [Obsolete("Use Grafana for analytics. This endpoint will be removed in v3")]
    [Authorize(Policy = "ApiUser")]
    [Produces("application/json")]
    [Route("api/Dashboard")]
    [ResponseCache(Duration = 15)]
    public class DashboardController : Controller
    {
        private readonly IReportService _service;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public DashboardController(IReportService service)
        {
            this._service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            log.Trace("Request for dashboard");

            return Ok(await this._service.GetDashboard(ct));
        }
    }
}