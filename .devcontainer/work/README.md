# Work Area

This is a personal project work area. It is **not tracked by version control** —
everything you create inside `.devcontainer/work/` (except this README) is ignored by
git, so it never shows up in `git status`, never gets committed, and never appears in a
pull request.

## Purpose

This directory is the starting working directory for **Open Terminal** — the sandbox
the Open WebUI chat uses to run shell commands, read and write files, and execute code.
When the AI creates or edits files on your behalf, they land here by default, keeping
scratch work, generated artifacts, and downloaded uploads out of the tracked repository.

Use it for:

- Scratch files, notes, and experiments
- Output and artifacts the AI generates for you
- Copies of uploaded files you want to work on directly

## Notes

- Only `.devcontainer/work/README.md` is tracked. The `.gitignore` rule in
  `.devcontainer/` (`work/*` with `!work/README.md`) ignores everything else in here.
- The workspace is a bind mount from your host, so files here persist across container
  rebuilds — but they live only on your machine, not in the repository.
- If you want something to be version-controlled, move it out of `.devcontainer/work/`
  into the tracked part of the repository.
