// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using ghosts.api.Infrastructure.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ghosts.api.ViewModels
{
    public class WebhookViewModel
    {
        public WebhookViewModel()
        {
            CreatedUtc = DateTime.UtcNow;
            Status = StatusType.Active;
            PostbackMethod = Webhook.WebhookMethod.GET;
        }

        public string Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }

        public string Description { get; set; }
        public string PostbackUrl { get; set; }
        public Webhook.WebhookMethod PostbackMethod { get; set; }
        public object PostbackFormat { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string ApplicationUserId { get; set; }
    }
}
