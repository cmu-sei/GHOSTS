// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ghosts.Api.Hubs
{
    public class ClientHub : Hub
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineService _machineService;
        private readonly IBackgroundQueue _queue;
        private readonly IMachineUpdateService _updateService;
        private readonly CancellationToken _ct;
        
        public ClientHub(IMachineService machineService, IMachineUpdateService updateService, IBackgroundQueue queue)
        {
            this._ct = new CancellationToken();
            this._updateService = updateService;
            this._queue = queue;
            this._machineService = machineService;
        }
        
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await this.SendId(null);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var m = await FindMachine();
            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - Disconnecting...");
            await base.OnDisconnectedAsync(exception);
        }
        
        public async Task SendId(string id)
        {
            var m = await FindMachine();
            if (m.HadId) return;
            id = m.Id.ToString();
            
            //client saves this for future calls
            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveId");
            await Clients.Caller.SendAsync("ReceiveId", id, this._ct);
        }
        
        public async Task SendResults(TransferLogDump message)
        {
            await Clients.Caller.SendAsync("ReceiveResults", message, this._ct);
            throw new NotImplementedException();
        }
        
        public async Task SendSurvey(Survey message)
        {
            await Clients.Caller.SendAsync("ReceiveSurvey", message, this._ct);
            throw new NotImplementedException();
        }
        
        public async Task SendUpdates(string message)
        {
            var m = await FindMachine();
            _queue.Enqueue(
                new QueueEntry
                {
                    Payload =
                        new MachineQueueEntry
                        {
                            Machine = m,
                            LogDump = null,
                            HistoryType = Machine.MachineHistoryItem.HistoryType.RequestedUpdates
                        },
                    Type = QueueEntry.Types.Machine
                });

            //check dB for new updates to deliver
            var u = await _updateService.GetAsync(m.Id, m.CurrentUsername, this._ct);
            if (u == null)
            {
                _log.Error("machine is invalid");
                return;
            }

            _log.Trace($"Update sent to {m.Id} {m.FQDN} {u.Id} {u.Username} {u.Update}");
            
            var update = new UpdateClientConfig { Type = u.Type, Update = u.Update };

            await _updateService.DeleteAsync(u.Id, m.Id, this._ct);

            // integrators want to know that a timeline was actually delivered
            // (the service only guarantees that the update was received)
            _queue.Enqueue(
                new QueueEntry
                {
                    Payload =
                        new NotificationQueueEntry()
                        {
                            Type = NotificationQueueEntry.NotificationType.TimelineDelivered,
                            Payload = (JObject)JToken.FromObject(update)
                        },
                    Type = QueueEntry.Types.Notification
                });

            message = JsonSerializer.Serialize(update);

            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveUpdates");
            await Clients.Caller.SendAsync("ReceiveUpdates", message, this._ct);
        }
        
        public async Task SendTimeline(string message)
        {
            await Clients.Caller.SendAsync("ReceiveTimeline", "This is not implemented yet", this._ct);
            throw new NotImplementedException();
        }
        
        public async Task SendHeartbeat(string message)
        {
            var m = await FindMachine();
            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveHeartbeat");
            await Clients.Caller.SendAsync("ReceiveHeartbeat", DateTime.UtcNow, this._ct);
        }
        
        public async Task SendMessage(string message)
        {
            var m = await FindMachine();
            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveMessage");
            await Clients.All.SendAsync("ReceiveMessage", $"{message} {DateTime.UtcNow}", this._ct);
        }
    
        public async Task SendSpecificMessage(string message)
        {
            var m = await FindMachine();
            Console.WriteLine($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveSpecificMessage");
            await Clients.Caller.SendAsync("ReceiveSpecificMessage", message, this._ct);
        }

        private async Task<Machine> FindMachine()
        {
            var machineResponse = new FindMachineResponse();
            var m = WebRequestReader.GetMachine(Context.GetHttpContext());

            if (m.Id == Guid.Empty)
            {
                var findMachineResponse = await this._machineService.FindOrCreate(Context.GetHttpContext(), this._ct);
                if (!findMachineResponse.IsValid())
                {
                    _log.Error($"Could not find machine - {findMachineResponse.Error}");
                    throw new Exception($"Could not find machine - {findMachineResponse.Error}");
                }

                m = findMachineResponse.Machine;
            }
            else
            {
                m.HadId = true;
            }

            return m;
        }
    }
}