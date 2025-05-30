using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.TimelineManager;
using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;
using Quartz;

namespace Ghosts.Client.Infrastructure
{
    public class CronScheduling
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private Guid _id;

        public CronScheduling()
        {
            this._id = Guid.NewGuid();
        }

        public ITrigger GetTrigger(TimelineHandler handler)
        {
            var o = TriggerBuilder.Create()
                .WithIdentity(this._id.ToString(), handler.HandlerType.ToString())
                .StartNow()
                .WithCronSchedule(handler.Schedule)
                .Build();
            _log.Trace($"{handler.HandlerType} {o.JobKey} {o.Description} {o.StartTimeUtc} {o.EndTimeUtc} {o.JobDataMap}");
            return o;
        }

        public IJobDetail GetJob(TimelineHandler handler)
        {
            var o = JobBuilder.Create<HandlerJob>()
                .WithIdentity(this._id.ToString(), handler.HandlerType.ToString());
            o.UsingJobData("handler", JsonConvert.SerializeObject(handler));
            var j = o.Build();
            _log.Trace($"{handler.HandlerType} {j.Key} {j.Description} {j.JobType}");
            return j;
        }

        internal class HandlerJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                _log.Trace("Cron handler job firing");

                var dataMap = context.JobDetail.JobDataMap;

                var s = new StringBuilder();
                foreach (var data in dataMap)
                {
                    s.Append(" -").Append(data.Key).Append(' ').Append(data.Value);
                    if (data.Key.Equals("sleep"))
                        Thread.Sleep((int)data.Value);
                    // Console.WriteLine(data.Key);

                    try
                    {
                        var handler = JsonConvert.DeserializeObject<TimelineHandler>(data.Value.ToString());
                        var timeline = new Timeline();
                        timeline.TimeLineHandlers.Add(handler);

                        var orchestrator = new Orchestrator();
                        orchestrator.RunCommandCron(timeline, handler);
                    }
                    catch
                    {
                        _log.Trace("Could not schedule cron job, deserialization failed");
                    }
                }
            }
        }
    }
}
