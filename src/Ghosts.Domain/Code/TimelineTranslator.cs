// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Domain.Code
{
    public static class TimelineTranslator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static TimelineHandler FromBrowserUnitTests(IEnumerable<string> commands)
        {
            var timelineHandler = new TimelineHandler
            {
                HandlerType = HandlerType.BrowserFirefox,
                Initial = "about:blank",
                Loop = false
            };

            foreach (var command in commands)
            {
                TimelineEvent timelineEvent = null;
                try
                {
                    timelineEvent = GetEvent(command.Trim());
                }
                catch (Exception exc)
                {
                    _log.Error(exc);
                }

                if (timelineEvent != null && !string.IsNullOrEmpty(timelineEvent.Command) && timelineEvent.CommandArgs.Count > 0)
                    timelineHandler.TimeLineEvents.Add(timelineEvent);
            }

            return timelineHandler;
        }

        private static TimelineEvent GetEvent(string command)
        {
            var timelineEvent = new TimelineEvent
            {
                DelayBefore = 0,
                DelayAfter = 3000
            };

            if (command.StartsWith("driver.Quit", StringComparison.InvariantCultureIgnoreCase)) return timelineEvent;

            // determine command and commandArgs
            if (command.StartsWith("driver.Navigate()", StringComparison.InvariantCultureIgnoreCase))
            {
                timelineEvent.Command = "browse";
                timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
            }
            else if (command.StartsWith("driver.Manage().Window.Size", StringComparison.InvariantCultureIgnoreCase))
            {
                timelineEvent.Command = "manage.window.size";
                var size = command.Split("Size(").Last().Replace(");", "").Replace(" ", "").Split(Convert.ToChar(","));
                var width = Convert.ToInt32(size[0]);
                var height = Convert.ToInt32(size[1]);
                timelineEvent.CommandArgs.Add(width);
                timelineEvent.CommandArgs.Add(height);
            }
            else if (command.StartsWith("js.", StringComparison.InvariantCultureIgnoreCase))
            {
                if (command.StartsWith("js.ExecuteScript(", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "js.executescript";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
            }
            else if (command.StartsWith("driver.FindElement(", StringComparison.InvariantCultureIgnoreCase) &&
                     command.EndsWith(").Click();", StringComparison.InvariantCultureIgnoreCase))
            {
                if (command.StartsWith("driver.FindElement(By.LinkText(", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "click.by.linktext";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
                else if (command.StartsWith("driver.FindElement(By.Id", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "click.by.id";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
                else if (command.StartsWith("driver.FindElement(By.Name", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "click.by.name";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
                else if (command.StartsWith("driver.FindElement(By.CssSelector", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "click.by.cssselector";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
            }

            return timelineEvent;
        }
    }
}
