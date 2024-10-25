using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Ghosts.Domain.Code
{
    public static class TimelineManager
    {
        public static IEnumerable<Timeline> GetLocalTimelines()
        {
            var timelines = new List<Timeline>
            {
                // get default timeline
                TimelineBuilder.GetTimeline()
            };

            var placesToLook = new List<string>
            {
                // look for instance timelines
                ApplicationDetails.InstanceDirectories.TimelineIn,
                ApplicationDetails.InstanceDirectories.TimelineOut
            };

            foreach (var placeToLook in placesToLook)
            {
                var d = new DirectoryInfo(placeToLook);

                foreach (var file in d.GetFiles("*.*"))
                {
                    // is this a timeline file?
                    try
                    {
                        var t = TimelineBuilder.GetTimeline(file.FullName);
                        if (t != null)
                        {
                            timelines.Add(t);
                        }
                    }
                    catch
                    {
                        // ignored: not a timeline
                    }
                }
            }

            return timelines;
        }
    }

    public static class TimelineUpdateClientConfigManager
    {
        public static Guid GetConfigUpdateTimelineId(UpdateClientConfig config)
        {
            var result = new Guid();

            try
            {
                if (config.Update != null)
                {
                    var id = JObject.Parse(config.Update.ToString().ToLower())["timelineid"].ToString();
                    Guid.TryParse(id, out result);
                }
            }
            catch
            {
                // ignored: not valid update config
            }

            return result;
        }
    }
}
