# Dev Container Setup Script for Windows
#
# Usage:
#   .\setup.ps1              First run: CUI data question, profile selection, credentials
#                            Re-run:   Prompts credentials for previously configured profiles
#   .\setup.ps1 <profile>    Update credentials for a specific profile (e.g., aws, opal, etc)
#   .\setup.ps1 -Spawning    Pre-container run from spawn.ps1: CUI data question, profile
#               [-Chat]      selection, feature selection, credentials (skippable) and Git
#               [-Git]       init - everything, so the container create asks nothing
#   .\setup.ps1 -Ensure      What initializeCommand runs: a silent no-op once
#                            devcontainer.env exists, and the first-run flow when it does
#                            not (a project that adopted .devcontainer\ without a spawn)
#
# Normally invoked automatically by .devcontainer\scripts\init.cmd via initializeCommand.
# Run manually only to update credentials in an existing devcontainer.env.

param(
    [string[]]$Profiles,
    [switch]$Spawning,
    [switch]$Chat,
    [switch]$Git,
    [switch]$Ensure
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Sibling resources live one level up, in .devcontainer\ itself.
$DevcontainerDir = Split-Path -Parent $ScriptDir
$ProfilesDir = Join-Path $DevcontainerDir "profiles"
$DevEnv = Join-Path $DevcontainerDir "devcontainer.env"
# script: qualified because it is only ever read inside functions (Set-FeatureState).
$script:DcJson = Join-Path $DevcontainerDir "devcontainer.json"

if ($Spawning -and $Profiles.Count -gt 0) {
    Write-Host "ERROR: -Spawning takes no profile arguments."
    exit 1
}

$ProjectDir = Split-Path -Parent $DevcontainerDir
$ProjectName = Split-Path -Leaf $ProjectDir
$Esc = [char]27

# Marks a checkout of agent-dev ITSELF rather than a project built from it. Tracked in
# agent-dev's root and deleted by spawn's clean slate, so it exists in exactly one place: a
# clone of the template repo. Its only effect is to make Select-CuiHandling skip the
# profile deletion - see there (and choose_cui in setup.sh) for why.
$TemplateMarker = Join-Path $ProjectDir ".template-container"

# Can this run actually ask the user something? The bash side answers this by opening
# /dev/tty (every prompt there reads < /dev/tty, so redirected stdin is irrelevant);
# PowerShell has no equivalent, and Read-Host reads STDIN.
#
# [Environment]::UserInteractive alone is NOT the answer: it reports whether the process
# has a user-interactive session, and stays True when stdin is a file or /dev/null - so
# Read-Host returns "" at EOF instead of throwing, and every prompt silently answers
# itself with the empty string. For a [y/N] question that reads as "no", which is how
# Select-CuiHandling came to delete the CUI-approved profiles unasked when run as
# "pwsh -File setup.ps1 < /dev/null". [Console]::IsInputRedirected is what detects that.
#
# Both halves are needed: no session (a service, a scheduled task) and a redirected stdin
# (a pipeline, a CI step) are different conditions and either one makes prompting wrong.
function Test-CanPrompt {
    return ([Environment]::UserInteractive -and -not [Console]::IsInputRedirected)
}

# Write devcontainer.env with LF line endings, NOT Set-Content's CRLF.
# The file is consumed two ways, and only one tolerates CR: Docker's --env-file
# (runArgs) trims a trailing \r, but postcreate.sh reads the file DIRECTLY with grep
# to parse CONFIGURED_PROFILES, where a stray \r ends up inside the last CSV element
# and every profile path built from it misses. postcreate.sh strips CR defensively as
# well; keep BOTH - this side stops the corruption at the source (any other in-container
# reader benefits), that side handles env files written by other tools or editors.
function Write-EnvFile {
    param([string]$Path, [string[]]$Lines)
    [System.IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"))
}

# --- Banner ---
# Printed at most once, and only when this run is actually going to say something:
# -Ensure runs on every container start and is silent when there is nothing to finish,
# so it defers the banner to the first real prompt.
$script:BannerShown = $false
function Show-Banner {
    if ($script:BannerShown) { return }
    $script:BannerShown = $true
    $HeaderText = "$ProjectName Dev Container Setup"
    $HeaderBorder = [string][char]0x2500 * ($HeaderText.Length + 2)
    Write-Host ""
    Write-Host "$([char]0x250C)$HeaderBorder$([char]0x2510)"
    Write-Host "$([char]0x2502) $Esc[1;38;5;141m$ProjectName$Esc[0m Dev Container Setup $([char]0x2502)"
    Write-Host "$([char]0x2514)$HeaderBorder$([char]0x2518)"
}
if (-not $Ensure) { Show-Banner }

# --- Helper: prompt for a profile's empty env vars ---
# Returns a hashtable of VAR=VALUE pairs for the prompted vars.
#
# -Optional (spawn mode only): an empty answer SKIPS the var instead of being an error,
# so someone who has not generated a key yet can still finish the spawn. The var is then
# left out of the hashtable, Write-DevEnvFile leaves it blank, and `setup.ps1 <profile>`
# fills it in later. Everywhere else a blank answer is still a hard error - update mode
# would otherwise patch a real value away.
function Get-ProfileCredentials {
    param([string]$ProfileName, [switch]$Optional)

    $EnvFile = Join-Path $ProfilesDir "$ProfileName\profile.env"
    if (-not (Test-Path $EnvFile)) {
        Write-Host "ERROR: Profile '$ProfileName' not found."
        return $null
    }

    Write-Host ""
    Write-Host "  $ProfileName"
    Write-Host ""

    $Result = @{}
    foreach ($Line in (Get-Content $EnvFile)) {
        if ($Line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($Line)) { continue }

        if ($Line -match '^([A-Za-z_][A-Za-z0-9_]*)=$') {
            $VarName = $Matches[1]
            $IsSensitive = $VarName -match 'SECRET|_KEY$'

            if ($IsSensitive) {
                $SecureValue = Read-Host $VarName -AsSecureString
                # PtrToStringBSTR, not PtrToStringAuto: a BSTR is always UTF-16, while
                # Auto decodes at the platform's default char size. Those agree on
                # Windows PowerShell 5.1 (the host init.cmd uses) but not in pwsh on
                # macOS/Linux, where Auto stops at the first NUL byte and silently
                # yields a ONE-CHARACTER secret. ZeroFreeBSTR is the matching free, so
                # the plaintext copy doesn't linger in memory.
                $Bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
                try {
                    $Value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($Bstr)
                } finally {
                    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr)
                }
            } else {
                $Value = Read-Host $VarName
            }

            if ([string]::IsNullOrEmpty($Value)) {
                if ($Optional) { continue }
                Write-Host "ERROR: $VarName is required."
                return $null
            }

            $Result[$VarName] = $Value
        }
    }
    return $Result
}

# --- Helper: collect every selected profile's credentials ---
# Shared by spawn mode and the adopted-project first run; Write-DevEnvFile consumes the
# result. Values are deduplicated across profiles (aws and awsgov share AWS_* names).
# -Optional is passed straight through to Get-ProfileCredentials.
#
# Returns the hashtable; the Write-Host calls do not pollute it (Write-Host does not
# write to the pipeline), but do not add a bare expression to this function.
function Read-AllCredentials {
    param([switch]$Optional)

    $Collected = @{}

    Write-Host ""
    Write-Host "  Configure Credentials"
    if ($Optional) {
        Write-Host ""
        Write-Host "  Press Enter to skip a key you have not generated yet - you can add it"
        Write-Host "  later with .devcontainer\scripts\setup.ps1 <profile>."
    }

    foreach ($Prof in $script:Selected) {
        $Creds = Get-ProfileCredentials -ProfileName $Prof -Optional:$Optional
        if ($null -eq $Creds) { exit 1 }
        foreach ($Key in $Creds.Keys) {
            if (-not $Collected.ContainsKey($Key)) {
                $Collected[$Key] = $Creds[$Key]
            }
        }
    }
    return $Collected
}

# --- Helper: turn a local feature line in devcontainer.json on or off ---
# Features are resolved and installed at IMAGE BUILD, and devcontainer.env is an
# --env-file (run time only), so nothing in it can gate an install. Editing
# devcontainer.json on the host before the build is the only lever there is - which
# is why this is reached from -Spawning, before the container exists.
#
# Commented rather than deleted so the line stays discoverable: turning a feature back
# on is uncomment + rebuild. Comments are legal in devcontainer.json (the CLI and
# VS Code both parse it as JSONC). Line-based rather than a JSON parse for the same
# reason: ConvertFrom-Json would choke on the comments that are already in the file.
#
# The commas do NOT take care of themselves: commenting out the LAST entry of the
# features block leaves the one above it with a trailing comma. No toggleable feature
# happens to be last today, so nothing shows it - reorder the block and it breaks.
# Repair-FeatureComma below is called after a batch of toggles so the result never
# depends on that accident.
#
# Written with WriteAllText and "`n", NOT Set-Content: under PowerShell 5.1 that
# cmdlet writes CRLF, and the repo checks this file out with LF (.gitattributes).
function Set-FeatureState {
    param([string]$Name, [bool]$On)

    if (-not (Test-Path $script:DcJson)) { return }

    $Out = @()
    foreach ($Line in (Get-Content $script:DcJson)) {
        if ($On) {
            if ($Line -match "^(\s*)//\s*(`"\./features/$Name`".*)$") {
                $Line = "$($Matches[1])$($Matches[2])"
            }
        } else {
            if ($Line -match "^(\s*)(`"\./features/$Name`".*)$") {
                $Line = "$($Matches[1])// $($Matches[2])"
            }
        }
        $Out += $Line
    }
    [System.IO.File]::WriteAllText($script:DcJson, (($Out -join "`n") + "`n"))
}

