// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers
{
    [Route("/")]
    public class HomeController(ApplicationDbContext context) : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// API Home, often used to verify API is working correctly
        /// </summary>
        /// <returns>
        /// Basic check information including version number,
        /// and a simple database connection counting machines and groups
        /// </returns>
        [Produces("application/json")]
        [ResponseCache(Duration = 60)]
        [SwaggerOperation("HomeTestApi")]
        [HttpGet("test")]
        public IActionResult Test()
        {
            var status = new Status
            {
                Version = ApplicationDetails.Version,
                VersionFile = ApplicationDetails.VersionFile,
                Created = DateTime.UtcNow
            };

            try
            {
                status.Machines = _context.Machines.Count();
                status.Groups = _context.Groups.Count();
                status.Npcs = _context.Npcs.Count();
            }
            catch (Exception e)
            {
                _log.Error(e, "An error occurred while counting database entities.");
                return StatusCode(500, "Internal server error");
            }

            return Json(status);
        }

        public class Status
        {
            public string Version { get; set; }
            public string VersionFile { get; set; }
            public int Machines { get; set; }
            public int Groups { get; set; }
            public int Npcs { get; set; }
            public DateTime Created { get; set; }
        }
    }
}
