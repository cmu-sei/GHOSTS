// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

public class AiModels
{
    public class ActionRequest
    {
        [JsonPropertyName("handler")]
        public string Handler { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("scale")]
        public int Scale { get; set; }

        [JsonPropertyName("who")]
        public string Who { get; set; }

        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; }

        [JsonPropertyName("sentiment")]
        public string Sentiment { get; set; }

        [JsonPropertyName("original")]
        public string Original { get; set; }
    }

    public class SentimentResult
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }

        [JsonPropertyName("entities")]
        public object Entities { get; set; }
    }
}
