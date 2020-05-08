// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Newtonsoft.Json.Linq;

namespace Ghosts.Api.Models
{
    public class QueueEntry
    {
        public enum Types
        {
            Notification,
            Machine,
            Survey
        }

        public object Payload { get; set; }
        public Types Type { get; set; }
    }

    public class MachineQueueEntry
    {
        public Machine Machine { get; set; }
        public TransferLogDump LogDump { get; set; }
        public Machine.MachineHistoryItem.HistoryType HistoryType { get; set; }
    }

    public class NotificationQueueEntry
    {
        public enum NotificationType
        {
            Timeline = 0,
            WebhookCreate = 1
        }

        public NotificationType Type { get; set; }
        public JObject Payload { get; set; }
    }
}