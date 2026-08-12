# Running a Single Handler (`--handle`)

The Universal client can run **one handler action and exit**, instead of loading a full timeline and running as a long-lived agent. This turns each GHOSTS handler into an atomic, invokable primitive — useful when an external system (for example, an agent/LLM harness) decides *what* an NPC should do, and GHOSTS simply executes *how*.

???+ info "This does not start the agent"
    `--handle` is a standalone entry point. It never starts the scheduler, sockets, listeners, or the normal timeline loop. Running the client with no flags behaves exactly as before.

## Usage

```bash
dotnet ghosts.client.universal.dll --handle <handler> --command <verb> [--arg <value> ...] [--handler-arg key=value ...] [--json]
```

| Flag            | Description                                                                 |
| --------------- | --------------------------------------------------------------------------- |
| `--handle`      | The handler to run (e.g. `bash`, `browserfirefox`, `curl`). Case-insensitive. |
| `--command`     | The handler command/verb to execute (e.g. `browse`, `random`).              |
| `--arg`         | A command argument. Repeat the flag for multiple arguments.                 |
| `--handler-arg` | A handler option as `key=value`. Repeat for multiple.                       |
| `--json`        | Emit the result as JSON to stdout (see [Output](#output)).                  |

The values map directly onto a single timeline event: `--command` becomes the event's `Command`, each `--arg` is appended to `CommandArgs`, and each `--handler-arg` is added to the handler's `HandlerArgs`. The action is run once (`Loop` is `false`), and working-hours gating is disabled so it runs immediately.

## Examples

Run a shell command:

```bash
dotnet ghosts.client.universal.dll --handle bash --command "whoami"
```

Browse a URL with Firefox (headless — see [below](#browsers)):

```bash
dotnet ghosts.client.universal.dll --handle browserfirefox --command browse --arg "https://example.com"
```

Pass multiple arguments and a handler option:

```bash
dotnet ghosts.client.universal.dll --handle bash --command "echo one" --arg "echo two" --handler-arg execution-probability=100
```

## Output

By default, each result is printed to stdout in human-readable form, with the banner and logs alongside it.

With `--json`, stdout carries **only** the JSON result — the banner and all console logging are redirected to stderr — so the output can be consumed by another program:

```bash
dotnet ghosts.client.universal.dll --handle bash --command "echo hello" --json 2>/dev/null
```

```json
{
  "handler": "Bash",
  "command": "echo hello",
  "success": true,
  "error": null,
  "results": [
    {
      "Handler": "Command",
      "Command": "echo hello",
      "Result": "hello\n"
    }
  ]
}
```

- `success` reflects whether the **handler** completed without throwing. It does not reflect the exit status of a command the handler ran (for example, `bash` running a nonexistent binary still reports `success: true`, because the handler itself ran).
- `results` contains the records the handler reported for this action (the same records normally written to `logs/clientupdates.log`).
- The process exit code is `0` on success and `1` on failure (unknown handler, or the handler threw).

## Platform notes

### Office handlers

The `Word`, `Excel`, and `PowerPoint` handlers rely on Office COM automation and only run on Windows. On non-Windows platforms, `--handle word|excel|powerpoint` transparently runs the cross-platform `LightWord`/`LightExcel`/`LightPowerPoint` variants instead, which write the corresponding document file without a live Office application. The `handler` field in the JSON output reflects the handler that actually ran.

`Outlook` and `Outlookv2` are already cross-platform (MailKit-based) and run as-is.

### Browsers

Browser handlers (`browserfirefox`, `browserchrome`, `browseredge`) default to headless mode so they can run without a display. Pass `--handler-arg isheadless=false` to override. As with timeline-driven browsing, the appropriate browser and its automation driver must be installed (see [the client overview](client.md#windows-installation)).
