<#
.SYNOPSIS
  Installs the GHOSTS Windows client and starts it at logon.

.DESCRIPTION
  Unpacks the client, points its config at the GHOSTS API, and registers a scheduled task that starts ghosts.exe
  when the NPC account logs on. The Office and browser handlers need an interactive session, so the client runs in
  the user's logon session rather than as a service.

  The host must report a hostname equal to its name in the terrain manifest: GHOSTS matches clients by exact name.

.EXAMPLE
  .\install.ps1 -ApiRootUrl http://ghosts-api:5000/api -Package .\ghosts-client-windows.zip -RunAs OFFICE\dokafor
#>
param(
    [Parameter(Mandatory = $true)][string]$ApiRootUrl,
    [Parameter(Mandatory = $true)][string]$Package,
    [string]$InstallDir = 'C:\ghosts',
    # The account the client runs as. Empty: whichever user logs on.
    [string]$RunAs = ''
)
$ErrorActionPreference = 'Stop'

Write-Host "Installing the GHOSTS client into $InstallDir"
Expand-Archive -Path $Package -DestinationPath $InstallDir -Force
# An instance folder copied from another host would carry its id.
Remove-Item (Join-Path $InstallDir 'instance') -Recurse -Force -ErrorAction SilentlyContinue

# Edit the package's own config so Windows-only keys are kept.
$configPath = Join-Path $InstallDir 'config\application.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$config.ApiRootUrl = $ApiRootUrl
$config.Id.IsEnabled = $false
$config.Sockets.IsEnabled = $true
$config | ConvertTo-Json -Depth 32 | Set-Content -Path $configPath -Encoding UTF8

$action = New-ScheduledTaskAction -Execute (Join-Path $InstallDir 'ghosts.exe') -WorkingDirectory $InstallDir
if ($RunAs) {
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $RunAs
    $principal = New-ScheduledTaskPrincipal -UserId $RunAs -LogonType Interactive -RunLevel Limited
} else {
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $principal = New-ScheduledTaskPrincipal -GroupId 'BUILTIN\Users' -RunLevel Limited
}
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName 'GHOSTS client' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

Write-Host "GHOSTS client installed. It starts at the next logon$(if ($RunAs) { " of $RunAs" })."
