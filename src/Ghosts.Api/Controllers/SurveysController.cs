// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Services;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Controllers
{
    public class SurveysController : Controller
    {
        private readonly ISurveyService _surveyService;

        public SurveysController(ISurveyService surveyService)
        {
            _surveyService = surveyService;
        }

        [ProducesResponseType(typeof(Survey), 200)]
        [HttpGet("surveys/{machineId}")]
        public async Task<IActionResult> Survey([FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _surveyService.GetLatestAsync(machineId, ct));
        }

        [ProducesResponseType(typeof(IEnumerable<Survey>), 200)]
        [HttpGet("surveys/{machineId}/all")]
        public async Task<IActionResult> SurveyAll([FromRoute] Guid machineId, CancellationToken ct)
        {
            return Ok(await _surveyService.GetAllAsync(machineId, ct));
        }
    }
}