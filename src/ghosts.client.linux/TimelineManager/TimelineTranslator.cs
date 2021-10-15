using System;
using System.Collections.Generic;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace ghosts.client.linux.timelineManager
{
    public static class TimelineTranslator
    {
        public static TimelineHandler FromBrowserUnitTests(IEnumerable<string> commands)
        {
            var timelineHandler = new TimelineHandler();

            timelineHandler.HandlerType = HandlerType.BrowserFirefox;
            timelineHandler.Initial = "about:blank";
            timelineHandler.Loop = false;

            foreach (var command in commands)
            {
                var timelineEvent = new TimelineEvent();
                timelineEvent.DelayBefore = 0;
                timelineEvent.DelayAfter = 3000;
                
                // determine command and commandArgs
                if (command.StartsWith("driver.Navigate()", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "browse";
                    timelineEvent.CommandArgs.Add(command.GetTextBetweenQuotes());
                }
                else if (command.StartsWith("driver.Manage().Window.Size", StringComparison.InvariantCultureIgnoreCase))
                {
                    timelineEvent.Command = "manage.window.size";
                    var s = command.Split("Size(")[1].Replace(");", "").Replace(" ","").Split(",");
                    var width = Convert.ToInt32(s[0]);
                    var height = Convert.ToInt32(s[1]);
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
                else if(command.StartsWith("driver.FindElement(", StringComparison.InvariantCultureIgnoreCase) && command.EndsWith(").Click();", StringComparison.InvariantCultureIgnoreCase))
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
                
                if(!string.IsNullOrEmpty(timelineEvent.Command) && timelineEvent.CommandArgs.Count > 0)
                    timelineHandler.TimeLineEvents.Add(timelineEvent);
            }
            
            return timelineHandler;
        }
    }
}