// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
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

        public static void CheckForUrlTimeline(WebClient client, string timelineConfig)
        {
            if (!timelineConfig.StartsWith("http")) return;

            try
            {
                using (var stream = client.OpenRead(timelineConfig))
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            if (string.IsNullOrEmpty(content))
                                throw new Exception("Http timeline file could not be found, falling back to local");

                            try
                            {
                                JsonConvert.DeserializeObject<Timeline>(content);
                            }
                            catch (Exception)
                            {
                                _log.Error($"Timeline fetched from {timelineConfig} is not in the correct format, falling back to local");
                                throw;
                            }

                            File.WriteAllText(TimelineFile, content);
                        }
            }
            catch (Exception e)
            {
                _log.Error($"Http timeline file could not be found, falling back to local: {e}");
            }
        }

        /// <summary>
        /// Get from local disk
        /// </summary>
        /// <returns>The local timeline to be executed</returns>
        public static Timeline GetLocalTimeline()
        {
            return GetLocalTimeline(TimelineFile);
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
                
                _log.Debug($"Timeline {timeline.Id} loaded successfully");
                return timeline;
            }
            catch
            {
                return null;
            }
        }

        public static Timeline StringToTimeline(string raw)
        {
            try
            {
                var timeline = JsonConvert.DeserializeObject<Timeline>(raw);
                return timeline;
            }
            catch
            {
                _log.Debug($"String is not a timeline: {raw}");
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
