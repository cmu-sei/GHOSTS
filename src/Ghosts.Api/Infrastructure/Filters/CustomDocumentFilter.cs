// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ghosts.Api.Infrastructure.Filters;

public class CustomDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var actionDescriptor = context.ApiDescriptions
                    .FirstOrDefault(desc => desc.RelativePath == path.Key.Substring(1))
                    ?.ActionDescriptor;

                if (actionDescriptor != null && actionDescriptor.RouteValues.TryGetValue("action", out var value))
                {
                    operation.Value.OperationId = value;
                }
            }
        }
    }
}
