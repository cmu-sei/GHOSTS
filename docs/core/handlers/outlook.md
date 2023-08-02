# Outlook Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

```json
{
    "TimeLineHandlers": [
        {
            "HandlerType": "Outlook",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": "True",
            "TimeLineEvents": [
                {
                    "Command": "create",
                    "CommandArgs": [
                        "CurrentUser",
                        "Random|Other:string ToEmailAddress - comma separate multiples",
                        "Random|Other:string CcEmailAddress - comma separate multiples",
                        "Random|Other:string BccEmailAddress - comma separate multiples",
                        "Random|Other:string Subject",
                        "Random|Other:string Body",
                        "PlainText|RTF|HTML enum BodyType",
                        "string Attachments - comma separate multiples"
                    ],
                    "DelayAfter": 900000,
                    "DelayBefore": 0
                },
              {
                "Command": "reply",
                "CommandArgs": [
                  "CurrentUser",
                  "All - reply to all",
                  "All",
                  "All",
                  "Parent - format is 'RE: <original message/>'",
                  "Random+Parent - format is reply then original message",
                  "Parent - format of original message",
                  ""
                ],
                "DelayAfter": 900000,
                "DelayBefore": 0
              },
              {
                "Command": "clickrandomlink",
                "CommandArgs": [],
                "DelayAfter": 900000,
                "DelayBefore": 0
              }
            ]
        },
        {
            "HandlerType": "Outlook",
            "Initial": "",
            "UtcTimeOn": "00:00:00",
            "UtcTimeOff": "24:00:00",
            "Loop": "True",
            "TimeLineEvents": [
                {
                    "Command": "create",
                    "CommandArgs": [
                        "CurrentUser",
                        "random",
                        "random",
                        "random",
                        "Random",
                        "Random",
                        "PlainText",
                        ""
                    ],
                    "DelayAfter": 900000,
                    "DelayBefore": 0
                },
              {
                "Command": "reply",
                "CommandArgs": [
                  "CurrentUser",
                  "All",
                  "All",
                  "All",
                  "Parent",
                  "Parent+Random",
                  "Parent",
                  ""
                ],
                "DelayAfter": 900000,
                "DelayBefore": 0
              },
              {
                "Command": "clickrandomlink",
                "CommandArgs": [],
                "DelayAfter": 900000,
                "DelayBefore": 0
              }
            ]
        }
    ]
}
```

Some of the key-value pairs are self-explanatory, but let's review a few important ones:

| Key                       | Value                                                                                                                                             |
| ----------------------    |  ---------------------------------------                                                                                                          |
| `Command`                 | (create) Create new emails. (reply) Reply to emails in the current inbox at random (clickrandomlink) Click a link at random in the current inbox  |
| `CommandArgs`&nbsp;       | See below                                                                                                                                         |

For CommandArgs, we began with positional arguments, but this quickly became unwieldy. We are now using named arguments, which are easier to read and maintain, but the mix remains for now. The following arguments are available for creating new emails (create):

- [0] "CurrentUser": The current user's email address, or indicate the email address you want to use here. Note that if Outlook is not configured to use this email address, email may not be sent.
- [1] "Random|Other": This configures the TO address in an email where "Random" picks email addresses from the configuration directory. If you want to specify particular addresses, this field can also be comma-separated email addresses.
- [2] "Random|Other": This configures the CC address in an email where "Random" picks email addresses from the configuration directory. If you want to specify particular addresses, this field can also be comma-separated email addresses.
- [3] "Random|Other": This configures the BCC address in an email where "Random" picks email addresses from the configuration directory. If you want to specify particular addresses, this field can also be comma-separated email addresses.
- [4] "Random|Other:string Subject",
- [5] "Random|Other:string Body",
- [6] "PlainText|RTF|HTML enum BodyType",
- [7] "string Attachments - comma separate multiples"

For replying to emails (reply):

- [0] "CurrentUser": The current user's email address, or indicate the email address you want to use here. Note that if Outlook is not configured to use this email address, email may not be sent.
- [1] "All": Reply to all on the original thread, or reply to a specific user on the original email thread. If you want to reply to a specific user, indicate the email address here.
- [2] "All": Reply to all the addresses in the original email thread CC field. If you want to reply to a specific user, indicate the email address here.
- [3] "All": Reply to all the addresses in the original email thread BCC field. If you want to reply to a specific user, indicate the email address here.
- [4] "Parent": Subject line of the reply. "Parent" format is 'RE: $original_message$'
- [5] "Random+Parent": Body of the reply. "Random+Parent" format is the reply then the original message below - as is typically seen in email threads.
- [6] "Parent": HTML or plain text. "Parent" uses the format of the original message.
- [7] "": Not used in replies.
