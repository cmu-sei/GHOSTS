// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Ghosts.Api.Infrastructure.Models;
using NLog;

namespace Ghosts.Api.Infrastructure.ContentServices.Bedrock;

public class BedrockConnectorService : IContentService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;

    public BedrockConnectorService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        return await ExecuteQuery(prompt, CancellationToken.None);
    }

    public async Task<string> ExecuteQuery(string prompt, CancellationToken ct)
    {
        try
        {
            var client = BuildClient();

            var request = new ConverseRequest
            {
                ModelId = _configuration.Model,
                Messages = new List<Message>
                {
                    new Message
                    {
                        Role = ConversationRole.User,
                        Content = new List<ContentBlock> { new ContentBlock { Text = prompt } }
                    }
                }
            };

            var response = await client.ConverseAsync(request, ct);
            return response.Output.Message.Content[0].Text;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Bedrock threw an exception calling model {_configuration.Model} in region {_configuration.AwsRegion}");
            return null;
        }
    }

    private AmazonBedrockRuntimeClient BuildClient()
    {
        var region = RegionEndpoint.GetBySystemName(
            _configuration.AwsRegion
            ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
            ?? "us-east-1");

        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            return new AmazonBedrockRuntimeClient(credentials, region);
        }

        // Fall back to the default credential chain (IAM role, instance profile, ~/.aws/credentials)
        return new AmazonBedrockRuntimeClient(region);
    }
}
