// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

/// <summary>
/// Filename generator for those files created for office
/// </summary>
public static class RandomFilename
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Random _random = new Random();

    private static List<string> _filenames = null;
    private static string[] _days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    private static string[] _months = { "September", "October", "November", "December", "January", "February", "March", "April", "May", "June", "July", "August" };
    
    public static string Generate()
    {
        // load config file otherwise use hardcoded list for older clients
        if (_filenames == null) {
            _filenames = new List<string>();

            try
            {
                if(File.Exists(ClientConfigurationResolver.FileNames))
                    _filenames = File.ReadAllLines(ClientConfigurationResolver.FileNames).ToList();
            }
            catch (Exception exc)
            {
                _log.Debug($"./config/filename.txt could not be loaded: {exc}");
            }

            if (_filenames.Count < 1)
            {
                _filenames = new List<string> {
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
                    "must_do_list",
                    "dont_forget_to_do_ths_$x$",
                    "RiskManagement",
                    "RPODirectory",
                    "Agenda",
                    "OfficeProcedures_$x$"
                };
            }
        }

        var fileName = _filenames.PickRandom();

        // add variables?
        if (fileName.Contains("$x$"))
        {
            var rand = _random.Next(0, 4);
            switch (rand)
            {
                case 0:
                    fileName = fileName.Replace("$x$", $"{_random.Next(1, 13).ToString()}_{_random.Next(1, 28).ToString()}");
                    break;
                case 1:
                    fileName = fileName.Replace("$x$", _days[_random.Next(0,_days.Length)]);
                    break;
                case 2:
                    fileName = fileName.Replace("$x$", _random.Next(0, 99).ToString());
                    break;
                case 3:
                    fileName = fileName.Replace("$x$",  _months[_random.Next(0,_months.Length)]);
                    break;
            }
        }

        return fileName;
    }

}
