// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

/// <summary>
/// Protocol-level XMPP client that connects to an XMPP server via TCP/TLS,
/// authenticates with PLAIN SASL, and sends chat messages to specified JIDs.
/// </summary>
public class Pidgin(Timeline timeline, TimelineHandler handler, CancellationToken token)
    : BaseHandler(timeline, handler, token)
{
    private static readonly string[] WordList =
    {
        "hello", "how", "are", "you", "doing", "today", "great", "thanks",
        "sure", "let", "me", "know", "sounds", "good", "talk", "later",
        "meeting", "tomorrow", "project", "update", "quick", "question",
        "appreciate", "help", "working", "on", "that", "right", "now",
        "check", "email", "sent", "earlier", "lunch", "break", "coffee",
        "team", "deadline", "friday", "status", "report", "review",
        "document", "shared", "folder", "looks", "fine", "agreed",
        "morning", "afternoon", "evening", "weekend", "plans", "busy",
        "free", "available", "schedule", "call", "chat", "soon"
    };

    protected override Task RunOnce()
    {
        try
        {
            // Parse HandlerArgs
            string xmppServer = null;
            var xmppPort = 5222;
            string xmppUsername = null;
            string xmppPassword = null;
            string xmppDomain = null;
            var useTls = false;
            var timeBetweenMessagesMax = 10000;
            var timeBetweenMessagesMin = 4000;
            var repliesMin = 1;
            var repliesMax = 6;
            var jitterFactor = 0;

            if (Handler.HandlerArgs != null)
            {
                if (Handler.HandlerArgs.TryGetValue("xmpp-server", out var serverVal))
                    xmppServer = serverVal.ToString();

                if (Handler.HandlerArgs.TryGetValue("xmpp-port", out var portVal))
                {
                    if (int.TryParse(portVal.ToString(), out var port))
                        xmppPort = port;
                }

                if (Handler.HandlerArgs.TryGetValue("xmpp-username", out var userVal))
                    xmppUsername = userVal.ToString();

                if (Handler.HandlerArgs.TryGetValue("xmpp-password", out var passVal))
                    xmppPassword = passVal.ToString();

                if (Handler.HandlerArgs.TryGetValue("xmpp-domain", out var domainVal))
                    xmppDomain = domainVal.ToString();

                if (Handler.HandlerArgs.TryGetValue("xmpp-use-tls", out var tlsVal))
                {
                    if (bool.TryParse(tlsVal.ToString(), out var tls))
                        useTls = tls;
                }

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenMessagesMax", out var maxVal))
                {
                    if (int.TryParse(maxVal.ToString(), out var max) && max >= 0)
                        timeBetweenMessagesMax = max;
                }

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenMessagesMin", out var minVal))
                {
                    if (int.TryParse(minVal.ToString(), out var min) && min >= 0)
                        timeBetweenMessagesMin = min;
                }

                if (Handler.HandlerArgs.TryGetValue("RepliesMin", out var rMinVal))
                {
                    if (int.TryParse(rMinVal.ToString(), out var rMin) && rMin >= 0)
                        repliesMin = rMin;
                }

                if (Handler.HandlerArgs.TryGetValue("RepliesMax", out var rMaxVal))
                {
                    if (int.TryParse(rMaxVal.ToString(), out var rMax) && rMax >= 0)
                        repliesMax = rMax;
                }

                if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterVal))
                    jitterFactor = Jitter.JitterFactorParse(jitterVal.ToString());
            }

            // Validate required config
            if (string.IsNullOrEmpty(xmppServer) || string.IsNullOrEmpty(xmppUsername) ||
                string.IsNullOrEmpty(xmppPassword) || string.IsNullOrEmpty(xmppDomain))
            {
                _log.Error("Pidgin:: Missing required XMPP configuration (xmpp-server, xmpp-username, xmpp-password, xmpp-domain).");
                return Task.CompletedTask;
            }

            foreach (var timelineEvent in Handler.TimeLineEvents)
            {
                Token.ThrowIfCancellationRequested();
                WorkingHours.Is(Handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                if (timelineEvent.CommandArgs == null || timelineEvent.CommandArgs.Count == 0)
                {
                    _log.Trace("Pidgin:: No CommandArgs (target JIDs) specified, skipping event.");
                    continue;
                }

                var numMessages = _random.Next(repliesMin, repliesMax + 1);
                for (var i = 0; i < numMessages; i++)
                {
                    Token.ThrowIfCancellationRequested();

                    var target = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)].ToString();
                    if (string.IsNullOrEmpty(target))
                        continue;

                    var messageBody = GenerateRandomMessage();

                    try
                    {
                        SendXmppMessage(xmppServer, xmppPort, xmppUsername, xmppPassword, xmppDomain, useTls, target, messageBody);
                        _log.Trace($"Pidgin:: Sent XMPP message to {target}.");
                        Report(new ReportItem { Handler = "Pidgin", Command = target, Arg = "xmpp-message", Trackable = timelineEvent.TrackableId });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Pidgin:: Failed to send message to {target}: {e.Message}");
                    }

                    if (i < numMessages - 1)
                    {
                        var delay = _random.Next(timeBetweenMessagesMin, timeBetweenMessagesMax + 1);
                        delay = Jitter.JitterFactorDelay(delay, jitterFactor);
                        Thread.Sleep(delay);
                    }
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterFactor));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        return Task.CompletedTask;
    }

    private void SendXmppMessage(string server, int port, string username, string password, string domain, bool useTls, string targetJid, string body)
    {
        using var tcpClient = new TcpClient();
        tcpClient.Connect(server, port);

        Stream stream = tcpClient.GetStream();

        if (useTls)
        {
            // Open initial stream to request STARTTLS
            var initStream = $"<stream:stream to='{domain}' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' version='1.0'>";
            WriteToStream(stream, initStream);
            ReadFromStream(stream);

            // Send STARTTLS
            var startTls = "<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>";
            WriteToStream(stream, startTls);
            var tlsResponse = ReadFromStream(stream);

            if (!tlsResponse.Contains("<proceed", StringComparison.OrdinalIgnoreCase))
            {
                _log.Error("Pidgin:: STARTTLS not supported or rejected by server. Aborting connection (TLS required).");
                return;
            }

            // Upgrade to TLS
            var sslStream = new SslStream(stream, false, (sender, certificate, chain, errors) => true);
            sslStream.AuthenticateAsClient(server);
            stream = sslStream;
        }

        // Open XMPP stream
        var openStream = $"<stream:stream to='{domain}' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' version='1.0'>";
        WriteToStream(stream, openStream);
        ReadFromStream(stream);

        // Authenticate with PLAIN SASL
        var authPayload = $"\0{username}\0{password}";
        var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authPayload));
        var authStanza = $"<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>{authBase64}</auth>";
        WriteToStream(stream, authStanza);
        var authResponse = ReadFromStream(stream);

        if (!authResponse.Contains("<success", StringComparison.OrdinalIgnoreCase))
        {
            _log.Error("Pidgin:: SASL authentication failed.");
            return;
        }

        // Re-open stream after auth
        WriteToStream(stream, openStream);
        ReadFromStream(stream);

        // Bind resource
        var bindStanza = "<iq type='set' id='bind1'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></iq>";
        WriteToStream(stream, bindStanza);
        ReadFromStream(stream);

        // Send message
        var escapedBody = System.Security.SecurityElement.Escape(body);
        var messageStanza = $"<message to='{targetJid}' type='chat'><body>{escapedBody}</body></message>";
        WriteToStream(stream, messageStanza);

        // Close stream
        var closeStream = "</stream:stream>";
        WriteToStream(stream, closeStream);

        try
        {
            ReadFromStream(stream);
        }
        catch
        {
            // Server may close connection immediately; that is acceptable
        }
    }

    private static void WriteToStream(Stream stream, string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static string ReadFromStream(Stream stream)
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();

        // Set a short read timeout to avoid blocking indefinitely
        if (stream is NetworkStream ns)
            ns.ReadTimeout = 5000;

        try
        {
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            while (bytesRead > 0)
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                // Check if more data is available without blocking
                if (stream is NetworkStream netStream && !netStream.DataAvailable)
                    break;
                if (stream is SslStream)
                {
                    // For SslStream, we rely on the initial read being sufficient
                    break;
                }

                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
        }
        catch (IOException)
        {
            // Read timeout - we have what we need
        }

        return sb.ToString();
    }

    private string GenerateRandomMessage()
    {
        var wordCount = _random.Next(3, 12);
        var words = new List<string>(wordCount);
        for (var i = 0; i < wordCount; i++)
        {
            words.Add(WordList[_random.Next(0, WordList.Length)]);
        }

        var message = string.Join(" ", words);
        // Capitalize first letter
        if (message.Length > 0)
            message = char.ToUpper(message[0]) + message.Substring(1);

        return message;
    }
}
