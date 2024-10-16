// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ghosts.api.Infrastructure.Models;

public class NpcSocialGraph
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public IList<SocialConnection> Connections { get; set; }
    public IList<Learning> Knowledge { get; set; }
    public IList<Belief> Beliefs { get; set; }
    public long CurrentStep { get; set; }

    public NpcSocialGraph()
    {
        this.Connections = new List<SocialConnection>();
        this.Knowledge = new List<Learning>();
    }

    public class SocialConnection
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Distance { get; set; }
        public int RelationshipStatus { get; set; }

        public IList<Interaction> Interactions { get; set; }

        public SocialConnection()
        {
            this.Interactions = new List<Interaction>();
        }
    }

    public class Interaction
    {
        public long Step { get; set; }
        public int Value { get; set; }
    }

    public class Learning(Guid to, Guid from, string topic, long currentStep, int value)
    {
        public Guid To { get; set; } = to;
        public Guid From { get; set; } = from;
        public string Topic { get; set; } = topic;
        public long Step { get; set; } = currentStep;
        public int Value { get; set; } = value;

        public override string ToString()
        {
            return $"{this.To},{this.From},{this.Topic},{this.Step},{this.Value}";
        }
    }

    [method: JsonConstructor]
    public class Belief(Guid to, Guid from, string name, long step, decimal likelihood, decimal posterior)
    {
        public Guid To { get; set; } = to;
        public Guid From { get; set; } = from;
        public string Name { get; set; } = name;
        public long Step { get; set; } = step;
        public decimal Likelihood { get; set; } = likelihood;
        public decimal Posterior { get; set; } = posterior;

        public override string ToString()
        {
            return $"{this.To},{this.From},{this.Name},{this.Step},{this.Likelihood},{this.Posterior}";
        }

        public static string ToHeader()
        {
            return "To,From,Name,Step,Likelihood,Posterior";
        }
    }

}