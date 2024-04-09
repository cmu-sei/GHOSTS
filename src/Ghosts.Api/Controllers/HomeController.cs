// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Ghosts.Api.Controllers
{
    [Route("/")]
    public class HomeController : Controller
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

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
        [HttpGet("test")]
        [Produces("application/json")]
        [ResponseCache(Duration = 60)]
        public IActionResult Test()
        {
            var s = new Status();
            s.Version = ApplicationDetails.Version;
            s.VersionFile = ApplicationDetails.VersionFile;
            s.Created = DateTime.UtcNow;

            try
            {
                s.Machines = _context.Machines.Count();
                s.Groups = _context.Groups.Count();
                s.Npcs = _context.Npcs.Count();
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }

            return Json(s);
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