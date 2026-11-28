// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using OpenQA.Selenium;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Handles Apache Guacamole actions for BaseBrowserHandler.
    /// Each event picks one CommandArgs entry "connectionName|credKey|cmd1;cmd2", logs into Guacamole,
    /// opens the connection, types the commands (ssh/telnet only) or holds the session, then logs out.
    /// </summary>
    public class GuacamoleHelper : BrowserHelper
    {
        private const int WaitTimeoutMs = 60000;
        private const int PollIntervalMs = 500;
        private const int PromptWaitMs = 2000;
        private const int ChunkSize = 60;
        private const int ChunkGapMs = 150;
        private const int DefaultExecutionTimeMs = 20000;

        private const string UsernameSelector = ".login-ui input[name='username']";
        private const string PasswordSelector = ".login-ui input[name='password']";
        private const string LoginButtonSelector = ".login-ui input[name='login']";
        private const string FilterSelector = ".home-view input.search-string";

        private const string Injector = "angular.element(document.body).injector()";
        private const string ConnectedClients = "(function(){var m=" + Injector + ".get('guacClientManager').getManagedClients();" +
            "return Object.keys(m).map(function(k){return m[k];}).filter(function(c){return c.clientState.connectionState==='CONNECTED';});})()";

        // predicates, evaluated as "return !!(<predicate>);"
        private const string LoginFormPredicate = "document.querySelector(\"" + UsernameSelector + "\")";
        private const string HomeOrClientPredicate = "document.querySelector('.home-view:not(.loading), .client-view')";
        private const string ClientViewPredicate = "document.querySelector('.client-view')";
        // Guacamole sets the managed client's name and protocol asynchronously, so wait for those too
        private const string ConnectedPredicate = "(function(c){return c.length===1&&c[0].name&&c[0].protocol;})(" + ConnectedClients + ")" +
            "&&document.querySelector('.client-view .display')";

        // scripts
        private const string OpenConnectionScript = "var n=arguments[0];" +
            "var m=Array.prototype.filter.call(document.querySelectorAll('a.home-connection .name'),function(e){return e.textContent.trim()===n;});" +
            "if(m.length===1)m[0].closest('a.home-connection').click();return m.length;";
        private const string ClientInfoScript = "var c=" + ConnectedClients + ";return c.length===1?[String(c[0].name),String(c[0].protocol)]:null;";
        private const string SendKeysScript = "var c=" + ConnectedClients + "[0].client;" +
            "arguments[0].forEach(function(k){c.sendKeyEvent(1,k);c.sendKeyEvent(0,k);});";
        private const string LogoutScript = "return " + Injector + ".get('authenticationService').logout();";

        public GuacamoleHelper(BaseBrowserHandler callingHandler, IWebDriver callingDriver)
        {
            baseHandler = callingHandler;
            Driver = callingDriver;
        }

        public void Execute(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            var url = Arg(handler, "guacamole-url");
            if (string.IsNullOrEmpty(url))
            {
                Log.Error("Guacamole:: Handler option 'guacamole-url' must be specified, guacamole browser action will not be executed.");
                return;
            }

            var entry = timelineEvent.CommandArgs?.Count > 0
                ? timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)]?.ToString()
                : null;
            if (!ParseEntry(entry, out var connection, out var credKey, out var commandsPart))
            {
                Log.Error($"Guacamole:: CommandArgs entry '{entry}' must be 'connectionName|credKey|commands', guacamole browser action will not be executed.");
                return;
            }

            var executionTime = DefaultExecutionTimeMs;
            var executionArg = Arg(handler, "guacamole-execution-time");
            if (executionArg != null && (!int.TryParse(executionArg, out executionTime) || executionTime < 0))
            {
                Log.Error($"Guacamole:: Handler option 'guacamole-execution-time' value '{executionArg}' is not a valid number of milliseconds, guacamole browser action will not be executed.");
                return;
            }

            string webUser, webPassword, termUser = null, termPassword = null;
            try
            {
                var inline = Arg(handler, "guacamole-credentials");
                var file = Arg(handler, "guacamole-credentials-file");
                var credentials = JsonConvert.DeserializeObject<Credentials>(inline ?? (file == null ? string.Empty : File.ReadAllText(file)));
                if (credentials == null) throw new Exception("no 'guacamole-credentials' or 'guacamole-credentials-file' given");

                var webKey = Arg(handler, "guacamole-credential-key") ?? string.Empty;
                webUser = credentials.GetUsername(webKey);
                webPassword = credentials.GetPassword(webKey);
                if (webUser == null || webPassword == null) throw new Exception($"credential key '{webKey}' does not return a valid credential");

                if (credKey != string.Empty)
                {
                    termUser = credentials.GetUsername(credKey);
                    termPassword = credentials.GetPassword(credKey);
                    if (termUser == null || termPassword == null) throw new Exception($"credential key '{credKey}' does not return a valid credential");
                }
            }
            catch (JsonException)
            {
                Log.Error("Guacamole:: Error parsing guacamole credentials. Guacamole browser action will not be executed.");
                return;
            }
            catch (Exception e)
            {
                Log.Error($"Guacamole:: Error reading guacamole credentials: {e.Message}. Guacamole browser action will not be executed.");
                return;
            }

            var commands = SplitCommands(commandsPart);
            var js = (IJavaScriptExecutor)Driver;
            try
            {
                Driver.Navigate().GoToUrl(url);
                if (!WaitFor(js, LoginFormPredicate + "||" + HomeOrClientPredicate))
                {
                    Log.Error($"Guacamole:: Guacamole page did not load at {url}.");
                    return;
                }

                if (!IsTrue(js, LoginFormPredicate))
                {
                    //stale session from an earlier event: log out and start over
                    RunGuarded(js, LogoutScript);
                    Driver.Navigate().Refresh();
                    if (!WaitFor(js, LoginFormPredicate))
                    {
                        Log.Error("Guacamole:: Login form not found.");
                        return;
                    }
                }

                Driver.FindElement(By.CssSelector(UsernameSelector)).SendKeys(webUser);
                Driver.FindElement(By.CssSelector(PasswordSelector)).SendKeys(webPassword);
                Driver.FindElement(By.CssSelector(LoginButtonSelector)).Click();

                if (!WaitFor(js, HomeOrClientPredicate))
                {
                    Log.Error("Guacamole:: Login failed or no connections are available.");
                    return;
                }

                //with a single connection Guacamole opens it straight away, otherwise click the matching tile
                if (!IsTrue(js, ClientViewPredicate))
                {
                    //filtering by name also lists connections beyond the first page (20 tiles) and inside connection groups
                    Driver.FindElement(By.CssSelector(FilterSelector)).SendKeys(connection);
                    var matches = Convert.ToInt32(js.ExecuteScript(OpenConnectionScript, connection));
                    if (matches != 1)
                    {
                        Log.Error($"Guacamole:: Expected exactly one connection named '{connection}', found {matches}.");
                        return;
                    }
                }

                if (!WaitFor(js, ConnectedPredicate))
                {
                    Log.Error($"Guacamole:: Connection '{connection}' did not reach the CONNECTED state.");
                    return;
                }

                var info = js.ExecuteScript(ClientInfoScript) as IReadOnlyList<object>;
                var name = info?[0] as string;
                var protocol = (info?[1] as string)?.ToLowerInvariant();
                if (name != connection)
                {
                    Log.Error($"Guacamole:: Connected to '{name}' instead of '{connection}'; nothing typed.");
                    return;
                }

                var terminal = protocol == "ssh" || protocol == "telnet";
                if (commands.Count > 0 && !terminal)
                {
                    Log.Error($"Guacamole:: Connection '{connection}' has protocol '{protocol ?? "unknown"}', commands are only typed on ssh/telnet; nothing typed.");
                    return;
                }

                if (terminal && termUser != null)
                {
                    Pause(PromptWaitMs);
                    TypeLine(js, termUser);
                    Pause(PromptWaitMs);
                    TypeLine(js, termPassword);
                    Pause(PromptWaitMs);
                }

                foreach (var command in commands)
                {
                    TypeLine(js, command);
                    Pause(PromptWaitMs); //also keeps the last command from being cut off by the logout
                }

                if (commands.Count == 0)
                {
                    Pause(executionTime);
                }

                baseHandler.Report(new ReportItem
                {
                    Handler = handler.HandlerType.ToString(),
                    Command = "guacamole",
                    Arg = connection + "|" + commandsPart,
                    Trackable = timelineEvent.TrackableId
                });
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException)
                {
                    throw;
                }
                Log.Error(e);
            }
            finally
            {
                //logging out revokes the auth token, which also closes the connection
                RunGuarded(js, LogoutScript);
            }
        }

        // parses "connectionName|credKey|cmd1;cmd2"; pipes inside the commands part are kept
        public static bool ParseEntry(string entry, out string connection, out string credKey, out string commandsPart)
        {
            var parts = entry?.Split(new[] { '|' }, 3);
            var valid = parts != null && parts.Length >= 2 && parts[0].Trim() != string.Empty;
            connection = valid ? parts[0].Trim() : null;
            credKey = valid ? parts[1].Trim() : null;
            commandsPart = valid ? (parts.Length == 3 ? parts[2] : string.Empty) : null;
            return valid;
        }

        // splits on ';' ("\;" is a literal ';'), trims and drops empty commands
        public static List<string> SplitCommands(string commandsPart)
        {
            var commands = new List<string>();
            foreach (var raw in Regex.Split(commandsPart ?? string.Empty, @"(?<!\\);"))
            {
                var command = raw.Replace("\\;", ";").Trim();
                if (command != string.Empty) commands.Add(command);
            }

            return commands;
        }

        // converts text to the X11 keysyms Guacamole.Client.sendKeyEvent expects
        public static List<int> ToKeysyms(string text)
        {
            var keysyms = new List<int>();
            foreach (var c in text)
            {
                if ((c >= 0x20 && c <= 0x7E) || (c >= 0xA0 && c <= 0xFF)) keysyms.Add(c);
                else if (c == '\n') keysyms.Add(0xff0d);
                else if (c == '\t') keysyms.Add(0xff09);
                else if (c > 0xFF && !char.IsSurrogate(c)) keysyms.Add(0x01000000 | c);
                //other control characters and characters outside the BMP are skipped
            }

            return keysyms;
        }

        private static string Arg(TimelineHandler handler, string key)
        {
            return handler.HandlerArgs.TryGetValue(key, out var v) ? v?.ToString() : null;
        }

        // types the text and Enter, in small chunks so slow targets keep up
        private void TypeLine(IJavaScriptExecutor js, string text)
        {
            var keysyms = ToKeysyms(text + "\n");
            for (var i = 0; i < keysyms.Count; i += ChunkSize)
            {
                if (i > 0) Pause(ChunkGapMs);
                js.ExecuteScript(SendKeysScript, keysyms.GetRange(i, Math.Min(ChunkSize, keysyms.Count - i)));
            }
        }

        private bool WaitFor(IJavaScriptExecutor js, string jsPredicate)
        {
            var end = DateTime.UtcNow.AddMilliseconds(WaitTimeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (IsTrue(js, jsPredicate)) return true;
                Pause(PollIntervalMs);
            }

            return IsTrue(js, jsPredicate);
        }

        private static bool IsTrue(IJavaScriptExecutor js, string jsPredicate)
        {
            try
            {
                return js.ExecuteScript("return !!(" + jsPredicate + ");") is true;
            }
            catch (WebDriverException)
            {
                return false; //page or Guacamole app not ready yet
            }
        }

        private static void RunGuarded(IJavaScriptExecutor js, string script)
        {
            try
            {
                js.ExecuteScript(script);
            }
            catch (Exception e)
            {
                Log.Trace($"Guacamole:: Cleanup script failed: {e.Message}");
            }
        }

        private void Pause(int ms)
        {
            Thread.Sleep(ms);
        }
    }
}
