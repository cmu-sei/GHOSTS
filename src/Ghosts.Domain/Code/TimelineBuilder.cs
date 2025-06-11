// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Ghosts.Domain.Code.Helpers;
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

        public static async Task CheckForUrlTimeline(HttpClient client, string timelineConfig)
        {
            if (!timelineConfig.StartsWith("http")) return;

            try
            {
                var response = await client.GetAsync(timelineConfig);
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Http timeline file could not be found, falling back to local");

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content))
                    throw new Exception("Http timeline file is empty, falling back to local");

                try
                {
                    GetTimelineFromString(content, null);
                }
                catch (Exception)
                {
                    _log.Error($"Timeline fetched from {timelineConfig} is not in the correct format, falling back to local");
                    throw;
                }

                File.WriteAllText(TimelineFile, content);
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
        public static Timeline GetTimeline()
        {
            return GetTimeline(TimelineFile);
        }

        public static Timeline GetTimeline(string path)
        {
            try
            {
                var raw = File.ReadAllText(path);
                return GetTimelineFromString(raw, path);
            }
            catch
            {
                return null;
            }
        }

        public static Timeline GetTimelineFromString(string raw, string path)
        {
            _ = new Random();

            try
            {
                var (timeline, fileFormat) = ConfigManager.DeserializeConfig<Timeline>(raw);
                if (timeline.Id == Guid.Empty)
                {
                    timeline.Id = Guid.NewGuid();
                    if (!string.IsNullOrEmpty(path))
                    {
                        SetLocalTimeline(timeline, path);
                    }
                }

                _log.Debug($"Timeline {timeline.Id} loaded successfully");
                return timeline;
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return null;
            }
        }

        public static string TimelineToJsonPayload(Timeline timeline)
        {
            try
            {
                return ConfigManager.SerializeConfig(timeline, ConfigManager.FileFormat.Json);
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
            var timelineObject = GetTimelineFromString(timelineString, null);
            if (timelineObject != null)
            {
                SetLocalTimeline(timelineObject);
            }
            else
            {
                _log.Error("Attempt to write null timeline to disk...");
            }
        }

        /// <summary>
        /// Save to local disk
        /// </summary>
        /// <param name="timeline">`Timeline` type</param>
        public static void SetLocalTimeline(Timeline timeline)
        {
            ConfigManager.SaveConfig(timeline, TimelineFile, Formatting.Indented);
        }

        public static void SetLocalTimeline(Timeline timeline, string path)
        {
            ConfigManager.SaveConfig(timeline, path);
        }
    }
}