# --- Helper: fix up the commas in the features block ---
# Gives every direct child of "features" a trailing comma except the last, ignoring
# commented-out lines - i.e. repairs the trailing comma that commenting out the final
# entry leaves behind, and restores the one that uncommenting a new final entry needs.
#
# Depth counting rather than matching on "{}", because a direct child can span several
# lines (the common-utils feature has an options object). A line is a direct child's
# last line when the running depth returns to 1; depth 0 is the block's own closing
# brace and ends the scan. Commented lines are skipped as candidates but still counted -
# they are balanced, so they contribute nothing either way.
function Repair-FeatureComma {
    if (-not (Test-Path $script:DcJson)) { return }

    $Lines = @(Get-Content $script:DcJson)
    $Start = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^\s*"features"\s*:\s*\{\s*$') { $Start = $i; break }
    }
    if ($Start -lt 0) { return }

    $Depth = 1
    $Terminators = @()
    for ($i = $Start + 1; $i -lt $Lines.Count; $i++) {
        $Opens = ([regex]::Matches($Lines[$i], '\{')).Count
        $Closes = ([regex]::Matches($Lines[$i], '\}')).Count
        $Depth += $Opens - $Closes
        if ($Depth -eq 0) { break }
        if ($Depth -eq 1 -and $Lines[$i] -notmatch '^\s*//') { $Terminators += $i }
    }

    for ($k = 0; $k -lt $Terminators.Count; $k++) {
        $Idx = $Terminators[$k]
        $Lines[$Idx] = $Lines[$Idx] -replace ',\s*$', ''
        if ($k -lt ($Terminators.Count - 1)) { $Lines[$Idx] = $Lines[$Idx] + "," }
    }

    [System.IO.File]::WriteAllText($script:DcJson, (($Lines -join "`n") + "`n"))
}

# --- Profile-dependent features ---
# Only these are offered. The rest of the feature set (certs, shell, tmux, herdr, the
# ghcr.io features) is provider-independent and stays as devcontainer.json ships it -
# except .\features\_template_, which is not a profile question at all and is handled by
# Set-TemplateFeature below.
$FeatureList = @(
    @{ Name = "claude";   Desc = "Claude Code" }
    @{ Name = "codex";    Desc = "Codex CLI" }
    @{ Name = "opencode"; Desc = "OpenCode" }
    @{ Name = "pi";       Desc = "Pi Coding Agent" }
    @{ Name = "grok";     Desc = "Grok Build" }
    @{ Name = "bedrock";  Desc = "Bedrock account gates (retention, GovCloud access)" }
    @{ Name = "chat";     Desc = "Browser chat stack - adds minutes to the build" }
)

# Features setup decides on its own: still toggled in devcontainer.json, never offered.
# bedrock is not a tool anyone chooses - it is the AWS ACCOUNT gates (data retention,
# GovCloud model entitlement) that every agent reaching Bedrock needs set, so it follows the
# profile selection exactly: on when a selected profile has AWS_REGION, off otherwise.
# Offering it as a checkbox only made it possible to pick an AWS profile and then turn off
# the thing that makes its models answer at all, which fails much later and looks like a
# model problem. Its line is still printed, unnumbered, so the choice stays visible.
$FeatureAuto = @("bedrock")

