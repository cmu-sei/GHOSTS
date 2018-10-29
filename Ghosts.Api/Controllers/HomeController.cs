// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Api.Code;
using Ghosts.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/Home")]
    [ResponseCache(Duration = 60)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API Home
        /// </summary>
        /// <returns>Basic check information including version number, and a simple database connection counting machines and groups</returns>
        [HttpGet]
        public IActionResult Index()
        {
            var s = new Status();
            s.Version = ApiDetails.Version;

            try
            {
                s.Machines = this._context.Machines.Count();
                s.Groups = this._context.Groups.Count();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return Json(s);
        }

        //[HttpGet("reset")]
        //public IActionResult Reset([FromQuery(Name = "n")] int n)
        //{
        //    ApplicationCleanUp.Run(_context, n);
        //    return Ok($"Reset complete, retaining {n}");
        //}


        [HttpGet("error")]
        public IActionResult Error()
        {
            throw new NotImplementedException("Error controller is not complete");
        }

        public class Status
        {
            public string Version { get; set; }
            public int Machines { get; set; }
            public int Groups { get; set; }
        }
    }
}