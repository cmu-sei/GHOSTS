// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Models;
using Ghosts.Api.ViewModels;
using NLog;

namespace Ghosts.Api.Services
{
    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboard(CancellationToken ct);
    }

    public class ReportService : IReportService
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        private const int _hoursBack = -23;

        public async Task<DashboardViewModel> GetDashboard(CancellationToken ct)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var dashboard = new DashboardViewModel();

            var dictHealth = new Dictionary<DateTime, int>();
            var dictTimeline = new Dictionary<DateTime, int>();
            var dictMachine = new Dictionary<DateTime, int>();

            var health = new DashboardViewModel.ChartItem { Label = "Health" };
            var timeline = new DashboardViewModel.ChartItem { Label = "Timeline" };
            var history = new DashboardViewModel.ChartItem { Label = "Agent Activities" };

            var s = DateTime.UtcNow.FlattenToHour().AddHours(_hoursBack);
            while (s < DateTime.UtcNow)
            {
                dictHealth[s] = 0;
                dictTimeline[s] = 0;
                dictMachine[s] = 0;

                dashboard.ChartLabels.Add(s.ToLocalTime().FlattenToHour().ToString());
                health.Data.Add(0);
                history.Data.Add(0);
                timeline.Data.Add(0);
                s = s.AddHours(1);
            }
            
            var oldest = this._context.Machines.Where(o => o.Status == StatusType.Active).OrderBy(o => o.CreatedUtc).Take(1).SingleOrDefault();
            if (oldest == null)
                return null;

            dashboard.MachinesTracked = this._context.Machines.Count(o => o.Status == StatusType.Active);
            dashboard.ClientOperations = this._context.HistoryTimeline.Count();

            var diff = DateTime.UtcNow - oldest.CreatedUtc;
            dashboard.HoursManaged = Math.Round((diff.TotalHours * dashboard.MachinesTracked), 2);

            var list = this._context.Machines.Where(o => o.Status == StatusType.Active).ToList();
            dashboard.MachinesWithHealthIssues = list.Count(o => o.StatusUp == Machine.UpDownStatus.Down || o.StatusUp == Machine.UpDownStatus.DownWithErrors);
            
            var queryDate = DateTime.UtcNow.FlattenToHour().AddHours(_hoursBack);
            
            var n = queryDate;
            while (n <= DateTime.UtcNow.FlattenToHour())
            {
                n = n.AddHours(1);
                if (!dictHealth.ContainsKey(n))
                    dictHealth[n] = 0;
                if (!dictTimeline.ContainsKey(n))
                    dictTimeline[n] = 0;
                if (!dictMachine.ContainsKey(n))
                    dictMachine[n] = 0;
                health.Data.Add(0);
                history.Data.Add(0);
                timeline.Data.Add(0);
            }

            var histories = this._context.HistoryHealth.Where(o => o.CreatedUtc > queryDate)
                .GroupBy(x => new DateTime(x.CreatedUtc.Year, x.CreatedUtc.Month, x.CreatedUtc.Day, x.CreatedUtc.Hour, 0, 0))
                .Select(g => new { Date = g.Key, Totals = g.Count() });
            foreach (var x in histories)
                dictHealth[x.Date] = x.Totals;

            histories = this._context.HistoryTimeline.Where(o => o.CreatedUtc > queryDate)
                .GroupBy(x => new DateTime(x.CreatedUtc.Year, x.CreatedUtc.Month, x.CreatedUtc.Day, x.CreatedUtc.Hour, 0, 0))
                .Select(g => new { Date = g.Key, Totals = g.Count() });
            foreach (var x in histories)
                dictTimeline[x.Date] = x.Totals;

            histories = this._context.HistoryMachine.Where(o => o.CreatedUtc > queryDate)
                .GroupBy(x => new DateTime(x.CreatedUtc.Year, x.CreatedUtc.Month, x.CreatedUtc.Day, x.CreatedUtc.Hour, 0, 0))
                .Select(g => new { Date = g.Key, Totals = g.Count() });
            foreach (var x in histories)
                dictMachine[x.Date] = x.Totals;

            var i = 0;
            foreach (var item in dictMachine)
            {
                history.Data[i] = item.Value;
                i++;
            }
            i = 0;
            foreach (var item in dictHealth)
            {
                health.Data[i] = item.Value;
                i++;
            }
            i = 0;
            foreach (var item in dictTimeline)
            {
                timeline.Data[i] = item.Value;
                i++;
            }

            dashboard.ChartItems.Add(health);
            dashboard.ChartItems.Add(timeline);
            dashboard.ChartItems.Add(history);

            watch.Stop();
            log.Trace($"Dashboard generated in {watch.ElapsedMilliseconds} milliseconds");

            return dashboard;
        }
    }

    public static class DateTimeExt
    {
        public static DateTime FlattenToHour(this DateTime self)
        {
            return new DateTime(self.Year, self.Month, self.Day, self.Hour, 0, 0);
        }

        public static DateTime FlattenToDay(this DateTime self)
        {
            return new DateTime(self.Year, self.Month, self.Day, 0, 0, 0);
        }
    }
}