# --- Helper: does this profile configure this feature? ---
# The profile fragment each feature's own postcreate.sh reads IS the support signal:
# with no fragment there is nothing to configure, so the feature would install and then
# find no work. bedrock is the one keyed on a variable instead of a file - it carries
# the AWS ACCOUNT gates (data retention, GovCloud model entitlement), so AWS_REGION in
# profile.env is what marks a profile as Bedrock.
function Test-FeatureSupported {
    param([string]$Feature, [string]$ProfileName)

    $Dir = Join-Path $ProfilesDir $ProfileName
    switch ($Feature) {
        "claude"   { return (Test-Path (Join-Path $Dir "claude.json")) }
        "codex"    { return (Test-Path (Join-Path $Dir "config.toml")) }
        "opencode" { return (Test-Path (Join-Path $Dir "opencode.json")) }
        "pi"       { return (Test-Path (Join-Path $Dir "models.json")) }
        "grok"     { return (Test-Path (Join-Path $Dir "grok.toml")) }
        "chat"     { return (Test-Path (Join-Path $Dir "litellm.json")) }
        "bedrock"  {
            $EnvFile = Join-Path $Dir "profile.env"
            if (-not (Test-Path $EnvFile)) { return $false }
            $Hit = Get-Content $EnvFile | Where-Object { $_ -match '^AWS_REGION=' }
            return [bool]$Hit
        }
    }
    return $false
}

# --- CUI data handling ---
# One project, one side of the fence. A .cui_approved marker file in a profile's own
# directory means that provider is approved for CUI data; the answer here DELETES every
# profile on the other side, so a public project can reach public providers only and a CUI
# project CUI-approved ones only. See choose_cui in setup.sh for the full reasoning: the
# deletion is the enforcement (profile directories are the menu, and a profile left in
# place can be enabled later with setup.ps1 <profile>), the surviving set is the only
# record of the answer, and the aws/awsgov collision on shared AWS_* names needs no special
# case because the partition already separates them.
#
# Under -Spawning the deletions ride in the initial commit (Initialize-GitRepo runs after
# this); on the initializeCommand path they are left as working-tree changes for the user to
# commit, because nothing here may touch an adopted project's history.
function Select-CuiHandling {
    $Cui = @()
    $Public = @()

    foreach ($Dir in @(Get-ChildItem -Path $ProfilesDir -Directory | Sort-Object Name)) {
        if (-not (Test-Path (Join-Path $Dir.FullName "profile.env"))) { continue }
        # Test-Path, never Get-ChildItem, to find the marker: on non-Windows PowerShell a
        # leading dot marks an item hidden and Get-ChildItem omits hidden entries without
        # -Force, so listing the directory would report every profile as public and the
        # question would silently stop pruning. Test-Path sees it either way.
        if (Test-Path (Join-Path $Dir.FullName ".cui_approved")) {
            $Cui += $Dir.Name
        } else {
            $Public += $Dir.Name
        }
    }

    # Nothing to decide unless both sides are represented - which is also how a later run
    # knows the question has been answered already.
    if ($Cui.Count -eq 0 -or $Public.Count -eq 0) {
        return
    }

    # The maintainer bypass, and the ONE exception to the partition. A checkout of agent-dev
    # itself has to keep every profile: that container exists to develop and test all of
    # them, and the enforcement here is a deletion in the working tree, so a first create in
    # a fresh clone would otherwise ask a maintainer to permanently discard three quarters of
    # what they are maintaining. The marker is tracked in agent-dev's root and deleted by
    # spawn's clean slate, so a spawned or adopted project never takes this branch.
    #
    # It skips the question rather than answering it: no profile is deleted and none is
    # deemed approved either. poststart.sh reads the same markers and reports any unmarked
    # profile as public, so the resulting mixed container says so on every start.
    if (Test-Path $TemplateMarker) {
        Write-Host ""
        Write-Host "  CUI Data Handling - QUESTION SKIPPED"
        Write-Host ""
        Write-Host "  This is a checkout of the agent-dev TEMPLATE repository"
        Write-Host "  (.template-container is present), so every profile was left in place - a"
        Write-Host "  maintainer needs all of them. No provider was approved as a result:"
        Write-Host "  treat this container as PUBLIC and keep CUI data out of it."
        Write-Host ""
        Write-Host "  If you are not maintaining agent-dev, spawn a project instead - that asks"
        Write-Host "  the question properly and enforces the answer:"
        Write-Host ""
        Write-Host "    $ScriptDir\spawn.ps1 <path>"
        return
    }

    # The POSIX side's failed-read guard, and it is needed here for a DIFFERENT reason.
    # bash reads the answer from /dev/tty, so redirection cannot reach it and only an
    # absent terminal fails - loudly, with a non-zero `read`. Read-Host reads stdin and
    # returns "" at EOF, which '^[Yy]$' reads as "no", so an unattended run would delete
    # every CUI-approved profile without ever asking. The call sites probe too; this guard
    # is in the function because it is the one place where guessing is destructive.
    if (-not (Test-CanPrompt)) {
        Write-Host ""
        Write-Warning "  No console to ask the CUI data handling question on -"
        Write-Warning "  every profile was left in place. Run setup.ps1 from a terminal."
        return
    }

    Write-Host ""
    Write-Host "  CUI Data Handling"
    Write-Host ""
    Write-Host "  This belongs to the project, not to a single request, so the profiles on the"
    Write-Host "  other side of the answer are DELETED - no agent here can reach them afterwards,"
    Write-Host "  and changing your mind means spawning the project again."
    Write-Host ""
    Write-Host "    CUI-approved:  $($Cui -join ', ')"
    Write-Host "    Public:        $($Public -join ', ')"
    Write-Host ""
    $CuiAnswer = Read-Host "Will this project handle CUI data? [y/N]"
    if ($CuiAnswer -match '^[Yy]$') {
        $Keep = $Cui
        $Drop = $Public
    } else {
        $Keep = $Public
        $Drop = $Cui
    }

    foreach ($Name in $Drop) {
        # -Force is not optional here, and not only for the usual hidden-dotfile reason:
        # the directory holds the hidden .cui_approved marker, which Remove-Item would
        # otherwise refuse to delete, taking the parent with it.
        Remove-Item (Join-Path $ProfilesDir $Name) -Recurse -Force
    }

    Write-Host ""
    Write-Host "  Keeping: $($Keep -join ', ')"
    Write-Host "  Removed: $($Drop -join ', ')"
}

