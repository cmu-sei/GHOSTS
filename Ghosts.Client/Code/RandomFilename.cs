// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Client.Code
{
    /// <summary>
    /// Filename generator for those files created for office
    /// </summary>
    public static class RandomFilename
    {
        public static string Generate()
        {
            var list = new[] {
                "report",
                "receipts",
                "Results",
                "Final",
                "draft",
                "netcom",
                "army",
                "FY18-report",
                "intel",
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
                "dont forget to do ths",
                "RiskManagement",
                "RPODirectory",
                "Agenda",
                "OfficeProceduresV01",
                "OfficeProceduresV02",
                "OfficeProceduresV03",
                "OfficeProceduresV04",
                "OfficeProceduresV05",
                "OfficeProceduresV06",
                "OfficeProceduresV07"
            };

            var rand = new Random().Next(0, list.GetUpperBound(0) - 1);
            var filename = list[rand];

            rand = new Random().Next(0, 4);
            switch(rand)
            {
                case 0:
                    filename += $"-{new Random().Next(0, 12)}";
                    break;
                case 1:
                    filename += $"-{DateTime.Now.Month}";
                    break;
                case 2:
                    filename += $"-{new Random().Next(0, 30)}";
                    break;
            }
            
            return filename;
        }
    }
}
