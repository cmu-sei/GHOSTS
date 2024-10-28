// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using ghosts.api.Infrastructure.Models;
using Ghosts.Domain;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Filters;

namespace Ghosts.Api.ViewModels
{
    public class MachineUpdateViewModel
    {
        public Guid MachineId { get; set; }

        public UpdateClientConfig.UpdateType Type { get; set; }

        public DateTime ActiveUtc { get; set; }

        public StatusType Status { get; set; }

        public Timeline Update { get; set; }

        public MachineUpdate ToMachineUpdate()
        {
            var machineUpdate = new MachineUpdate
            {
                CreatedUtc = DateTime.UtcNow,
                Status = Status,
                Update = Update, //JsonConvert.SerializeObject(Update),
                MachineId = MachineId,
                Type = Type,
                ActiveUtc = ActiveUtc
            };
            return machineUpdate;
        }
    }

    public class MachineUpdateViewModelExample : IExamplesProvider<MachineUpdateViewModel>
    {
        public MachineUpdateViewModel GetExamples()
        {
            return new MachineUpdateViewModel
            {
                MachineId = Guid.NewGuid(),
                Type = UpdateClientConfig.UpdateType.TimelinePartial,
                ActiveUtc = DateTime.UtcNow,
                Status = StatusType.Active,
                Update = new Timeline
                {
                    Id = Guid.NewGuid(),
                    Status = Timeline.TimelineStatus.Run,
                    TimeLineHandlers = new List<TimelineHandler>
                    {
                        new()
                        {
                            HandlerType = HandlerType.BrowserFirefox,
                            Initial = "https://cmu.edu",
                            UtcTimeOn = TimeSpan.Parse("00:00:00"),
                            UtcTimeOff = TimeSpan.Parse("23:59:59"),
                            Loop = true,
                            TimeLineEvents = new List<TimelineEvent>
                            {
                                new()
                                {
                                    Command = "browse",
                                    CommandArgs = new List<object> { "https://sei.cmu.edu" },
                                    DelayAfter = 30000,
                                    DelayBefore = 0
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
