// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Api.Models;
using Ghosts.Domain;
using Newtonsoft.Json;

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
                Update = JsonConvert.SerializeObject(Update),
                MachineId = MachineId,
                Type = Type,
                ActiveUtc = ActiveUtc
            };
            return machineUpdate;
        }
    }
}