// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class WebhooksController(ApplicationDbContext context, IBackgroundQueue service) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IBackgroundQueue _service = service;

        /// <summary>
        /// Gets all of the webhooks currently active on the system
        /// </summary>
        /// <returns>A list of all webhooks</returns>
        [SwaggerOperation("WebhooksGetAll")]
        [HttpGet]
        public IEnumerable<Webhook> GetWebhooks()
        {
            return _context.Webhooks;
        }

        /// <summary>
        /// Gets a specific webhook by its Id
        /// </summary>
        /// <param name="id">The webhook to retrieve</param>
        /// <returns>The webhook</returns>
        [SwaggerOperation("WebhooksGetById")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetWebhook([FromRoute] Guid id)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);

            if (webhook == null) return NotFound();

            return Ok(webhook);
        }

        /// <summary>
        /// Updates a specific webhook
        /// </summary>
        /// <param name="id">The specific webhook to update</param>
        /// <param name="webhook">The update to make</param>
        /// <returns>The updated webhook</returns>
        [SwaggerOperation("WebhooksUpdate")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutWebhook([FromRoute] Guid id, [FromBody] Webhook webhook)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (id != webhook.Id) return BadRequest();

            _context.Entry(webhook).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WebhookExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        /// <summary>
        /// Create a new webhook
        /// </summary>
        /// <param name="webhook">The webhook to create</param>
        /// <returns>The saved webhook</returns>
        [SwaggerOperation("WebhooksCreate")]
        [HttpPost]
        public async Task<IActionResult> PostWebhook([FromBody] Webhook webhook)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (webhook.Id == Guid.Empty)
                webhook.Id = Guid.NewGuid();
            _context.Webhooks.Add(webhook);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetWebhook", new { id = webhook.Id }, webhook);
        }

        /// <summary>
        /// Delete a specfic webhook by its Id
        /// </summary>
        /// <param name="id">The Id of the webhook to delete</param>
        /// <returns>204 No Content</returns>
        [SwaggerOperation("WebhooksDete")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWebhook([FromRoute] Guid id)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);
            if (webhook == null) return NotFound();

            _context.Webhooks.Remove(webhook);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// For webhook testing
        /// </summary>
        /// <param name="id">The Id to test</param>
        /// <returns>204 No Content</returns>
        [SwaggerOperation("WebhooksTest")]
        [HttpGet("{id}/test")]
        public async Task<IActionResult> Test([FromRoute] Guid id)
        {
            var webhook = await _context.Webhooks.SingleOrDefaultAsync(m => m.Id == id);

            var timeline = new HistoryTimeline();

            var payload = new NotificationQueueEntry
            {
                Type = NotificationQueueEntry.NotificationType.Timeline,
                Payload = (JObject)JToken.FromObject(timeline)
            };

            QueueSyncService.HandleWebhook(webhook, payload);
            return NoContent();
        }

        /// <summary>
        /// Gets a test instance of a webhook
        /// </summary>
        /// <param name="webhookid">The Id of the webhook</param>
        /// <param name="historytimelineid">The timeline item to hook</param>
        /// <returns>204 No Content</returns>
        [SwaggerOperation("WebhooksTestById")]
        [HttpGet("{webhookid}/test/{historytimelineid}")]
        public async Task<IActionResult> TestByID([FromRoute] Guid webhookid, int historytimelineid)
        {
            try
            {
                var timeline = await _context.HistoryTimeline.FirstOrDefaultAsync(o => o.Id == historytimelineid);

                _service.Enqueue(
                    new QueueEntry
                    {
                        Type = QueueEntry.Types.Notification,
                        Payload =
                            new NotificationQueueEntry
                            {
                                Type = NotificationQueueEntry.NotificationType.Timeline,
                                Payload = (JObject)JToken.FromObject(timeline)
                            }
                    });


            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                //todo log this 
            }
            return NoContent();
        }

        private bool WebhookExists(Guid id)
        {
            return _context.Webhooks.Any(e => e.Id == id);
        }
    }
}
