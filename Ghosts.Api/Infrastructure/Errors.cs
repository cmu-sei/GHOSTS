// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ghosts.Api.Infrastructure
{
    public static class Errors
    {
        public static ModelStateDictionary AddErrorToModelState(string code, string description, ModelStateDictionary modelState)
        {
            modelState.TryAddModelError(code, description);
            return modelState;
        }
    }
}
