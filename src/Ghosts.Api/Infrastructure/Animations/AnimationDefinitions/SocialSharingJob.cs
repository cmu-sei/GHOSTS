// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator.Extensions;
using ghosts.api.Hubs;
using Ghosts.Api.Infrastructure;
using ghosts.api.Infrastructure.ContentServices;
using Ghosts.Api.Infrastructure.Data;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using RestSharp;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions
{
    public class SocialSharingJob
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationSettings _configuration;
        private readonly Random _random;
        private int _currentStep;
        private readonly IHubContext<ActivityHub> _activityHubContext;
        private readonly CancellationToken _cancellationToken;
        private readonly ApplicationDbContext _context;
        private readonly IMachineUpdateService _updateService;
        private readonly IFormatterService _formatterService;
        private static readonly string[] ar = new[] { "user", "usr", "u", "uid", "user_id", "u_id" };

        public SocialSharingJob(ApplicationSettings configuration, IServiceScopeFactory scopeFactory, Random random,
            IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
        {
            try
            {
                this._activityHubContext = activityHubContext;
                this._configuration = configuration;
                this._random = random;

                using var innerScope = scopeFactory.CreateScope();
                this._context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                this._cancellationToken = cancellationToken;
                this._updateService = innerScope.ServiceProvider.GetRequiredService<IMachineUpdateService>();

                _formatterService =
                    new ContentCreationService(_configuration.AnimatorSettings.Animations.SocialSharing.ContentEngine).FormatterService;

                if (!_configuration.AnimatorSettings.Animations.SocialSharing.IsInteracting)
                {
                    _log.Trace($"Social sharing is not interacting. Exiting...");
                    return;
                }

                RunAsync().GetAwaiter().GetResult();
            }
            catch (ThreadInterruptedException e)
            {
                _log.Info("Social sharing thread interrupted!");
                _log.Error(e);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            _log.Info("Social sharing job complete. Exiting...");
        }

        private async Task RunAsync()
        {
            while (!this._cancellationToken.IsCancellationRequested)
            {
                if (this._currentStep > _configuration.AnimatorSettings.Animations.SocialSharing.MaximumSteps)
                {
                    _log.Trace($"Maximum steps met: {this._currentStep - 1}. Social sharing is exiting...");
                    return;
                }

                await this.Step();
                await Task.Delay(this._configuration.AnimatorSettings.Animations.SocialSharing.TurnLength, this._cancellationToken);
                this._currentStep++;
            }
        }

        private async Task Step()
        {
            _log.Trace("Social sharing step proceeding...");

            //take some random NPCs
            var activities = new List<NpcActivity>();
            var rawAgents = this._context.Npcs.ToList();
            if (rawAgents.Count == 0)
            {
                _log.Warn("No NPCs found. Is this correct?");
                return;
            }
            _log.Trace($"Found {rawAgents.Count} raw agents...");

            var agents = rawAgents.Shuffle(_random).Take(_random.Next(5, 20)).ToList();
            _log.Trace($"Processing {agents.Count} agents...");
            foreach (var agent in agents)
            {
                _log.Trace($"Processing agent {agent.NpcProfile.Email}...");
                var tweetText = await this._formatterService.GenerateTweet(agent);
                if (string.IsNullOrEmpty(tweetText))
                {
                    _log.Trace($"Content service generated no payload...");
                    return;
                }

                activities.Add(new NpcActivity { ActivityType = NpcActivity.ActivityTypes.SocialMediaPost, NpcId = agent.Id, CreatedUtc = DateTime.UtcNow, Detail = tweetText });

                // the payloads to socializer are a bit randomized
                var userFormValue = ar.RandomFromStringArray();
                var messageFormValue =
                    new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" }
                        .RandomFromStringArray();

                if (_configuration.AnimatorSettings.Animations.SocialSharing.IsSendingTimelinesDirectToSocializer)
                {
                    var client = new RestClient(_configuration.AnimatorSettings.Animations.SocialSharing.PostUrl);
                    var request = new RestRequest("/", Method.Post)
                    {
                        RequestFormat = DataFormat.Json
                    };
                    request.AddParameter(userFormValue, agent.NpcProfile.Name.ToString());
                    request.AddParameter(messageFormValue, tweetText);

                    try
                    {
                        var response = client.Execute(request);
                        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NoContent)
                        {
                            throw (new Exception(
                                $"Socializer responded with {response.StatusCode} to the request agent: {agent.NpcProfile.Name} text: {tweetText}"));
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Error(
                            $"Could not post timeline command to Socializer {_configuration.AnimatorSettings.Animations.SocialSharing.PostUrl}: {e}");
                    }
                }

                if (_configuration.AnimatorSettings.Animations.SocialSharing.IsSendingTimelinesToGhostsApi)
                {
                    var payload = new
                    {
                        Uri = _configuration.AnimatorSettings.Animations.SocialSharing.PostUrl,
                        Category = "social",
                        Method = "POST",
                        Headers = new Dictionary<string, string>
                        {
                            { "u", agent.NpcProfile.Email }
                        },
                        FormValues = new Dictionary<string, string>
                        {
                            { userFormValue, agent.NpcProfile.Email },
                            { messageFormValue, tweetText }
                        }
                    };

                    var t = new Timeline
                    {
                        Id = Guid.NewGuid(),
                        Status = Timeline.TimelineStatus.Run
                    };
                    var th = new TimelineHandler
                    {
                        HandlerType = HandlerType.BrowserFirefox,
                        Initial = "about:blank",
                        UtcTimeOn = new TimeSpan(0, 0, 0),
                        UtcTimeOff = new TimeSpan(23, 59, 59),
                        HandlerArgs = new Dictionary<string, object>
                    {
                        { "isheadless", "false" }
                    },
                        Loop = false
                    };
                    var te = new TimelineEvent
                    {
                        Command = "browse",
                        CommandArgs = [JsonConvert.SerializeObject(payload)],
                        DelayAfter = 0,
                        DelayBefore = 0
                    };
                    th.TimeLineEvents.Add(te);
                    t.TimeLineHandlers.Add(th);

                    var machineUpdate = new MachineUpdate();
                    if (agent.MachineId.HasValue)
                    {
                        machineUpdate.MachineId = agent.MachineId.Value;
                    }

                    machineUpdate.Update = t; //JsonConvert.SerializeObject(t);
                    machineUpdate.Username = agent.NpcProfile.Email;
                    machineUpdate.Status = StatusType.Active;
                    machineUpdate.Type = UpdateClientConfig.UpdateType.TimelinePartial;

                    _ = await _updateService.CreateAsync(machineUpdate, _cancellationToken);
                }

                //post to hub
                await this._activityHubContext.Clients.All.SendAsync("show",
                    "1",
                    agent.Id.ToString(),
                    "social",
                    tweetText,
                    DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    cancellationToken: _cancellationToken);
            }

            await this._context.NpcActivities.AddRangeAsync(activities, _cancellationToken);
            await this._context.SaveChangesAsync(this._cancellationToken);
        }
    }
}
