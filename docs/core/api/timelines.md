# Managing Timelines

One of the primary capabilities of the API is to manage multiple client [timelines](../handlers.md). Timelines are the mechanism for clients to perform actions on behalf of a user. For example, a client might execute a timeline to browse a website, create a document, or other Handler activities.

Clients are configured to check in periodically with the API to report their current state and to check if any updates are available. We can have the API hold an update for the next time a client, group of clients, or all clients check in. That update will then be processed by the client and the client will report back to the API that the update has been processed. This allows the API to manage the state of the client and to ensure that the client is always up to date. So the process is:

1. We configure an update for a client or group of clients in the API.
2. Clients check in and are told that an update is available.
3. The client processes the update and reports back to the API that the update has been processed.

## Configuring a Timeline for a Client

Client timelines are updated via POST to /api/timelines in the following format:

```json
{
  "machineId": "3fa85f64-5717-4562-b3fc-2c963f66afaf",
  "type": 0,
  "activeUtc": "2023-03-08T16:38:04.002Z",
  "status": 0,
  "update": {
    "status": 0,
    "timeLineHandlers": [
      {
        "handlerType": 0,
        "initial": "string",
        "utcTimeOn": {
          "ticks": 0
        },
        "utcTimeOff": {
          "ticks": 0
        },
        "handlerArgs": {
          "additionalProp1": "string",
          "additionalProp2": "string",
          "additionalProp3": "string"
        },
        "loop": true,
        "timeLineEvents": [
          {
            "trackableId": "string",
            "command": "string",
            "commandArgs": [
              "string"
            ],
            "delayAfter": 0,
            "delayBefore": 0
          }
        ]
      }
    ]
  }
}
```

The key values to consider here are: The machine ID that you want to update, the other key value is the `type` of timeline. The type is an integer value that represents the type of update. The following are the types of updates that are currently supported:

- Timeline = 0            // this 
- Health = 1
- TimelinePartial = 10    // this the agent is currently doing off its default timeline
- RequestForTimeline = 20

| ID | Type               | Description                          |
|----| -----------        | ------------------------------------ |
|  0 | Timeline           | Replaces the client's default timeline stored within ./config/timeline.json                                             |
|  1 | Health             | This updates a client's health instructions                                                                             |
| 10 | TimelinePartial    | Does not replace the default timeline, rather this timeline is executed immediately on separate threads from whatever   |
| 20 | RequestForTimeline | Use this to instruct the client to send its current default timeline up to the API                                     |

The remainder of the settings are for the timeline - basically what we are doing here is sending a client a new timeline, with the above values to indicate, which machine and what to change in the `update` node.