# --- Profile selection --- (sets $script:Selected)
function Select-ProfileList {
    Write-Host ""
    Write-Host "  Select Profiles"
    Write-Host ""

    $Available = @(Get-ChildItem -Path $ProfilesDir -Directory | Where-Object {
        Test-Path (Join-Path $_.FullName "profile.env")
    } | ForEach-Object { $_.Name } | Sort-Object)

    if ($Available.Count -eq 0) {
        Write-Host "ERROR: No profiles found in $ProfilesDir"
        exit 1
    }

    for ($i = 0; $i -lt $Available.Count; $i++) {
        Write-Host "  $($i + 1). $($Available[$i])"
    }
    Write-Host ""

    $script:Selected = @()
    while ($script:Selected.Count -eq 0) {
        $Selection = Read-Host "Enter profile numbers (comma or space separated)"

        $Picks = $Selection -split '[,\s]+' | Where-Object { $_ -ne '' }
        $Valid = $true
        $TempSelected = @()
        foreach ($Pick in $Picks) {
            $Index = 0
            if ([int]::TryParse($Pick, [ref]$Index) -and $Index -ge 1 -and $Index -le $Available.Count) {
                $TempSelected += $Available[$Index - 1]
            } else {
                Write-Host "Invalid selection: $Pick"
                $Valid = $false
                break
            }
        }

        if ($Valid -and $TempSelected.Count -gt 0) {
            $script:Selected = $TempSelected
        } else {
            Write-Host "Please select at least one profile."
        }
    }
}

# --- Feature selection --- (needs $Selected; edits devcontainer.json)
# Defaults come from the profiles: a feature no selected profile configures starts off,
# everything else starts on. chat is the exception - every profile ships a litellm.json,
# so it is always supported and its default is the -Chat switch, because what makes it
# opt-in is build time, not provider support.
#
# The defaults are the product, not a starting point: the list is printed, then a single
# [y/N] decides whether to touch it at all. Accepting is one keystroke and the common case,
# because the profile selection has already determined which features have anything to do.
# The flip loop is only entered on an explicit yes - it used to be the only way past this
# screen, which read as a decision the user was required to make.
#
# $FeatureAuto entries are printed without a number and cannot be flipped; see there.
function Select-FeatureList {
    $States = @()
    $Notes = @()
    $PromptMap = @()
    $Customize = $false

    foreach ($Feature in $FeatureList) {
        $Supporting = @()
        $Missing = @()
        foreach ($Prof in $script:Selected) {
            if (Test-FeatureSupported -Feature $Feature.Name -ProfileName $Prof) {
                $Supporting += $Prof
            } else {
                $Missing += $Prof
            }
        }

        $Note = ""
        if ($Supporting.Count -eq 0) {
            $State = $false
            $Note = "no config for $($script:Selected -join ' ')"
        } elseif ($Feature.Name -eq "chat" -and -not $Chat) {
            $State = $false
        } else {
            $State = $true
            if ($Missing.Count -gt 0) {
                $Note = "no config for $($Missing -join ' ')"
            }
        }
        $States += $State
        $Notes += $Note
        if ($FeatureAuto -contains $Feature.Name) {
            $Notes[$Notes.Count - 1] = "automatic - follows the profiles"
        } else {
            $PromptMap += ($States.Count - 1)
        }
    }

    Write-Host ""
    Write-Host "  Features"
    Write-Host ""
    Write-Host "  [x] gets installed. Defaults follow the profile(s) you picked."
    Write-Host ""

    while ($true) {
        $Num = 0
        for ($i = 0; $i -lt $FeatureList.Count; $i++) {
            $Mark = " "
            if ($States[$i]) { $Mark = "x" }
            $Note = ""
            if ($Notes[$i] -ne "") { $Note = "  ($($Notes[$i]))" }
            if ($FeatureAuto -contains $FeatureList[$i].Name) {
                Write-Host ("     [{0}] {1,-9} {2}{3}" -f $Mark, $FeatureList[$i].Name, $FeatureList[$i].Desc, $Note)
            } else {
                $Num++
                Write-Host ("  {0}. [{1}] {2,-9} {3}{4}" -f $Num, $Mark, $FeatureList[$i].Name, $FeatureList[$i].Desc, $Note)
            }
        }
        Write-Host ""

        # First pass asks whether to touch the list at all; anything but yes accepts the
        # defaults - including the "" that Read-Host returns at EOF, which is the right
        # answer for a run with no input to give (the call site probes first regardless).
        if (-not $Customize) {
            $Answer = Read-Host "Customize the feature set? [y/N]"
            if ($Answer -notmatch '^[Yy]$') { break }
            $Customize = $true
            Write-Host ""
        }

        $Answer = Read-Host "Numbers to flip (comma or space separated, Enter to accept)"
        if ([string]::IsNullOrWhiteSpace($Answer)) { break }

        $Picks = $Answer -split '[,\s]+' | Where-Object { $_ -ne '' }
        $Valid = $true
        foreach ($Pick in $Picks) {
            $Index = 0
            if (-not ([int]::TryParse($Pick, [ref]$Index)) -or $Index -lt 1 -or $Index -gt $PromptMap.Count) {
                Write-Host "Invalid selection: $Pick"
                $Valid = $false
                break
            }
        }
        Write-Host ""
        if (-not $Valid) { continue }

        # The numbers the user sees index $PromptMap, not $FeatureList - the automatic
        # entries are printed in place but take no number.
        foreach ($Pick in $Picks) {
            $Index = $PromptMap[[int]$Pick - 1]
            $States[$Index] = -not $States[$Index]
        }
    }

    for ($i = 0; $i -lt $FeatureList.Count; $i++) {
        Set-FeatureState -Name $FeatureList[$i].Name -On $States[$i]
    }
    Repair-FeatureComma
    Write-Host "Updated: .devcontainer/devcontainer.json"
}

