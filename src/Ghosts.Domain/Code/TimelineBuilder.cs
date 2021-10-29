// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Domain.Code
{
    /// <summary>
    /// Helper class that loads timeline and watches it for future changes
    /// </summary>
    public class TimelineBuilder
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public static string TimelineFile = ApplicationDetails.ConfigurationFiles.Timeline;

        public static FileInfo TimelineFilePath()
        {
            return new FileInfo(TimelineFile);
        }

        /// <summary>
        /// Get from local disk
        /// </summary>
        /// <returns>The local timeline to be executed</returns>
        public static Timeline GetLocalTimeline()
        {
            _log.Trace($"Loading timeline config {TimelineFile }");

            var raw = File.ReadAllText(TimelineFile);
            var timeline = JsonConvert.DeserializeObject<Timeline>(raw);
            if (timeline.Id == Guid.Empty)
            {
                timeline.Id = Guid.NewGuid();
                SetLocalTimeline(TimelineFile, timeline);
            }

            _log.Debug($"Timeline {timeline.Id} loaded successfully");
            
            return timeline;
        }

        public static Timeline GetLocalTimeline(string path)
        {
            try
            {
                var raw = File.ReadAllText(path);

                var timeline = JsonConvert.DeserializeObject<Timeline>(raw);
                if (timeline.Id == Guid.Empty)
                {
                    timeline.Id = Guid.NewGuid();
                    SetLocalTimeline(path, timeline);
                }

                return timeline;
            }
            catch
            {
                return null;
            }
        }

        public static string TimelineToString(Timeline timeline)
        {
            try
            {
                return JsonConvert.SerializeObject(timeline);
            }
            catch
            {
                // not a timeline?
                return null;
            }
        }
        
        /// <summary>
        /// Save to local disk
        /// </summary>
        /// <param name="timelineString">Raw timeline string (to be converted to `Timeline` type)</param>
        public static void SetLocalTimeline(string timelineString)
        {
            var timelineObject = JsonConvert.DeserializeObject<Timeline>(timelineString);
            SetLocalTimeline(timelineObject);
        }
        
        /// <summary>
        /// Save to local disk
        /// </summary>
        /// <param name="timeline">`Timeline` type</param>
        public static void SetLocalTimeline(Timeline timeline)
        {
            using (var file = File.CreateText(ApplicationDetails.ConfigurationFiles.Timeline))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(file, timeline);
            }
        }

        public static void SetLocalTimeline(string path, Timeline timeline)
        {
            using (var file = File.CreateText(path))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(file, timeline);
            }
        }
    }
}
