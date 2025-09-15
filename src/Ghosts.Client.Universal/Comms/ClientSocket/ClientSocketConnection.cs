// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Universal.Comms.ClientSocket;

public class Connection(ClientConfiguration.SocketsSettings options)
{
    private int _attempts;
    private HubConnection _connection;
    private readonly CancellationToken _ct = CancellationToken.None;
    public readonly BackgroundTaskQueue Queue = new();
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task Run()
    {
        var url = Program.ConfigurationUrls.Socket;
        _log.Trace($"Connecting to {url}...");
        while (_connection == null)
        {
            await EstablishConnection(url);
            _attempts++;
        }

        // Send a message to the server
        while (_connection.State == HubConnectionState.Connected)
        {
            _log.Trace($"Connected to {url}");
            _ = new Timer(_ =>
            {
                Task.Run(async () =>
                {
                    await ClientHeartbeat();
                }, _ct).ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        // Log or handle the exception
                        _log.Error($"Exception in ClientHeartbeat: {task.Exception}");
                    }
                }, _ct);
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(options.Heartbeat));

            while (true)
            {
                _log.Trace("Peeking into queue...");

                var item = await Queue.DequeueAsync(_ct);
                if (item != null)
                {
                    _log.Trace($"There was a {item.Type} in the queue: {item.Payload}");

                    switch (item.Type)
                    {
                        case QueueEntry.Types.Heartbeat:
                            await ClientHeartbeat();
                            break;
                        case QueueEntry.Types.Message:
                            await ClientMessage(item.Payload.ToString());
                            break;
                        case QueueEntry.Types.MessageSpecific:
                            await ClientMessageSpecific(item.Payload.ToString());
                            break;
                        case QueueEntry.Types.Results:
                            if (item.Payload is TransferLogDump r)
                                await ClientResults(r);
                            break;
                        case QueueEntry.Types.Survey:
                            if (item.Payload is Ghosts.Domain.Messages.MesssagesForServer.Survey s)
                                await ClientSurvey(s);
                            break;
                        case QueueEntry.Types.Updates:
                            await ClientUpdates("check");
                            break;
                        case QueueEntry.Types.Timeline:
                            await ClientTimeline(item.Payload.ToString());
                            break;
                    }
                }
                else
                {
                    await Task.Delay(new TimeSpan(0, 0, 10), _ct);
                }
            }
        }
    }

    async Task EstablishConnection(string url)
    {
        if (_connection != null && _connection.State != HubConnectionState.Disconnected)
        {
            await _connection.StopAsync(_ct);
        }

        var machine = new ResultMachine();
        GuestInfoVars.Load(machine);

        var headers = HttpClientBuilder.GetHeaders(machine);

        _connection = new HubConnectionBuilder()
            .WithUrl(url, x =>
            {
                x.Headers = headers;
            }).WithAutomaticReconnect()
            .Build();

        _log.Trace($"Connection state: {_connection.State}");

        // Define how to handle incoming messages
        _connection.On<string>("ReceiveHeartbeat",
            (message) => { _log.Trace($"Heartbeat {message}"); });

        _connection.On<string>("ReceiveMessage",
            (message) => { _log.Trace($"ALL: {message}"); });

        _connection.On<string>("ReceiveSpecificMessage",
            (message) => { _log.Trace($"SPECIFIC: {message}"); });

        _connection.On<string>("ReceiveId", (id) =>
        {
            _log.Trace($"ID: {id}");
            CheckId.WriteId(id);
        });

        _connection.On<TransferLogDump>("ReceiveResults",
            (message) => { _log.Trace($"Results: {message}"); });

        _connection.On<string>("ReceiveSurvey",
            (message) => { _log.Trace($"Survey: {message}"); });

        _connection.On<string>("ReceiveUpdate", (message) =>
        {
            _log.Trace($"Timeline: {message}");

            try
            {
                var update = JsonConvert.DeserializeObject<UpdateClientConfig>(message);

                if (update != null)
                {
                    try
                    {
                        var timeline = JsonConvert.DeserializeObject<Timeline>(update.Update.ToString() ?? string.Empty);

                        foreach (var timelineHandler in timeline.TimeLineHandlers)
                        {
                            //_log.Trace($"PartialTimeline found: {timelineHandler.HandlerType}");

                            foreach (var timelineEvent in timelineHandler.TimeLineEvents)
                            {
                                if (string.IsNullOrEmpty(timelineEvent.TrackableId))
                                {
                                    timelineEvent.TrackableId = Guid.NewGuid().ToString();
                                }
                            }

                            var orchestrator = new Orchestrator();
                            orchestrator.RunCommand(timeline, timelineHandler);
                        }
                    }
                    catch (Exception)
                    {
                        //pass
                        //_log.Debug(exc);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to process timeline: {ex.Message} from {message}");
            }
        });


        _connection.Closed += async (error) =>
        {
            _log.Trace($"Connection lost {error}. Trying to reconnect...");
            await EstablishConnection(url); // Call your reconnection method
        };

        try
        {
            await _connection.StartAsync(_ct); // Start the connection
        }
        catch (Exception ex)
        {
            if (_attempts > 1)
                _log.Error($"An error occurred at {url} while connecting: {ex.Message}");
        }
    }

    private async Task ClientHeartbeat()
    {
        await _connection?.InvokeAsync("SendHeartbeat", $"Client heartbeat at {DateTime.UtcNow}", _ct)!;
    }

    private async Task ClientMessage(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(message))
                await _connection?.InvokeAsync("SendMessage", $"Client message: {message}", _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async Task ClientMessageSpecific(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(message))
                await _connection?.InvokeAsync("SendSpecificMessage", $"Client specific message: {message}", _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async Task ClientResults(TransferLogDump message)
    {
        try
        {
            await _connection?.InvokeAsync("SendResults", message, _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async Task ClientSurvey(Ghosts.Domain.Messages.MesssagesForServer.Survey message)
    {
        try
        {
            await _connection?.InvokeAsync("SendSurvey", message, _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async Task ClientUpdates(string trigger)
    {
        try
        {
            await _connection?.InvokeAsync("SendUpdates", trigger, _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async Task ClientTimeline(string message)
    {
        try
        {
            await _connection?.InvokeAsync("SendTimeline", message, _ct)!;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

}
