// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ghosts.api.Infrastructure.Models;

public class EnclaveReducedCsv
{
    public string CsvData { get; set; }

    public EnclaveReducedCsv(string[] fieldsToReturn, Dictionary<string, Dictionary<string, string>> npcDictionary)
    {
        var rowList = new List<string>();
        var fields = string.Join(",", fieldsToReturn);
        var header = "Name," + fields;
        rowList.Add(header);


        foreach (var npc in npcDictionary)
        {
            var npcRow = new List<string>() { npc.Key };
            npcRow.AddRange(fieldsToReturn.Select(property => npcDictionary[npc.Key].TryGetValue(property, out var value) ? value : ""));
            rowList.Add(string.Join(",", npcRow));
        }

        CsvData = string.Join(Environment.NewLine, rowList);

    }
}
