// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [Table("webhooks")]
    public class Webhook
    {
        [Key]
        public Guid Id { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status {get; set;}
        public string Description { get; set; }
        public string PostbackUrl { get; set; }
        public WebhookMethod PostbackMethod { get; set; }
        public string PostbackFormat { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Guid ApplicationUserId { get; set; }

        public Webhook()
        {
            this.CreatedUtc = DateTime.UtcNow;
            this.Status = StatusType.Active;
            this.PostbackMethod = WebhookMethod.GET;
        }

        public enum WebhookMethod
        {
            GET = 0,
            POST = 1
        }
    }
}

