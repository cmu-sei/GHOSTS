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

        public Webhook(WebhookViewModel model)
        {
            var id = Guid.NewGuid();
            if(Guid.TryParse(model.Id, out id))
                this.Id = id;
            this.Status = model.Status;
            this.Description = model.Description;
            this.PostbackUrl = model.PostbackUrl;
            this.PostbackMethod = model.PostbackMethod;
            this.PostbackFormat = model.PostbackFormat.ToString();
            this.CreatedUtc = model.CreatedUtc;
            if (Guid.TryParse(model.ApplicationUserId, out id))
                this.ApplicationUserId = id;
        }

        public enum WebhookMethod
        {
            GET = 0,
            POST = 1
        }
    }
}

