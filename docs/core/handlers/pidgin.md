# Pidgin Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

Exercises a Pidgin client - tested with Pidgin 2.14.1 (libpurple 2.14.1) ane Centos 7.3 ejabberd server

## Prequisites
- Pidgin must be installed and already configured with an enabled account in %APPDATA%\.purple\accounts.xml and pointing to the target server. 
- The logged in user must have an enabled Pidgin account in accounts.xml
- Pidgin preferences must have already been set in  %APPDATA%\.purple\prefs.xml
- Conversations must be TABBED (in prefs.xml/conversations section, name='tabs' type='bool' value='1')

## Implementation
- This implementation is about 95% open loop as there are no C# bindings for the Pidgin libpurple.dll
- The only feedback to GHOSTS is via window titles, it cannot determine when messages arrive or message content.
- GHOSTS cannot parse the chat logs to synch converstations as the Pidgin process has these log files locked.
- So messages are sent open loop with simple delays between messages.
- The GHOSTS time line CommandArgs lists chat targets (username@domain)

Activity Cycle - each activity cycle is seperated by DelayAfter. An activity cycle does:

- Pick a random target from the timeline -  this is only used to initiate the first chat
- If Pidgin is not started then Pidgin is started.
- If an IM window is not open, the roll against NewChatProbability and open an IM window to the random target chosen from the timeline
- If roll against  NewChatProbability was not successful, end activity cycle.
- If an IM window is open and a new chat was not initiated, the roll against CloseChatProbability, if successful, close current chat and end activity cycle.
- If get to this point, then IM window is open with one or more targets and message loop is entered.
- Enter a loop in which between RepliesMin and RepliesMax messages are sent.
- The first message is sent to current selected target in the Chat window, then the next chat target in the Chat window is selected. If the max replies is reached, then the loop exits and the activity cycle is ended. The next activity cycle picks up where the last activity cycle ended as per the first chat target.

A chat target can be the current logged in user, which means messages are simply echoed back from the server.

- As chats arrive from other different users, the number of open tabs in the grows, but chats can be closed by CloseChatProbability
- Between 1-4 random emojis are added to a message based on EmojiProbability
- During an activity cycle, any popup windows that match a title in ErrorWindowTitles are closed


```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "Pidgin",
      "HandlerArgs": {
        "RepliesMin": 2,
        "RepliesMax": 5,
        "ErrorWindowTitles": [ "XMPP Message Error" ],
        "EmoticonProbability": 50,
        "NewChatProbability": 100,
        "CloseChatProbability": 100,
        "TimeBetweenMessagesMax": 10000,
        "TimeBetweenMessagesMin": 5000
      },
      "Initial": "",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
            "bjones@sitea.com",
            "pharvey@sitea.com",

          ],
          "DelayAfter": 20000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```
