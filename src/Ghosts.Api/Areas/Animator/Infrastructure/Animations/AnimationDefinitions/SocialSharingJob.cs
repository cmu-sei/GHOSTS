// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using ghosts.api.Areas.Animator.Hubs;
using ghosts.api.Areas.Animator.Infrastructure.ContentServices;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using NLog;
using RestSharp;
using DataFormat = Npgsql.Internal.DataFormat;

namespace ghosts.api.Areas.Animator.Infrastructure.Animations.AnimationDefinitions;

public class SocialSharingJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly Random _random;
    private const string SavePath = "output/socialsharing/";
    private readonly int _currentStep;
    private readonly IHubContext<ActivityHub> _activityHubContext;
    private CancellationToken _cancellationToken;
    private readonly ApplicationDbContext _context;
    
    public SocialSharingJob(ApplicationSettings configuration, ApplicationDbContext context, Random random, IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        try
        {
            this._activityHubContext = activityHubContext;
            this._configuration = configuration;
            this._random = random;
            this._context = context;
            this._cancellationToken = cancellationToken;

            if (!_configuration.AnimatorSettings.Animations.SocialSharing.IsInteracting) return;
            
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            while (!this._cancellationToken.IsCancellationRequested)
            {
                if (this._currentStep > _configuration.AnimatorSettings.Animations.SocialSharing.MaximumSteps)
                {
                    _log.Trace($"Maximum steps met: {this._currentStep - 1}. Social sharing is exiting...");
                    return;
                }

                this.Step();
                Thread.Sleep(this._configuration.AnimatorSettings.Animations.SocialSharing.TurnLength);
                this._currentStep++;
            }
        }
        catch (ThreadInterruptedException)
        {
            // continue
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async void Step()
    {
        var contentService = new ContentCreationService(_configuration.AnimatorSettings.Animations.SocialSharing.ContentEngine);
        
        //take some random NPCs
        var lines = new StringBuilder();
        var rawAgents = this._context.Npcs.ToList();
        if (!rawAgents.Any())
        {
            _log.Warn("No NPCs found in Mongo. Is this correct?");
            return;
        }
        var agents = rawAgents.Shuffle(_random).Take(_random.Next(5, 20));
        foreach (var agent in agents)
        {
            var tweetText = await contentService.GenerateTweet(agent);
            if (string.IsNullOrEmpty(tweetText))
                return;
            
            lines.AppendFormat($"{DateTime.Now},{agent.Id},\"{tweetText}\"{Environment.NewLine}");

            // the payloads to socializer are a bit randomized
            var userFormValue = new[] { "user", "usr", "u", "uid", "user_id", "u_id" }.RandomFromStringArray();
            var messageFormValue = new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" }.RandomFromStringArray();

            //TODO
            throw new NotImplementedException();
            // if (_configuration.AnimatorSettings.Animations.SocialSharing.IsSendingTimelinesDirectToSocializer &&
            //     !string.IsNullOrEmpty(_configuration.GhostsApiUrl))
            // {
            //     var client = new RestClient(_configuration.Animations.SocialSharing.PostUrl);
            //     var request = new RestRequest("/", Method.Post)
            //     {
            //         RequestFormat = DataFormat.Json
            //     };
            //     request.AddParameter(userFormValue, agent.Name.ToString());
            //     request.AddParameter(messageFormValue, tweetText);
            //     
            //     try
            //     {
            //         var response = client.Execute(request);
            //         if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NoContent)
            //         {
            //             throw (new Exception($"Socializer responded with {response.StatusCode} to the request agent: {agent.Name} text: {tweetText}"));
            //         }
            //     }
            //     catch (Exception e)
            //     {
            //         _log.Error($"Could not post timeline command to Socializer {_configuration.Animations.SocialSharing.PostUrl}: {e}");
            //     }
            // }
            //
            // if (_configuration.AnimatorSettings.Animations.SocialSharing.IsSendingTimelinesToGhostsApi && !string.IsNullOrEmpty(_configuration.GhostsApiUrl))
            // {
            //     //{\"user\":\"{user}\",\"message\":\"{message}}
            //     var formValues = new StringBuilder();
            //     formValues.Append('{')
            //         .Append("\\\"").Append(userFormValue).Append("\\\":\\\"").Append(agent.Email).Append("\\\"")
            //         .Append(",\\\"").Append(messageFormValue).Append("\\\":\\\"").Append(tweetText).Append("\\\"");
            //     for (var i = 0; i < AnimatorRandom.Rand.Next(0, 6); i++)
            //     {
            //         formValues
            //             .Append(",\\\"").Append(Lorem.GetWord().ToLower()).Append("\\\":\\\"").Append(AnimatorRandom.Rand.NextDouble()).Append("\\\"");
            //     }
            //
            //     formValues.Append('}');
            //     
            //     var postPayload = await File.ReadAllTextAsync("config/socializer_post.json");
            //     postPayload = postPayload.Replace("{id}", Guid.NewGuid().ToString());
            //     postPayload = postPayload.Replace("{user}", agent.Email);
            //     postPayload = postPayload.Replace("{payload}", formValues.ToString());
            //     postPayload = postPayload.Replace("{url}", _configuration.Animations.SocialSharing.PostUrl);
            //     postPayload = postPayload.Replace("{now}", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            //
            //     await GhostsApiService.PostTimeline(_configuration, postPayload);
            // }
            
            //post to hub
            await this._activityHubContext.Clients.All.SendAsync("show",
                "1",
                agent.Id.ToString(),
                "social",
                tweetText,
                DateTime.Now.ToString(CultureInfo.InvariantCulture)
            );
        }

        await File.AppendAllTextAsync($"{SavePath}tweets.csv", lines.ToString());
    }
}