@echo off
REM Dev container initializeCommand dispatcher (Windows hosts)
REM Paired with init - see devcontainer.json's initializeCommand.

REM -Ensure, not a devcontainer.env existence test: only setup can tell a spawned
REM project (env file present, every question already answered on the host) from one
REM where .devcontainer\ was copied in by hand (no env file, CUI/profiles/keys still
REM owed). Duplicating that judgement here would mean keeping two gates in step, so
REM setup decides what is missing - and stays silent when nothing is, which is every
REM re-open of every spawned project.
powershell -NoProfile -ExecutionPolicy Bypass -File ".devcontainer\scripts\setup.ps1" -Ensure
