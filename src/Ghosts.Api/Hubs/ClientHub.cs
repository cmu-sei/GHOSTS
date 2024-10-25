// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Api.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ghosts.Api.Hubs
{
    public class ClientHub(IMachineService machineService, IMachineUpdateService updateService, IBackgroundQueue queue) : Hub
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IMachineService _machineService = machineService;
        private readonly IBackgroundQueue _queue = queue;
        private readonly IMachineUpdateService _updateService = updateService;
        private readonly CancellationToken _ct = new CancellationToken();

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await SendId(null);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var m = await FindMachine();
            _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - Disconnecting...");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendId(string id)
        {
            var m = await FindMachine();
            if (m.HadId) return;
            id = m.Id.ToString();

            //client saves this for future calls
            _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveId");
            await Clients.Caller.SendAsync("ReceiveId", id, _ct);
        }

        public async Task SendResults(TransferLogDump message)
        {
            await Clients.Caller.SendAsync("ReceiveResults", message, _ct);
            throw new NotImplementedException();
        }

        public async Task SendSurvey(Survey message)
        {
            await Clients.Caller.SendAsync("ReceiveSurvey", message, _ct);
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
            var u = await _updateService.GetAsync(m.Id, m.CurrentUsername, _ct);
            if (u == null)
            {
                _log.Error("machine is invalid");
                return;
            }

            _log.Trace($"Update sent to {m.Id} {m.FQDN} {u.Id} {u.Username} {u.Update}");

            var update = new UpdateClientConfig { Type = u.Type, Update = u.Update };

            await _updateService.MarkAsDeletedAsync(u.Id, m.Id, _ct);

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

            _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveUpdates");
            await Clients.Caller.SendAsync("ReceiveUpdates", message, _ct);
        }

        public async Task SendTimeline(string message)
        {
            await Clients.Caller.SendAsync("ReceiveTimeline", "This is not implemented yet", _ct);
            throw new NotImplementedException();
        }

        public async Task SendHeartbeat(string message)
        {
            var m = GetMachine();

            _log.Trace(m.Id != Guid.Empty
                ? $"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveHeartbeat"
                : $"New machine — ({Context.ConnectionId}) - ReceiveHeartbeat");
            await Clients.Caller.SendAsync("ReceiveHeartbeat", DateTime.UtcNow, _ct);
        }

        public async Task SendMessage(string message)
        {
            var m = await FindMachine();
            _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveMessage");
            await Clients.All.SendAsync("ReceiveMessage", $"{message} {DateTime.UtcNow}", _ct);
        }

        public async Task SendSpecificMessage(string message)
        {
            var m = await FindMachine();
            _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveSpecificMessage");
            await Clients.Caller.SendAsync("ReceiveSpecificMessage", message, _ct);
        }

        private Machine GetMachine()
        {
            var machineResponse = new FindMachineResponse();
            var m = WebRequestReader.GetMachine(Context.GetHttpContext());
            return m;
        }

        private async Task<Machine> FindMachine()
        {
            var m = GetMachine();

            if (m.Id == Guid.Empty)
            {
                var findMachineResponse = await _machineService.FindOrCreate(Context.GetHttpContext(), _ct);
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
