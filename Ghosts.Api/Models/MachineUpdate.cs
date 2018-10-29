// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Models
{
    [Table("machineupdates")]
    public class MachineUpdate
    {
        public int Id { get; set; }

        [ForeignKey("MachineId")]
        public Guid MachineId { get; set; }

        public Domain.UpdateClientConfig.UpdateType Type { get; set; }
        
        /// <summary>
        /// This is the path to the local update file 
        /// ala /app_data/updates/key/type.json
        /// </summary>
        public Guid Key { get; set; }

        public DateTime ActiveUtc { get; set; }
        public DateTime CreatedUtc { get; set; }

        public StatusType Status { get; set; }

        [NotMapped]
        public object Update { get; set; }

        public string GetPath()
        {
            var path = $"updates/{this.Key}/{this.Type.ToString().ToLower()}.json";

            return path;
        }
    }
}
