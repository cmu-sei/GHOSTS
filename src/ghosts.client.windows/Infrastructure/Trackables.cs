// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;
using NLog;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure;

/// <summary>
/// Lists trackable key/value pairs
/// </summary>
public static class Trackables
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
 
    public class Trackable
    {
        public Guid Key { get; set; }
        public string Value { get; set; }
        public DateTime CreatedUtc { get; set; }

        public Trackable()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        public Trackable(Guid key, string value)
        {
            this.Key = key;
            this.Value = value;
            CreatedUtc = DateTime.UtcNow;
        }

        public Trackable(string key, string value)
        {
            this.Key = new Guid(key);
            this.Value = value;
            CreatedUtc = DateTime.UtcNow;
        }
    }

    public class TrackablesManager
    {
        public List<Trackable> Items;

        public TrackablesManager()
        {
            Items = File.Exists(ApplicationDetails.InstanceFiles.Trackables) 
                ? JsonConvert.DeserializeObject<List<Trackable>>(File.ReadAllText(ApplicationDetails.InstanceFiles.Trackables)) 
                : new List<Trackable>();
        }

        public void Add(Trackable item)
        {
            if(this.Items.Any(_=> _.Key == item.Key))
                this.Items.First(_ => _.Key == item.Key).Value = item.Value;
            else
                Items.Add(item);
        }

        public Trackable Find(Guid key)
        {
            return this.Items.FirstOrDefault(_ => _.Key == key);
        }

        public void Save()
        {
            using var file = File.CreateText(ApplicationDetails.InstanceFiles.Trackables);
            var serializer = new JsonSerializer { Formatting = Formatting.Indented };
            serializer.Serialize(file, this.Items);
        }
    }
}