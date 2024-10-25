// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;

namespace Ghosts.Api.Infrastructure.Data
{
    public static class ApplicationCleanUp
    {
        public static void Run(ApplicationDbContext context, int retain)
        {
            foreach (var machine in context.Machines)
            {
                //put top 5 in list
                var ids = new List<int>();

                foreach (var o in context.HistoryHealth.Where(x => x.MachineId == machine.Id)
                    .OrderByDescending(x => x.CreatedUtc).Take(retain))
                    ids.Add(o.Id);

                if (ids.Count > 0)
                {
                    //delete not in list
                    var ids1 = ids;
                    var o = context.HistoryHealth.Where(x => !ids1.Contains(x.Id));
                    if (o.Any())
                    {
                        context.HistoryHealth.RemoveRange(o);
                        context.SaveChanges();
                    }
                }

                ids = new List<int>();
                foreach (var o in context.HistoryTimeline.Where(x => x.MachineId == machine.Id)
                    .OrderByDescending(x => x.CreatedUtc).Take(retain))
                    ids.Add(o.Id);

                if (ids.Count > 0)
                {
                    //delete not in list
                    var ids1 = ids;
                    var o = context.HistoryTimeline.Where(x => !ids1.Contains(x.Id));
                    if (o.Any())
                    {
                        context.HistoryTimeline.RemoveRange(o);
                        context.SaveChanges();
                    }
                }

                ids = new List<int>();
                foreach (var o in context.HistoryMachine.Where(x => x.MachineId == machine.Id)
                    .OrderByDescending(x => x.CreatedUtc).Take(retain))
                    ids.Add(o.Id);

                if (ids.Count > 0)
                {
                    //delete not in list
                    var o = context.HistoryMachine.Where(x => !ids.Contains(x.Id));
                    if (o.Any())
                    {
                        context.HistoryMachine.RemoveRange(o);
                        context.SaveChanges();
                    }
                }
            }
        }
    }
}
