// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ghosts.api.Infrastructure.Models;

[Obsolete("This will move to an Automapper map from NPC to NpcProfileSummary class")]
public class NPCReduced
{
    public Dictionary<string, string> PropertySelection { get; set; }

    public NPCReduced()
    {
        PropertySelection = new Dictionary<string, string>();
    }

    public NPCReduced(IEnumerable<string> fieldsToReturn, NpcRecord npc)
    {
        //what we'll return
        PropertySelection = new Dictionary<string, string>();

        //get an npc to "rip apart"

        //for each field we want to return
        foreach (var fieldToReturn in fieldsToReturn)
        {
            var fieldArray = fieldToReturn.Split(".");

            //get that field on the npc object

            object currentObject = npc;

            foreach (var f in fieldArray)
            {
                if (currentObject != null && currentObject.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                {
                    var collection = (IList)currentObject;
                    currentObject = collection[0];
                }

                currentObject = currentObject?.GetType().GetProperty(f)?.GetValue(currentObject, null);
            }

            if (currentObject is not null)
            {
                if (currentObject.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))) { }
                PropertySelection.Add(fieldToReturn, currentObject.ToString());
            }


            //other potential datas to look at 
            //type.GetProperties()
            //foreach (var method in type.GetMethods())
        }
    }

    public PropertyInfo GetPropertyInfo(object src, string name)
    {
        if (src.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            var collection = (IList)src;
            src = collection[0];
        }

        return src.GetType().GetProperties().FirstOrDefault(x => x.Name == name);
    }
}
