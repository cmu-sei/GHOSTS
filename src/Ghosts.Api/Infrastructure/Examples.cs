// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Infrastructure;

public class MachineUpdateExample(IServiceProvider serviceProvider) : IExamplesProvider<MachineUpdate>
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public MachineUpdate GetExamples()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var machineId = context.Machines
            .Select(m => (Guid?)m.Id) // Cast to nullable Guid
            .FirstOrDefaultAsync()
            .Result ?? Guid.Empty; // Default to Guid.Empty if no machine found

        var o = File.ReadAllText(Path.Combine(ApplicationDetails.InstalledPath, "config", "timelines", "BrowserFirefox.json"));
        var timeline = JsonConvert.DeserializeObject<Timeline>(o);
        timeline.Id = Guid.NewGuid();
        timeline.Status = Timeline.TimelineStatus.Run;

        return new MachineUpdate
        {
            MachineId = machineId,
            ActiveUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            Status = StatusType.Active,
            Type = UpdateClientConfig.UpdateType.TimelinePartial,
            Update = timeline
        };
    }
}
