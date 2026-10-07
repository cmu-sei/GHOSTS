// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ghosts.Api.Infrastructure
{
    /// <summary>
    /// For controllers routed under api/scenarios/{scenarioId}/...: a scenario that is not shown to the caller,
    /// another author's draft or no scenario at all, is 404 before any action runs (I2, J7). Like
    /// <see cref="CurrentUser"/>, this decides what is shown, not what is allowed.
    /// </summary>
    public class ScenarioVisibilityFilter(IScenarioService scenarios, CurrentUser user) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.RouteData.Values.TryGetValue("scenarioId", out var value)
                && int.TryParse(value?.ToString(), out var scenarioId)
                && !await scenarios.IsVisibleAsync(scenarioId, user.Name, context.HttpContext.RequestAborted))
            {
                context.Result = new NotFoundResult();
                return;
            }

            await next();
        }
    }
}
