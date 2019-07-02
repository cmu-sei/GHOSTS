using System;
using Ghosts.Api.Models;
using Ghosts.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
                Status = this.Status,
                Update = JsonConvert.SerializeObject(this.Update),
                MachineId = this.MachineId,
                Type = this.Type,
                ActiveUtc = this.ActiveUtc
            };
            return machineUpdate;           
        }
    }
}