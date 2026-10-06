// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Infrastructure.Services
{
    /// <summary>
    /// The one model call scenario authoring makes. It takes and returns the Converse types as they are, so
    /// that every content block of a reply, reasoning included, reaches the service whole. A test scripts it.
    /// </summary>
    public interface IAuthoringModel
    {
        Task<ConverseResponse> ConverseAsync(ConverseRequest request, CancellationToken ct);
    }

    /// <summary>
    /// Bedrock's Converse API. Credentials as BedrockChatService takes them: explicit keys from the
    /// environment, otherwise the default credential chain. Never from configuration, never to the browser.
    /// The SDK's own retries are off: the service retries a 503 once and records each attempt (F4, H1).
    /// </summary>
    public class BedrockAuthoringModel(IOptions<ScenarioAuthoringOptions> options) : IAuthoringModel
    {
        private readonly ScenarioAuthoringOptions _options = options.Value;
        private AmazonBedrockRuntimeClient _client;

        public Task<ConverseResponse> ConverseAsync(ConverseRequest request, CancellationToken ct) =>
            (_client ??= BuildClient()).ConverseAsync(request, ct);

        private AmazonBedrockRuntimeClient BuildClient()
        {
            var config = new AmazonBedrockRuntimeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region),
                // G1: a dead connection must not wait forever.
                Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                MaxErrorRetry = 0
            };

            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            AWSCredentials credentials = !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
                ? new BasicAWSCredentials(accessKey, secretKey)
                : FallbackCredentialsFactory.GetCredentials();

            return new AmazonBedrockRuntimeClient(credentials, config);
        }
    }
}
