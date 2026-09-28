# rename.ps1 - Rename this dev container project (folder + Docker resources)
# Run from the HOST terminal (outside the container).
#
# Usage: .devcontainer\scripts\rename.ps1 <new-name>

param(
    [Parameter(Position = 0)]
    [string]$NewName
)

# --- Helpers ---

function Exit-WithUsage {
    Write-Host "Usage: .devcontainer\scripts\rename.ps1 <new-name>"
    Write-Host ""
    Write-Host "  Renames the project folder and deletes the old Docker container"
    Write-Host "  and volume. They will be recreated when you reopen in VS Code."
    Write-Host ""
    Write-Host "  Must be run from the host terminal (outside the container)."
    exit 1
}

function Test-DockerContainer($Name) {
    docker inspect --type container $Name 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

function Test-DockerVolume($Name) {
    docker volume inspect $Name 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

# --- Argument parsing ---

if ([string]::IsNullOrEmpty($NewName)) {
    Exit-WithUsage
}

# Validate folder name
if ($NewName -match '[/\\]' -or $NewName.StartsWith('.') -or $NewName.StartsWith('-') -or $NewName -match '\s') {
    Write-Host "Error: '$NewName' is not a valid folder name."
    Write-Host "  Must not contain '/', '\', or spaces, or start with '-' or '.'"
    exit 1
}

# Check docker is available
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Error: 'docker' is not in PATH. This script must be run from the host."
    exit 1
}

# --- Derive names ---

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# .devcontainer\scripts -> .devcontainer -> the project folder.
$DevcontainerDir = Split-Path -Parent $ScriptDir
$ProjectDir = Split-Path -Parent $DevcontainerDir
$OldName = Split-Path -Leaf $ProjectDir
$ParentDir = Split-Path -Parent $ProjectDir
$NewProjectDir = Join-Path $ParentDir $NewName

if ($OldName -eq $NewName) {
    Write-Host "New name is the same as the current name. Nothing to do."
    exit 0
}

$OldContainer = $OldName
$OldVolData = "$OldName-data"

# --- Pre-flight checks ---

if (Test-Path $NewProjectDir) {
    Write-Host "Error: '$NewProjectDir' already exists."
    exit 1
}

if (Test-DockerContainer $NewName) {
    Write-Host "Error: A Docker container named '$NewName' already exists."
    exit 1
}

if (Test-DockerVolume "$NewName-data") {
    Write-Host "Error: Docker volume '$NewName-data' already exists."
    exit 1
}

# Probe which old resources exist
$ContainerExists = Test-DockerContainer $OldContainer
$VolDataExists = Test-DockerVolume $OldVolData

# --- Confirmation prompt ---

Write-Host ""
Write-Host "This will:"
Write-Host "  Rename folder: $OldName -> $NewName"

if ($ContainerExists) {
    Write-Host "  Delete container: $OldContainer"
}
if ($VolDataExists) {
    Write-Host "  Delete volume: $OldVolData"
}

Write-Host ""
Write-Host "The container and volume will be recreated when you reopen in VS Code."
Write-Host ""
$response = Read-Host "Proceed? [y/N]"
if ($response -notmatch '^[yY]([eE][sS])?$') {
    Write-Host "Aborted."
    exit 0
}

Write-Host ""

# --- Delete Docker resources (before folder rename so we can exit cleanly on failure) ---

if ($ContainerExists) {
    # Stop if running
    $status = docker inspect --format='{{.State.Status}}' $OldContainer 2>$null
    if ($status -eq "running") {
        Write-Host "Stopping container '$OldContainer'..."
        docker stop $OldContainer 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to stop container '$OldContainer'."
            exit 1
        }
    }

    Write-Host "Removing container '$OldContainer'..."
    docker rm $OldContainer 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to remove container '$OldContainer'."
        exit 1
    }
}

if ($VolDataExists) {
    Write-Host "Removing volume '$OldVolData'..."
    docker volume rm $OldVolData 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to remove volume '$OldVolData'."
        exit 1
    }
}

# --- Rename folder (last step) ---

Write-Host "Renaming folder: $OldName -> $NewName"
try {
    Rename-Item -Path $ProjectDir -NewName $NewName -ErrorAction Stop
} catch {
    Write-Host "Error: Failed to rename folder."
    Write-Host "Docker resources were already deleted. They will be recreated"
    Write-Host "when you reopen the project in VS Code."
    exit 1
}

# --- Done ---

Write-Host ""
Write-Host "Renamed: $OldName -> $NewName"
Write-Host ""
Write-Host "Open the project in VS Code to create the new container:"
Write-Host ""
Write-Host "  code $NewProjectDir"
Write-Host ""
