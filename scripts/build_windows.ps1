[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]
    $config
)

$configuration = "release" # release || debug

# release version is determined by the project file release version parameter
$r = ""
foreach($line in Get-Content "..\src\ghosts.client.windows\Properties\AssemblyInfo.cs") {
    if($line.StartsWith("[assembly: AssemblyVersion(")){
        $r = $line.Replace("[assembly: AssemblyVersion(", "").Replace(")]", "").Replace("`"","")
        break
    }
}

$release_version = $r.split(".")[0..2] -join "."
Write-Host "Preparing to build and package $release_version"

$t = "..\src\ghosts.client.windows\bin"
if (Test-Path $t -PathType Container) { 
    Write-Host "Removing $t"
    Remove-Item -Path $t -Recurse -Force -Confirm:$false
} 

$possiblePaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\msbuild.exe"
)
$msbuildPath = $possiblePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msbuildPath) {
    Write-Error "Could not find msbuild.exe in standard locations."
    exit 1
}
$msbuild = "& '$msbuildPath'"

$platforms = @(
    @{ Name="x32"; PlatformParam=""; PathPrefix="" },
    @{ Name="x64"; PlatformParam="/p:Platform=x64"; PathPrefix="x64\" }
)

foreach ($p in $platforms) {
    $platformName = $p.Name
    $platformParam = $p.PlatformParam
    $pathPrefix = $p.PathPrefix
    
    $buildArgs = " ..\src\ghosts.client.windows.sln /t:Rebuild /restore /nologo /v:minimal /p:configuration=$configuration $platformParam"
    $build = "$msbuild $buildArgs"
    Invoke-Expression $build

    $binPath = "..\src\ghosts.client.windows\bin\$pathPrefix$configuration"
    
    $g = (Invoke-Expression "& '$binPath\geckodriver.exe' --version").split("(")[0]
    $c = (Invoke-Expression "& '$binPath\chromedriver.exe' --version").split("(")[0]

    Write-Host "  $platformName build completed. Preparing package..." -ForegroundColor Green
    Write-Host "    $g" -ForegroundColor Green
    Write-Host "    $c" -ForegroundColor Green

    if (-not [string]::IsNullOrWhiteSpace($config)) {
        Write-Host "  Copying external config for $binPath\config..." -ForegroundColor Green
        if (Test-Path "$binPath\config") {
            Remove-Item -Path "$binPath\config" -Recurse -Force -Confirm:$false
        }
        Copy-Item -Path $config -Destination "$binPath\config" -Recurse -Force
    }

    # Wait for file handles to be released
    Start-Sleep -Seconds 1

    $packageName = "ghosts-client-$platformName-v$release_version"
    Rename-Item -Path $binPath -NewName $packageName
    
    $currentPackagePath = "..\src\ghosts.client.windows\bin\$pathPrefix$packageName"
    Compress-Archive -Path $currentPackagePath -DestinationPath "..\src\ghosts.client.windows\bin\$packageName.zip"
    
    Write-Host "  $platformName package complete..." -ForegroundColor Green

    if ($pathPrefix -ne "") {
        Move-Item -Path $currentPackagePath -Destination "..\src\ghosts.client.windows\bin\$packageName"
        $prefixDir = $pathPrefix.Trim("\")
        Remove-Item -Path "..\src\ghosts.client.windows\bin\$prefixDir" -Recurse -Force -Confirm:$false
    }
}

Write-Host "All packages completed." -ForegroundColor Green