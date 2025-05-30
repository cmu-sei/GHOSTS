// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Client.ClientSocket;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.SignalR.Client;
using Exception = System.Exception;

namespace Ghosts.Client.Comms.ClientSocket;

public class Connection
{
    private int _attempts = 0;
    private HubConnection _connection;
    private readonly CancellationToken _ct = new();
    public readonly BackgroundTaskQueue Queue = new();
    private readonly ClientConfiguration.SocketsSettings _options;

    public Connection(ClientConfiguration.SocketsSettings options)
    {
        this._options = options;
    }

    public async Task Run()
    {
        var url = Program.ConfigurationUrls.Socket;
        Console.WriteLine($"Connecting to {url}...");
        while (_connection == null)
        {
            await EstablishConnection(url);
            _attempts++;
        }

        Console.WriteLine($"Connected to {url}");

        // Send a message to the server
        while (_connection.State == HubConnectionState.Connected)
        {
            _ = new Timer(_ => {
                Task.Run(async () => {
                    await ClientHeartbeat();
                }, _ct).ContinueWith(task => {
                    if (task.Exception != null)
                    {
                        // Log or handle the exception
                        Console.WriteLine($"Exception in ClientHeartbeat: {task.Exception}");
                    }
                }, _ct);
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.Heartbeat));

            while (true)
            {
                Console.WriteLine("Peeking into queue...");

                var item = await Queue.DequeueAsync(this._ct);
                if (item != null)
                {
                    Console.WriteLine($"There was a {item.Type} in the queue: {item.Payload}");
                    if (item.Type == QueueEntry.Types.Heartbeat)
                        await this.ClientHeartbeat();

                    if (item.Type == QueueEntry.Types.Message)
                        await this.ClientMessage(item.Payload.ToString());
                    
                    if (item.Type == QueueEntry.Types.MessageSpecific)
                        await this.ClientMessageSpecific(item.Payload.ToString());
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

        _connection = new HubConnectionBuilder()
            .WithUrl(url, x =>
            {
                x.Headers = WebClientBuilder.GetHeaders(machine, true);
            }).WithAutomaticReconnect()
            .Build();
        
        Console.WriteLine($"Connection state: {_connection.State}");

        // Define how to handle incoming messages
        _connection.On<string>("ReceiveHeartbeat",
            (message) => { Console.WriteLine($"Heartbeat {message}"); });

        _connection.On<string>("ReceiveMessage",
            (message) => { Console.WriteLine($"ALL: {message}"); });

        _connection.On<string>("ReceiveSpecificMessage",
            (message) => { Console.WriteLine($"SPECIFIC: {message}"); });

        _connection.On<string>("ReceiveId", (id) =>
        {
            Console.WriteLine($"ID: {id}");
            CheckId.WriteId(id);
        });

        _connection.On<TransferLogDump>("ReceiveResults",
            (message) => { Console.WriteLine($"Results: {message}"); });

        _connection.On<string>("ReceiveSurvey",
            (message) => { Console.WriteLine($"Survey: {message}"); });

        _connection.On<string>("ReceiveUpdates",
            (message) => { Console.WriteLine($"Updates: {message}"); });

        _connection.On<string>("ReceiveTimeline",
            (message) => { Console.WriteLine($"Timeline: {message}"); });

        _connection.Closed += async (error) =>
        {
            Console.WriteLine($"Connection lost {error}. Trying to reconnect...");
            await EstablishConnection(url); // Call your reconnection method
        };

        try
        {
            await _connection.StartAsync(_ct); // Start the connection
        }
        catch (Exception ex)
        {
            if(_attempts > 1)
                Console.WriteLine($"An error occurred at {url} while connecting: {ex.Message}");
        }
    }

    private async Task ClientHeartbeat()
    {
        await _connection?.InvokeAsync("SendHeartbeat", $"Client heartbeat at {DateTime.UtcNow}", this._ct)!;
    }
    
    private async Task ClientMessage(string message)
    {
        if(!string.IsNullOrEmpty(message))
            await _connection?.InvokeAsync("SendMessage", $"Client message: {message}", this._ct)!;
    }
    
    private async Task ClientMessageSpecific(string message)
    {
        if(!string.IsNullOrEmpty(message))
            await _connection?.InvokeAsync("SendSpecificMessage", $"Client specific message: {message}", this._ct)!;
    }
}