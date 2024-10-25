// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace Ghosts.Client.Lite.Infrastructure.Comms.ClientSocket;

public class QueueEntry
{
    public enum Types
    {
        Id,
        Heartbeat,
        Message,
        MessageSpecific,
        Timeline
    }

    public object Payload { get; set; }
    public Types Type { get; set; }
}
