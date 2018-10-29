// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ghosts.Api.Data;
using Ghosts.Api.Models;
using Ghosts.Api.Services;
using Newtonsoft.Json.Linq;

namespace ghosts.api.Controllers
{
    [Produces("application/json")]
    [Route("api/Webhooks")]
    public class WebhooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBackgroundQueue _service;

        public WebhooksController(ApplicationDbContext context, IBackgroundQueue service)
        {
            _context = context;
            _service = service;
        }

        // GET: api/Webhooks
        [HttpGet]
        public IEnumerable<Webhook> GetWebhooks()
        {
            return _context.Webhooks;
        }

        // GET: api/Webhooks/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetWebhook([FromRoute] Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);

            if (webhook == null)
            {
                return NotFound();
            }

            return Ok(webhook);
        }

        // PUT: api/Webhooks/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutWebhook([FromRoute] Guid id, [FromBody] Webhook webhook)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != webhook.Id)
            {
                return BadRequest();
            }

            _context.Entry(webhook).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WebhookExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Webhooks
        [HttpPost]
        public async Task<IActionResult> PostWebhook([FromBody] Webhook webhook)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (webhook.Id == Guid.Empty)
                webhook.Id = Guid.NewGuid();
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetWebhook", new { id = webhook.Id }, webhook);
        }

        // DELETE: api/Webhooks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWebhook([FromRoute] Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);
            if (webhook == null)
            {
                return NotFound();
            }

            _context.Webhooks.Remove(webhook);
            await _context.SaveChangesAsync();

            return Ok(webhook);
        }

        // GET: api/Webhooks/5
        [HttpGet("{id}/test")]
        public async Task<IActionResult> Test([FromRoute] Guid id)
        {
            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);

            var timeline = new HistoryTimeline();

            var payload = new QueueSyncService.NotificationQueueEntry();
            payload.Type = QueueSyncService.NotificationQueueEntry.NotificationType.Timeline;
            payload.Payload = (JObject) JToken.FromObject(timeline);

            QueueSyncService.HandleWebhook(webhook, payload);
            return NoContent();
        }

        [HttpGet("{webhookid}/test/{historytimelineid}")]
        public async Task<IActionResult> TestByID([FromRoute] Guid webhookid, int historytimelineid)
        {
            //Webhook w = new Webhook();
            //w.PostbackFormat =
            //    "{ \"MessageInteraction\": {\"SimulationID\": \"\",\"Sender\": \"[MachineName]\",\"TimeStamp\": \"[DateTime.UtcNow]\",\"MessageType\": \"[MessageType]\",\"MessageBody\": {\"[MessagePayload]\"}} }";
            //w.ApplicationUserId = Guid.Empty;
            //w.CreatedUtc = DateTime.UtcNow;
            //w.Description = "Rotem";
            //w.PostbackMethod = Webhook.WebhookMethod.POST;
            //w.PostbackUrl = "http://localhost:8888";

            //_context.Webhooks.Add(w);
            //_context.SaveChanges();

            var timeline = await _context.HistoryTimeline.FirstOrDefaultAsync(o => o.Id == historytimelineid);

            this._service.Enqueue(
            new QueueEntry
            {
                Type = QueueEntry.Types.Notification,
                Payload =
                    new QueueSyncService.NotificationQueueEntry
                    {
                        Type = QueueSyncService.NotificationQueueEntry.NotificationType.Timeline,
                        Payload = (JObject) JToken.FromObject(timeline)
                    }
            });

            return NoContent();
        }

        private bool WebhookExists(Guid id)
        {
            return _context.Webhooks.Any(e => e.Id == id);
        }
    }
}