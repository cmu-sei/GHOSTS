# CLAUDE.md

## Working Rules

- Think before coding. State assumptions. Ask when blocked.
- Prefer the smallest correct change.
- Make surgical edits. Do not refactor adjacent code.
- Match existing style.
- Every changed line must trace to the task.
- Define verification before implementation.
- For bugs: Reproduce first, then fix, then verify.
- For multi-file changes: Plan first, then implement.
- Do not add features, abstractions, configurability, or speculative error handling unless asked.
- Do not delete unrelated dead code. Mention it instead.
- Rverify that you followed ALL of the CLAUDE.md rules before build.

## Architecture Invariants

- Timelines are the core execution model: `Timeline -> TimeLineHandlers -> TimeLineEvents`.
- New client behavior usually means adding/extending a handler in `Ghosts.Domain`.
- New handlers must be registered in `HandlerType`.
- API/server state lives in PostgreSQL and should change through migrations.
- Clients report execution results back through the reporting/queue path.
- SignalR is used for real-time client/server communication.
- Do not assume authentication is built in; deployment may rely on an auth proxy.

## Development
- Simplicity, concision, and readability matter much more than flexibility, abstractions, hypothetical reuse, etc.
- If something is unclear or confusing, stop and ask questions. Never assume.

## Verification

Before finishing:
- Run the narrowest relevant build/test command.
- For API changes: Run relevant .NET build/tests.
- For frontend changes: Run `npm test` or `npm run build`.
- For schema changes: Add an EF migration.
- Report exactly what was run and what passed or failed.
