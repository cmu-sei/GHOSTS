// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;

namespace ghosts.api.Areas.Animator.Infrastructure.Animations;

public class SocialGraph
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public IList<SocialConnection> Connections { get; set; }
    public IList<Learning> Knowledge { get; set; }
    public IList<Belief> Beliefs { get; set; }
    public long CurrentStep { get; set; }

    public SocialGraph()
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

    public class Learning
    {
        public Guid To { get; set; }
        public Guid From { get; set; }
        public string Topic { get; set; }
        public long Step { get; set; }
        public int Value { get; set; }

        public Learning(Guid to, Guid from, string topic, long currentStep, int value)
        {
            this.From = from;
            this.To = to;
            this.Topic = topic;
            this.Step = currentStep;
            this.Value = value;
        }

        public override string ToString()
        {
            return $"{this.To},{this.From},{this.Topic},{this.Step},{this.Value}";
        }
    }
    
    public class Belief
    {
        public Guid To { get; set; }
        public Guid From { get; set; }
        public string Name { get; set; }
        public long Step { get; set; }
        public decimal Likelihood { get; set; }
        public decimal Posterior { get; set; }

        public Belief(Guid to, Guid from, string name, long currentStep, decimal likelihood, decimal posterior)
        {
            this.From = from;
            this.To = to;
            this.Name = name;
            this.Step = currentStep;
            this.Likelihood = likelihood;
            this.Posterior = posterior;
        }

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