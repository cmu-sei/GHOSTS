@echo off
REM Dev container initializeCommand dispatcher (Windows hosts)
REM Paired with init — see devcontainer.json's initializeCommand.

if not exist ".devcontainer\devcontainer.env" (
    powershell -NoProfile -ExecutionPolicy Bypass -File ".devcontainer\setup.ps1"
)