# --- Maintainer-only tooling ---
# .\features\_template_ installs nothing; it is a handle for what agent-dev's OWN container
# needs and a project built from it does not, pulled in through dependsOn (today PowerShell,
# so a maintainer can run setup.ps1/spawn.ps1 in the container - every real invocation of
# those is on the host, so a spawned project has no use for pwsh and should not pay for the
# layer).
#
# Not a question, and not in $FeatureList: it has no profile dimension, so
# Test-FeatureSupported would have nothing to say about it, and the answer is already written
# down. The gate is the same root .template-container marker Select-CuiHandling reads -
# "is this the template repo?" has one answer and lives in one file. spawn's clean slate
# deletes the marker in staging before setup runs, so a spawned project switches this off
# without being asked, and an adopted one (which only ever receives .devcontainer\) does too.
#
# Called from -Spawning only, in both of its branches, and deliberately NOT from -Ensure:
# features are installed at image build, so an edit on the create path could only affect the
# next build, and in an adopted project it would dirty the working tree for no gain.
function Set-TemplateFeature {
    Set-FeatureState -Name "_template_" -On ([bool](Test-Path $TemplateMarker))
    Repair-FeatureComma
}

# --- Helper: is a local feature switched on in devcontainer.json? ---
# Reads the file rather than a flag, so it answers what the project will actually build:
# -Chat only seeds the default that Select-FeatureList then lets the user flip. An
# uncommented entry is the on state, and a commented one cannot match - the toggle prefixes
# "// ", so the quote no longer follows the indent.
function Test-FeatureEnabled {
    param([string]$Name)

    if (-not (Test-Path $script:DcJson)) { return $false }
    foreach ($Line in (Get-Content $script:DcJson)) {
        if ($Line -match "^\s*`"\./features/$Name`"") { return $true }
    }
    return $false
}

# --- Browser chat stack autostart --- (sets $script:Autostart; reads devcontainer.json)
# CHAT_AUTOSTART is the one runtime answer this script collects. It is read by
# features/chat/poststart.sh out of the container environment on EVERY start, so unlike the
# feature set it is not frozen at image build and a project can change its mind by editing
# devcontainer.env - which is also why asking it is worth doing on the adopted path too,
# where the feature prompt is deliberately skipped.
#
# Only asked when the chat feature is actually on: with the stack absent the variable would
# decide nothing, and a chat-less project's env file should not carry a knob for services it
# does not have.
#
# Defaults to on ([Y/n]) because reaching this prompt means the user just chose to build the
# stack; the question is only whether they want to pay the start-up wait every time or bring
# it up with start_chat_stack when they need it. That default is also what a missing console
# gets - no prompt, no warning, since a container that starts its own services is the
# unsurprising outcome of asking for them.
#
# $Autostart stays EMPTY when the feature is off, which is what Write-DevEnvFile keys off to
# omit the line entirely.
#
# A "no" answer echoes the command back, because that reply is the only point where the user
# has committed to starting the stack by hand: the pre-prompt text names the command while
# they are still deciding, and this is the line they can act on afterwards. The feature's
# postcreate.sh symlinks start.sh to ~/.local/bin/start_chat_stack, so it really is on PATH
# from the first create on.
function Select-ChatAutostart {
    $script:Autostart = ""
    if (-not (Test-FeatureEnabled -Name "chat")) { return }

    $script:Autostart = "1"
    if (-not (Test-CanPrompt)) { return }

    Write-Host ""
    Write-Host "  The chat stack is four services under supervisord, and a container start"
    Write-Host "  waits for them to answer before it finishes. Answer no to bring them up on"
    Write-Host "  demand instead, with 'start_chat_stack' inside the container."
    Write-Host ""
    $Answer = Read-Host "Start the browser chat stack automatically on container start? [Y/n]"
    if ($Answer -match '^[Nn]') {
        $script:Autostart = "0"
        Write-Host ""
        Write-Host "  Not started automatically. Bring the stack up from a terminal inside"
        Write-Host "  the container with 'start_chat_stack' (on PATH after the first"
        Write-Host "  create) - once per container start is enough, it keeps running."
    }
}

# --- Generate devcontainer.env --- (needs $Selected, $Credentials and $Autostart)
# Every var a selected profile declares is written; the ones whose value is empty in
# profile.env are filled from the collected credentials, and left EMPTY when nothing was
# collected (the -Spawning case, where the keys do not exist yet).
function Write-DevEnvFile {
    $Output = @("CONFIGURED_PROFILES=$($script:Selected -join ',')")

    # Container behaviour rather than a credential, so it goes above the profile blocks.
    # Written only when Select-ChatAutostart had something to decide (chat feature on); the
    # emptiness test rather than a call here, so this does not depend on every future caller
    # having run that prompt first.
    if (-not [string]::IsNullOrEmpty($script:Autostart)) {
        $Output += "CHAT_AUTOSTART=$($script:Autostart)"
    }

    foreach ($Prof in $script:Selected) {
        $EnvFile = Join-Path $ProfilesDir "$Prof\profile.env"
        foreach ($Line in (Get-Content $EnvFile)) {
            if ($Line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($Line)) { continue }

            if ($Line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
                $VarName = $Matches[1]
                $VarValue = $Matches[2]
                if ([string]::IsNullOrEmpty($VarValue) -and $script:Credentials.ContainsKey($VarName)) {
                    $Line = "$VarName=$($script:Credentials[$VarName])"
                }
            }

            $Output += $Line
        }
    }

    Write-EnvFile -Path $DevEnv -Lines $Output
    Write-Host ""
    Write-Host "Created: .devcontainer/devcontainer.env"
}

# --- Git repository --- (spawn mode only)
# ALL git for this project happens here, on the host, before the container is ever built.
# Nothing in the container commits anything: the two things that used to dirty the tree on
# a first create are both settled by now (the CUI profile deletion happened above, and the
# Claude Code feature writes its defaults to ~/.claude instead of the project), so the
# initial commit is the finished state and a create leaves the tree alone. That also
# retires the identity workaround the in-container commit needed - VS Code copies the host
# ~/.gitconfig in only on window attach, AFTER postCreateCommand, so git there has no
# identity to commit with. Here it does.
#
# Called last in spawn mode so every edit rides in the initial commit.
function Initialize-GitRepo {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { return }

    $Want = [bool]$Git

    # An ancestor directory is already a repo (spawn refuses an existing target, so this
    # means -Spawning was run by hand inside a checkout). Someone else's history: no init,
    # no commit, no staging.
    git -C $ProjectDir rev-parse --git-dir 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "  Already inside a Git repository - leaving it alone."
        return
    }

    if (Test-CanPrompt) {
        Write-Host ""
        Write-Host "  Git"
        Write-Host ""
        if ($Want) {
            $Answer = Read-Host "  Initialize a Git repository? [Y/n]"
            if ($Answer -match '^[Nn]$') { $Want = $false }
        } else {
            $Answer = Read-Host "  Initialize a Git repository? [y/N]"
            if ($Answer -match '^[Yy]$') { $Want = $true }
        }
    }
    if (-not $Want) { return }

    # -b needs git 2.28+; on anything older fall back to a plain init and let the host's
    # own init.defaultBranch decide.
    git -C $ProjectDir init -q -b main 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        git -C $ProjectDir init -q 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return }
    }

    # `git add -A` sweeps the whole tree, so devcontainer.env being ignored is what keeps
    # credentials out of the commit - and since the credentials were collected a moment
    # ago, that file holds REAL keys by the time we get here. Verify the rule instead of
    # assuming it: if it is missing, stop before anything is staged.
    git -C $ProjectDir check-ignore -q ".devcontainer/devcontainer.env" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Warning "  .devcontainer\devcontainer.env is not ignored by Git."
        Write-Warning "  Repository initialized, but nothing staged or committed - restore that"
        Write-Warning "  rule in .devcontainer\.gitignore before committing, or credentials will"
        Write-Warning "  land in Git history."
        return
    }

    # Identity, repo-local only: this is a brand-new repository, so setting it here
    # configures the project the user just created without touching their global config or
    # any other repo.
    $GitName = (git config --global user.name 2>$null)
    $GitEmail = (git config --global user.email 2>$null)
    if ([string]::IsNullOrWhiteSpace($GitName) -or [string]::IsNullOrWhiteSpace($GitEmail)) {
        if (-not (Test-CanPrompt)) {
            git -C $ProjectDir add -A
            Write-Host ""
            Write-Warning "  No global Git user.name/user.email, and no console to ask on."
            Write-Warning "  Repository initialized and everything staged, but not committed."
            Write-Warning "  Set your identity and commit:"
            Write-Warning "    git config user.name ""Your Name"""
            Write-Warning "    git config user.email ""you@example.com"""
            Write-Warning "    git commit -m ""Initial commit"""
            return
        }
        Write-Host ""
        Write-Host "  No global Git identity - setting one for this repository only."
        Write-Host ""
        while ([string]::IsNullOrWhiteSpace($GitName)) {
            $GitName = Read-Host "  Your name"
        }
        while ([string]::IsNullOrWhiteSpace($GitEmail)) {
            $GitEmail = Read-Host "  Your email"
        }
        git -C $ProjectDir config user.name $GitName
        git -C $ProjectDir config user.email $GitEmail
    }

    git -C $ProjectDir add -A
    git -C $ProjectDir commit -q -m "Initial commit"
    Write-Host ""
    Write-Host "  Git repository initialized with an initial commit."
}

# --- Helper: configured profiles whose credentials are still blank ---
# A key is blank when the user skipped it at the spawn-time prompt. Reads the generated
# devcontainer.env rather than anything collected in memory, so it is equally valid right
# after a write (spawn's closing summary) and on a later run (the update-mode handoff
# below). A profile is pending when profile.env declares a var with no value - that is
# what marks a credential - and the env file still has none.
function Get-PendingProfile {
    if (-not (Test-Path $DevEnv)) { return @() }

    $EnvLines = Get-Content $DevEnv
    $ProfilesLine = $EnvLines | Where-Object { $_ -match '^CONFIGURED_PROFILES=' } | Select-Object -First 1
    if (-not $ProfilesLine) { return @() }

    $Configured = @(($ProfilesLine -replace '^CONFIGURED_PROFILES=', '') -split ',' | Where-Object { $_ -ne '' })
    $Pending = @()
    foreach ($Prof in $Configured) {
        $EnvFile = Join-Path $ProfilesDir "$Prof\profile.env"
        if (-not (Test-Path $EnvFile)) { continue }
        foreach ($Line in (Get-Content $EnvFile)) {
            if ($Line -match '^([A-Za-z_][A-Za-z0-9_]*)=$') {
                $VarName = $Matches[1]
                if ($EnvLines | Where-Object { $_ -match "^$VarName=\s*$" }) {
                    $Pending += $Prof
                    break
                }
            }
        }
    }
    return $Pending
}

# =====================================================================
# initializeCommand fast path: devcontainer.env exists, so there is nothing to ask.
# -Spawning now collects the credentials too, so the env file existing means every
# question this project asks has been answered - including a key deliberately left blank,
# which is the user's to fill in on the host with `setup.ps1 <profile>` and not something
# to re-prompt on every container start. Exit before everything below so the create stays
# silent and, above all, non-interactive: initializeCommand has no console to Read-Host
# on, and that throws rather than blocking.
#
# The one case that still has work to do is a project that ADOPTED .devcontainer\ instead
# of being spawned - no env file, so it falls through to the first-run flow.
# =====================================================================
if ($Ensure -and (Test-Path $DevEnv)) { exit 0 }

# --- Finish a credential the spawn-time prompt left blank ---
# Handled by assigning $Profiles rather than by re-invoking the script: update mode is
# right below and does exactly this job, so the pending profiles are simply fed into it.
# Only reached by a hand-run setup.ps1 - -Ensure exited above, so nothing here can
# interrupt a build.
if ($Profiles.Count -eq 0 -and -not $Spawning) {
    $PendingProfiles = Get-PendingProfile
    if ($PendingProfiles.Count -gt 0 -and -not (Test-CanPrompt)) {
        # Warn rather than prompt when there is nothing to prompt on: with no console
        # Read-Host throws a HostException - a wall of errors and a non-zero exit over
        # credentials the user can fill in afterwards - and with stdin redirected it
        # returns "" at EOF, which Get-ProfileCredentials rejects as a required value.
        Write-Host ""
        Write-Warning "Credentials are still blank in .devcontainer\devcontainer.env"
        Write-Host "  for: $($PendingProfiles -join ' ')"
        Write-Host "  No console is available to prompt on. Fill them in by running, on"
        Write-Host "  the host, one of:"
        foreach ($Prof in $PendingProfiles) {
            Write-Host "    $ScriptDir\setup.ps1 $Prof"
        }
        Write-Host ""
        $PendingProfiles = @()
    }
    if ($PendingProfiles.Count -gt 0) {
        Show-Banner
        Write-Host ""
        Write-Host "  Configure Credentials"
        $Profiles = $PendingProfiles
    }
}

# =====================================================================
# Update mode: .\setup.ps1 <profile> [profile...]
# Updates credentials for existing profiles, or adds new profiles.
# =====================================================================
if ($Profiles.Count -gt 0) {
    if (-not (Test-Path $DevEnv)) {
        Write-Host "ERROR: $DevEnv not found. Run setup.ps1 with no arguments first."
        exit 1
    }

    # Determine which profiles are new (not yet in CONFIGURED_PROFILES)
    $ProfilesLine = Get-Content $DevEnv | Where-Object { $_ -match '^CONFIGURED_PROFILES=' } | Select-Object -First 1
    $ExistingCsv = $ProfilesLine -replace '^CONFIGURED_PROFILES=', ''
    $Existing = @($ExistingCsv -split ',' | Where-Object { $_ -ne '' })

    $NewProfiles = @()
    foreach ($Prof in $Profiles) {
        if ($Prof -notin $Existing) {
            $NewProfiles += $Prof
        }
    }

    # Collect new credential values from each specified profile
    $Updates = @{}
    foreach ($Prof in $Profiles) {
        $Creds = Get-ProfileCredentials -ProfileName $Prof
        if ($null -eq $Creds) { exit 1 }
        foreach ($Key in $Creds.Keys) {
            $Updates[$Key] = $Creds[$Key]
        }
    }

    # Patch the values into devcontainer.env
    $Content = Get-Content $DevEnv
    $NewContent = @()
    $Patched = @{}
    foreach ($Line in $Content) {
        if ($Line -match '^([A-Za-z_][A-Za-z0-9_]*)=' -and $Updates.ContainsKey($Matches[1])) {
            $VarName = $Matches[1]
            $NewContent += "$VarName=$($Updates[$VarName])"
            $Patched[$VarName] = $true
        } else {
            $NewContent += $Line
        }
    }
    # Append any credential vars not already in the file
    foreach ($Key in $Updates.Keys) {
        if (-not $Patched.ContainsKey($Key)) {
            $NewContent += "$Key=$($Updates[$Key])"
        }
    }

    # For new profiles, append their non-credential env vars (ones with values in profile.env)
    foreach ($Prof in $NewProfiles) {
        $EnvFile = Join-Path $ProfilesDir "$Prof\profile.env"
        foreach ($Line in (Get-Content $EnvFile)) {
            if ($Line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($Line)) { continue }
            if ($Line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.+)$') {
                $VarName = $Matches[1]
                if (-not ($NewContent | Where-Object { $_ -match "^$VarName=" })) {
                    $NewContent += $Line
                }
            }
        }
    }

    # Update CONFIGURED_PROFILES to include new profiles
    if ($NewProfiles.Count -gt 0) {
        $UpdatedCsv = ($Existing + $NewProfiles) -join ','
        $NewContent = $NewContent | ForEach-Object {
            if ($_ -match '^CONFIGURED_PROFILES=') {
                "CONFIGURED_PROFILES=$UpdatedCsv"
            } else {
                $_
            }
        }
    }

    Write-EnvFile -Path $DevEnv -Lines $NewContent

    Write-Host ""
    if ($NewProfiles.Count -gt 0) {
        Write-Host "Added profile(s): $($NewProfiles -join ', ')"
    }
    Write-Host "Updated: .devcontainer/devcontainer.env"
    Write-Host ""

    # Say how to make the new values take effect - writing the file does nothing by
    # itself. devcontainer.env is a Docker --env-file in runArgs, so it is read ONCE, at
    # container CREATE: reopening or restarting reuses the environment Docker captured
    # then, and a rebuild is the only thing that re-reads the file.
    #
    # No counterpart to setup.sh's `set -a; . devcontainer.env; set +a` hint here: this
    # script runs on the HOST, where nothing reads those variables out of the shell. The
    # POSIX one-liner is only worth printing in the container, where an agent started from
    # that shell would inherit the new value without a rebuild.
    Write-Host "Docker reads this file when the container is created, so rebuild the container"
    Write-Host "to pick the new values up - reopening or restarting reuses the environment"
    Write-Host "captured at the last create:"
    Write-Host ""
    Write-Host "  F1 -> ""Dev Containers: Rebuild Container"""
    # A new profile needs more than its environment variables: postcreate.sh regenerates
    # every agent's config (and LiteLLM's) from CONFIGURED_PROFILES on create, and only a
    # rebuild runs that.
    if ($NewProfiles.Count -gt 0) {
        Write-Host ""
        Write-Host "A rebuild is REQUIRED for the profile(s) just added: each agent's config is"
        Write-Host "generated from CONFIGURED_PROFILES on create, not read from the environment."
    }
    Write-Host ""
    exit 0
}

# =====================================================================
# Spawn mode: .\setup.ps1 -Spawning [-Chat]
# Invoked by spawn.ps1 on the HOST, in the freshly extracted project, BEFORE the
# container exists - which is the whole point. The profile questions have always been
# asked from initializeCommand, and that is too late for anything that has to be true at
# image build: by then the CLI has already parsed devcontainer.json, so a feature it
# lists is fetched and installed no matter what the answers turn out to be
# (.\features\bedrock even drags in the aws-cli feature via dependsOn). Asking at spawn
# instead means the very first build installs only what the chosen profiles can
# actually configure.
#
# Credentials are collected here too, which is what makes the container create fully
# non-interactive: devcontainer.env is gitignored, so a real key in it is never at risk of
# being committed (Initialize-GitRepo asserts that rule below before it stages anything),
# and a host console is a far better place to paste one than initializeCommand's, which
# has none. Any key the user has not generated yet can be skipped with an empty answer and
# added later with `setup.ps1 <profile>`.
# =====================================================================
if ($Spawning) {
    # Safety net: spawn always extracts into a new directory, so there should be no env
    # file. If one exists, the project is already configured - leave it alone.
    if (Test-Path $DevEnv) {
        Write-Host ""
        Write-Host "  .devcontainer\devcontainer.env already exists - nothing to do."
        Write-Host ""
        exit 0
    }

    # Before the console check, because it asks nothing: the marker decides, and the answer
    # is the same whether or not anyone is watching.
    Set-TemplateFeature

    # No console to prompt on (spawn run from a scheduled task or service). Apply the
    # -Chat default and stop: with no env file written, the questions are still open.
    # Point at the host rather than at container create - the create has no console to ask
    # on either (-Ensure's adopted-project path says the same thing if it gets there
    # first). -Git still applies: Initialize-GitRepo prompts only when it can, and does
    # nothing unless the switch was passed.
    if (-not (Test-CanPrompt)) {
        if (-not $Chat) {
            Set-FeatureState -Name "chat" -On $false
            Repair-FeatureComma
        }
        Write-Host ""
        Write-Host "  No console available - profile, feature and credential selection"
        Write-Host "  skipped. Before opening the project, run:"
        Write-Host ""
        Write-Host "    $ScriptDir\setup.ps1"
        Initialize-GitRepo
        Write-Host ""
        exit 0
    }

    Select-CuiHandling
    Select-ProfileList
    Select-FeatureList
    # After the feature selection, because it is only asked when chat survived it - and
    # before the credentials so the two prompts that write devcontainer.env are adjacent.
    Select-ChatAutostart
    $Credentials = Read-AllCredentials -Optional

    Write-DevEnvFile
    # Last, so the initial commit records the finished state: pruned profiles, the feature
    # set this project ended up with, and nothing else pending.
    Initialize-GitRepo

    Write-Host ""
    Write-Host "  Profiles: $($Selected -join ', ')"
    # Report the skipped keys, with the command that fills them in. Read back out of the
    # env file that was just written rather than tracked through the prompts, so this says
    # what the file actually contains.
    $PendingProfiles = Get-PendingProfile
    if ($PendingProfiles.Count -gt 0) {
        Write-Host ""
        Write-Host "  Credentials left blank for: $($PendingProfiles -join ', ')"
        Write-Host "  Add them on the host, before or after the first build, with:"
        Write-Host ""
        foreach ($Prof in $PendingProfiles) {
            Write-Host "    .devcontainer\scripts\setup.ps1 $Prof"
        }
    }
    Write-Host ""
    exit 0
}

# =====================================================================
# Full setup mode: .\setup.ps1 (no arguments)
# =====================================================================

$Selected = @()
$Autostart = ""

# --- Already configured: show help and exit ---
if (Test-Path $DevEnv) {
    $ProfilesLine = Get-Content $DevEnv | Where-Object { $_ -match '^CONFIGURED_PROFILES=' } | Select-Object -First 1
    if ($ProfilesLine) {
        $Csv = $ProfilesLine -replace '^CONFIGURED_PROFILES=', ''
        $Selected = @($Csv -split ',' | Where-Object { $_ -ne '' })
        # Validate all configured profiles still exist
        foreach ($Prof in $Selected) {
            if (-not (Test-Path (Join-Path $ProfilesDir "$Prof\profile.env"))) {
                $Selected = @()
                break
            }
        }
        if ($Selected.Count -gt 0) {
            # Nothing left to do - anything still blank was already collected above. No
            # -Ensure check is needed here: that path exited at the fast path above, so
            # this summary only ever reaches someone who ran setup.ps1 by hand and wants
            # to know where things stand.

            # Find unconfigured profiles
            $Unconfigured = @(Get-ChildItem -Path $ProfilesDir -Directory | Where-Object {
                (Test-Path (Join-Path $_.FullName "profile.env")) -and ($_.Name -notin $Selected)
            } | ForEach-Object { $_.Name } | Sort-Object)

            Write-Host ""
            Write-Host "  Profiles already configured: $($Selected -join ', ')"
            Write-Host ""
            Write-Host "  To update credentials for a profile:"
            Write-Host ""
            foreach ($Prof in $Selected) {
                Write-Host "    $PSCommandPath $Prof"
            }
            if ($Unconfigured.Count -gt 0) {
                Write-Host ""
                Write-Host "  To add an unconfigured profile:"
                Write-Host ""
                foreach ($Prof in $Unconfigured) {
                    Write-Host "    $PSCommandPath $Prof"
                }
            }
            Write-Host ""
            exit 0
        }
    }
}

# --- First-run setup (CUI + profile selection + credentials) ---
# This is the ADOPTED-project path: .devcontainer\ was dropped into an existing project,
# so no spawn ran and there is no env file. A spawned project never gets here - every
# question was answered on the host and -Ensure exited at the fast path above.
#
# Feature selection is deliberately NOT repeated here. It edits devcontainer.json, which
# the CLI parsed before initializeCommand ran, so an edit now would only take effect on
# the NEXT build - the opposite of useful on a first create.
if ($Selected.Count -eq 0) {
    # No console to ask on, and unlike every other path this one has real questions left.
    # Bail out with a warning rather than prompting: with no console Read-Host either
    # throws or (stdin redirected) returns "" at EOF, and Select-CuiHandling would
    # then be left deciding the CUI question with no answer - which permanently deletes
    # every profile on one side of it. Exit 0 so the build still finishes; the user runs
    # setup.ps1 on the host and reopens.
    if (-not (Test-CanPrompt)) {
        Write-Host ""
        Write-Warning "No provider profiles are configured for this project, and there is"
        Write-Host "  no console to ask which ones to use on, so the container will build"
        Write-Host "  with no provider credentials at all. Run this on the host and reopen"
        Write-Host "  the project:"
        Write-Host ""
        Write-Host "    $ScriptDir\setup.ps1"
        Write-Host ""
        exit 0
    }
    Show-Banner
    Select-CuiHandling
    Select-ProfileList
}

$Credentials = Read-AllCredentials

# Whether the web services (LiteLLM proxy, Open WebUI, Open Terminal, SearXNG) are
# INSTALLED is not asked here: that is the feature selection, which belongs to spawn mode
# because it has to happen before the image build (`spawn -Chat` seeds its default; the
# ./features/chat line is commented out of devcontainer.json when it ends up off).
# Whether an installed stack STARTS ITSELF is a runtime flag, so it is fair game on this
# path - an adopted project may well have the feature and has had no chance to answer.
Select-ChatAutostart

# find-skills is no longer asked about here: it installs into $HOME (user scope) on
# every create in postcreate.sh, so it's part of the container rather than a
# per-project choice and needs no persisted flag.

# --- Generate devcontainer.env ---
Write-DevEnvFile

# NB: no git here, deliberately. This is the initializeCommand path, which runs for
# ADOPTED projects too - a repo this container did not create and whose history it must not
# write to. The CUI profile deletion is left as a working-tree change for the user to
# commit. Git runs in exactly one place, -Spawning's Initialize-GitRepo, where the repo is
# one we are creating from scratch.

Write-Host ""
