# Guacamole Helper Configuration


The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client.Windows/Sample%20Timelines/BrowserChromeGuacamole.json>)

The 'guacamole' command for a browser handler drives an [Apache Guacamole](https://guacamole.apache.org/) web gateway: it logs into the Guacamole website, opens a connection, types commands into it or holds it open for a while, and then logs out.

It is a `"Command": "guacamole"` event on the `BrowserChrome` or `BrowserFirefox` handlers and works in both the Windows and the Universal clients. `BrowserEdge` uses the same base handler, but it is untested. It has been tested against Guacamole 1.6.0 and 1.5.5.

The handlerArgs for the guacamole command are:

- "guacamole-url": `Guacamole base URL`, required, e.g. `https://guacamole.example.com/guacamole/`
- "guacamole-credentials-file": `json credentials file path`, credentials file, see SSh.json sample handler for format
- "guacamole-credentials": embedded credentials instead of an external file. If both are present, `guacamole-credentials` takes precedence.
- "guacamole-credential-key": `credential key`, required, the credential used to log into the Guacamole website
- "guacamole-execution-time": `milliseconds`, how long to hold a connection that has no commands, default 20000

The credentials use the standard GHOSTS credentials format, with base64 encoded passwords:

```json
{
  "Version": "1.0",
  "Data": {
    "guacuser": {"username":"guacuser","password":"pw1base64"},
    "sshuser": {"username":"sshuser","password":"pw2base64"}
  }
}
```

The CommandArgs are strings of the form `connectionName|credKey|cmd1;cmd2`. One entry is picked at random for each event.

- The entry is split on `|` into at most 3 parts, so a `|` inside the commands is kept (e.g. `ps aux | grep sshd`).
- connectionName: the name of the Guacamole connection. It must match exactly one connection the Guacamole user can see; a missing or ambiguous name is refused.
- credKey: the credential key whose username and password are typed at the terminal login prompt. Leave it empty (`connectionName||cmd1`) when the credentials are stored in the Guacamole connection; nothing is typed for the login then. Do not give a credKey for such a connection: the helper cannot see whether a login prompt is showing, so the username and password would be typed into the remote shell (and end up in its history).
- cmd1;cmd2: the commands, separated by `;`. Use `\;` to type a literal `;` (written `\\;` in JSON). The shell on the remote still interprets that `;`, so quote it as needed, e.g. `echo 'a\\;b'`. To type a literal `\;` (as in `find . -exec ls {} \;`), write `\\;` (`\\\\;` in JSON).
- With no commands (`connectionName|` or `connectionName|credKey|`) the session is just held open for `guacamole-execution-time`, then closed. This is the RDP/VNC use case.

SSH vs RDP is decided by Guacamole, not by the timeline. After connecting, the helper reads the connection's protocol from Guacamole. Commands (and the terminal login) are typed only on `ssh` and `telnet` connections. An entry with commands for a connection with any other protocol is refused: an error is logged, nothing is typed, the session is closed and nothing is reported.

When the Guacamole user has a single connection, Guacamole connects to it automatically after login. The helper then checks that the connected name matches connectionName and refuses the entry if it does not.

Each event logs in, does its work and logs out, so Guacamole sessions do not accumulate. If a session from an earlier event is still logged in, it is logged out first.

On success, the event is reported with the handler type, command `guacamole` and arg `connectionName|commands`. Credentials are not reported, but the commands are, so do not put secrets in commands.

Limitations:

- There are fixed 2 second waits around the terminal login and after each command. Slow targets, host-key prompts or MOTD prompts are not handled.
- Connections inside balancing connection groups cannot be reached.
- The same connection name in two places is refused as ambiguous.
- Characters outside the Unicode Basic Multilingual Plane (e.g. emoji) are not supported, and control characters other than newline and tab are dropped.
- On Windows, the cleanup of a concurrently running `BrowserChrome` handler kills all Chrome processes, including the one used by this handler (existing behaviour of that handler).

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "BrowserChrome",
      "HandlerArgs": {
        "isheadless": "false",
        "command-line-args": [ "--ignore-certificate-errors" ],
        "guacamole-url": "https://guacamole.example.com/guacamole/",
        "guacamole-credentials": {
          "Version": "1.0",
          "Data": {
            "guacuser": {"username":"guacuser","password":"pw1base64"},
            "sshuser": {"username":"sshuser","password":"pw2base64"}
          }
        },
        "guacamole-credential-key": "guacuser",
        "guacamole-execution-time": 60000
      },
      "Initial": "about:blank",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "guacamole",
          "CommandArgs": [
            "linux-ssh-server|sshuser|whoami;hostname",
            "linux-ssh-stored||uptime",
            "linux-ssh-server|sshuser|ps aux | grep sshd;echo 'first\\; second' > /tmp/ghosts.txt",
            "windows-rdp-desktop|"
          ],
          "DelayAfter": 60000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```

Instead of placing the credentials in the timeline, they can be read from a file with the `HandlerArgs[guacamole-credentials-file]` key, e.g. `"guacamole-credentials-file": "c:\\ghosts_data\\guacamole_creds.json"`. Placing the credentials directly in the timeline is a better approach if the GhostsApi server is used to update the timeline as this makes the timeline self-contained.
