// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ghosts.api.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [Table("webhooks")]
    public class Webhook
    {
        public enum WebhookMethod
        {
            GET = 0,
            POST = 1
        }

        public Webhook()
        {
            CreatedUtc = DateTime.UtcNow;
            Status = StatusType.Active;
            PostbackMethod = WebhookMethod.GET;
        }

        public Webhook(WebhookViewModel model)
        {
            var id = Guid.NewGuid();
            if (Guid.TryParse(model.Id, out id))
                Id = id;
            Status = model.Status;
            Description = model.Description;
            PostbackUrl = model.PostbackUrl;
            PostbackMethod = model.PostbackMethod;
            PostbackFormat = model.PostbackFormat.ToString();
            CreatedUtc = model.CreatedUtc;
            if (Guid.TryParse(model.ApplicationUserId, out id))
                ApplicationUserId = id;
        }

        [Key] public Guid Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }

        public string Description { get; set; }
        public string PostbackUrl { get; set; }
        public WebhookMethod PostbackMethod { get; set; }
        public string PostbackFormat { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Guid ApplicationUserId { get; set; }
    }
}