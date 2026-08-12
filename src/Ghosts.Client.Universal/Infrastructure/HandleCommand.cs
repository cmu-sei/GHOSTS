// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Ghosts.Client.Universal.Infrastructure
{
    /// <summary>
    /// Runs a single handler action once and exits, for use as an atomic effector
    /// (e.g. driven by an external agent). This is a standalone entry point - it does
    /// not start the scheduler, sockets, listeners, or the normal agent loop.
    /// </summary>
    internal static class HandleCommand
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Executes one handler action described by the parsed options.
        /// </summary>
        /// <returns>0 on success, 1 on failure.</returns>
        internal static int Run(Options options)
        {
            if (!Enum.TryParse<HandlerType>(options.Handle, ignoreCase: true, out var type))
            {
                Console.Error.WriteLine(
                    $"Unknown handler '{options.Handle}'. Valid handlers: {string.Join(", ", Enum.GetNames(typeof(HandlerType)))}");
                return 1;
            }

            // In JSON mode stdout must carry only the JSON result, so send console logging to stderr
            // before anything else can log (e.g. the config loader).
            if (options.Json)
                RouteConsoleLoggingToStdErr();

            // Office COM handlers are Windows-only; transparently use the cross-platform Light variants elsewhere.
            var effectiveType = SubstituteForPlatform(type);
            if (effectiveType != type)
                _log.Trace($"Handler {type} substituted with {effectiveType} on this platform");

            // Some handlers (browsers, Outlook) read global config; load it best-effort so those don't NPE.
            try
            {
                Program.Configuration = ClientConfigurationLoader.Config;
            }
            catch (Exception e)
            {
                _log.Trace($"Configuration not loaded for handle mode (continuing): {e.Message}");
            }

            var handler = BuildHandler(effectiveType, options);
            var timeline = new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
            timeline.TimeLineHandlers.Add(handler);

            var capture = AttachResultCapture();

            try
            {
                using var cts = new CancellationTokenSource();
                Orchestrator.RunHandler(effectiveType, timeline, handler, cts.Token).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _log.Error($"Handler {effectiveType} failed: {e.Message}");
                _log.Debug(e);
                EmitResults(options, effectiveType, capture, success: false, error: e.Message);
                return 1;
            }

            EmitResults(options, effectiveType, capture, success: true, error: null);
            return 0;
        }

        private static HandlerType SubstituteForPlatform(HandlerType type)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return type;

            return type switch
            {
                HandlerType.Word => HandlerType.LightWord,
                HandlerType.Excel => HandlerType.LightExcel,
                HandlerType.PowerPoint => HandlerType.LightPowerPoint,
                _ => type
            };
        }

        private static TimelineHandler BuildHandler(HandlerType type, Options options)
        {
            var handler = new TimelineHandler
            {
                HandlerType = type,
                Loop = false // one-shot: run the action once and return
                // UtcTimeOn/UtcTimeOff left at zero so WorkingHours.Is() does not block
            };

            foreach (var kv in options.HandlerArgs ?? Enumerable.Empty<string>())
            {
                var idx = kv.IndexOf('=');
                if (idx <= 0) continue;
                handler.HandlerArgs[kv.Substring(0, idx)] = kv.Substring(idx + 1);
            }

            // Default browser handlers to headless so they run without a display, unless explicitly overridden.
            if (IsBrowser(type) && !handler.HandlerArgs.ContainsKey("isheadless"))
                handler.HandlerArgs["isheadless"] = "true";

            var timelineEvent = new TimelineEvent
            {
                Command = options.Command,
                DelayBefore = 0,
                DelayAfter = 0
            };
            foreach (var arg in options.Args ?? Enumerable.Empty<string>())
                timelineEvent.CommandArgs.Add(arg);

            handler.TimeLineEvents.Add(timelineEvent);
            return handler;
        }

        private static bool IsBrowser(HandlerType type) =>
            type is HandlerType.BrowserFirefox or HandlerType.BrowserChrome or HandlerType.BrowserEdge;

        /// <summary>
        /// Points any NLog console target at stderr so stdout stays reserved for the JSON result.
        /// </summary>
        private static void RouteConsoleLoggingToStdErr()
        {
            var config = LogManager.Configuration;
            if (config == null) return;

            foreach (var target in config.AllTargets.OfType<ConsoleTarget>())
                target.StdErr = true;

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Binds an in-memory NLog target to the TIMELINE logger so we can read back what
        /// the handler reported, without changing any handler code.
        /// </summary>
        private static MemoryTarget AttachResultCapture()
        {
            var config = LogManager.Configuration ?? new LoggingConfiguration();
            var mem = new MemoryTarget("handle-capture") { Layout = "${message}" };
            config.AddTarget(mem);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, mem, "TIMELINE");
            LogManager.Configuration = config; // reconfigures existing loggers
            return mem;
        }

        private static void EmitResults(Options options, HandlerType type, MemoryTarget capture, bool success, string error)
        {
            var records = new List<TimeLineRecord>();
            foreach (var line in capture.Logs)
            {
                // TIMELINE|<utc timestamp>Z|<json>
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                var json = string.Join("|", parts.Skip(2));
                try
                {
                    var record = JsonConvert.DeserializeObject<TimeLineRecord>(json);
                    if (record != null) records.Add(record);
                }
                catch
                {
                    // ignore malformed capture lines
                }
            }

            if (options.Json)
            {
                var payload = new
                {
                    handler = type.ToString(),
                    command = options.Command,
                    success,
                    error,
                    results = records
                };
                Console.WriteLine(JsonConvert.SerializeObject(payload, Formatting.Indented));
            }
            else
            {
                foreach (var record in records)
                    Console.WriteLine(record.Result);
                if (!success && !string.IsNullOrEmpty(error))
                    Console.Error.WriteLine(error);
            }
        }
    }
}
