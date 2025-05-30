// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Client.Infrastructure;

/// <summary>
/// Filename generator for those files created for office
/// </summary>
public static class RandomFilename
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Random _random = new Random();

    public static string Generate()
    {
        // load config file otherwise use hardcoded list for older clients
        var list = new List<string>();

        try
        {
            if(File.Exists(ClientConfigurationResolver.FileNames))
                list = File.ReadAllLines(ClientConfigurationResolver.FileNames).ToList();
        }
        catch (Exception exc)
        {
            _log.Debug($"./config/filename.txt could not be loaded: {exc}");
        }

        if (list.Count < 1)
        {
            list = new List<string> {
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
        }

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