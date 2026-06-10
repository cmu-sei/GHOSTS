// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileHelpers;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

public class DatabaseContentManager
{
    public string Subject { private set; get; }
    public string Body { private set; get; }

    internal IList<DatabaseContent> Content { private set; get; }
    private static readonly Random _random = new();

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private Dictionary<string,List<string>> Categories { set; get; }
   
    public static void Check()
    {
        var DatabaseContentManager = new DatabaseContentManager();
        DatabaseContentManager.LoadDatabaseFile();
        if (DatabaseContentManager.Content.Count < 1)
        {
            const string msg = "Database content could not be loaded. Database content will not be posted";
            _log.Error(msg);
            Console.WriteLine(msg);
        }
        else
        {
            var msg = $"Database content loaded successfully with {DatabaseContentManager.Content.Count} records found";
            _log.Info(msg);
            Console.WriteLine(msg);
        }
    }

    public DatabaseContentManager()
    {
        LoadDatabaseFile();
        var total = Content.Count;

        if (total <= 0) {
            const string msg = "Database content could not be loaded. Database content will not be posted";
            _log.Error(msg);
            Body = null;
            return;
        }
        // Parse the database
        var keyString = Content[0].Body;
        var keyList = keyString.Split('~');
        Categories = new Dictionary<string,List<string>>();
        
        foreach (string key in keyList)
        {
            Categories[key] = new List<string>();
        }
        for (int j = 1; j < Content.Count; j+=1)
        {
            var cString = Content[j].Body;
            var cList = cString.Split('~');
            int k = 0;
            foreach(string key in keyList)
            {
                List<string> v = Categories[key];
                v.Add(cList[k]);
                k += 1;
            }

        }
        return;
    }
    
    public string GetCategoryValue(string category)
    {

        if (Categories != null && Categories.ContainsKey(category))
        {
            List<string> v = Categories[category];
            return v[_random.Next(0, v.Count)];
        } else
        {
            // no string available. Return a Default
            return (category + "_noValueAvailable");
        }
    }

    public void LoadDatabaseFile()
    {
        try
        {
            var engine = new FileHelperEngine<DatabaseContent>
            {
                Encoding = Encoding.UTF8
            };
            Content = engine.ReadFile(ClientConfigurationResolver.DatabaseContent).ToList();
        }
        catch (Exception e)
        {
            _log.Error($"Database content file could not be loaded: {e}");
            Content = new List<DatabaseContent>();
        }
    }

}

[DelimitedRecord("|")]
[IgnoreEmptyLines()]
internal class DatabaseContent
{
    public string Id { get; set; }
    public string Body { get; set; }

}

