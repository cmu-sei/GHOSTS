// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Lite.Infrastructure;

public static class RandomFilename
{
    private static readonly Random _random = new();

    public static string Generate()
    {
        // load config file otherwise use hardcoded list for older clients
        var list = new List<string>
        {
            "report",
            "receipts",
            "Results",
            "Final $x$",
            "draft",
            "netcom",
            "army",
            "FY18-report",
            "intel-$x$",
            "Beluka",
            "report-intel",
            "Jalton-AR",
            "inter_office_report",
            "working_draft",
            "report for tom",
            "cpt wilson report",
            "sgt farr sitrep",
            "file_name",
            "todo list",
            "dont forget to do ths - $x$",
            "RiskManagement",
            "RPODirectory",
            "Agenda",
            "OfficeProcedures-$x$"
        };

        var fileName = list.PickRandom();

        // add variables?
        if (fileName.Contains("$x$"))
        {
            var rand = _random.Next(0, 4);
            switch (rand)
            {
                case 0:
                    fileName = fileName.Replace("$x$", _random.Next(0, 12).ToString());
                    break;
                case 1:
                    fileName = fileName.Replace("$x$", DateTime.Now.Month.ToString());
                    break;
                case 2:
                    fileName = fileName.Replace("$x$", _random.Next(0, 30).ToString());
                    break;
            }
        }

        return fileName;
    }
}
