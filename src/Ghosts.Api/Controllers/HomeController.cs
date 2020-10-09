// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/home")]
    [ResponseCache(Duration = 60)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API Home, often used to verify API is working correctly
        /// </summary>
        /// <returns>
        /// Basic check information including version number,
        /// and a simple database connection counting machines and groups
        /// </returns>
        [HttpGet]
        public IActionResult Index()
        {
            var s = new Status();
            s.Version = ApiDetails.Version;
            s.Created = DateTime.UtcNow;

            try
            {
                s.Machines = _context.Machines.Count();
                s.Groups = _context.Groups.Count();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return Json(s);
        }

        public class Status
        {
            public string Version { get; set; }
            public int Machines { get; set; }
            public int Groups { get; set; }
            public DateTime Created { get; set; }
        }
    }
}