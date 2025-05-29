// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Ghosts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ghosts.api.Infrastructure.Models
{
    [Table("machine_updates")]
    public class MachineUpdate
    {
        public int Id { get; set; }

        public Guid MachineId { get; set; }

        public string Username { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public UpdateClientConfig.UpdateType Type { get; set; }

        public DateTime ActiveUtc { get; set; }
        public DateTime CreatedUtc { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }

        public Timeline Update { get; set; }

        public MachineUpdate()
        {
            var now = DateTime.UtcNow;
            ActiveUtc = now;
            CreatedUtc = now;
        }

        public string ToActivityPlainText()
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var handler in this.Update.TimeLineHandlers)
                {
                    switch (handler.HandlerType)
                    {
                        case HandlerType.BrowserChrome:
                        case HandlerType.BrowserFirefox:
                            sb.Append($"Used {handler.HandlerType.ToString().Replace("Browser", "")} to visit {handler.Initial}\n");
                            break;
                        default:
                            sb.Append($"Used {handler.HandlerType.ToString()} to perform some activity\n");
                            break;
                    }

                }
            }
            catch (Exception e)
            {
                return "";
            }

            return sb.ToString();
        }
    }

    public class MachineUpdateConfiguration : IEntityTypeConfiguration<MachineUpdate>
    {
        public void Configure(EntityTypeBuilder<MachineUpdate> builder)
        {
            // Configure the Update property to be stored as JSON in the database
            builder.Property(e => e.Update)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v, Formatting.None),
                    v => JsonConvert.DeserializeObject<Timeline>(v))
                .HasColumnName("update");
        }
    }
}
