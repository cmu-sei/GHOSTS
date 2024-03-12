[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]
    $config
)

$configuration = "release" # release || debug

# release version is determined by the project file release version parameter
$r = ""
foreach($line in Get-Content "..\src\Ghosts.Client\Properties\AssemblyInfo.cs") {
    if($line.StartsWith("[assembly: AssemblyVersion(")){
        $r = $line.Replace("[assembly: AssemblyVersion(", "").Replace(")]", "").Replace("`"","")
        break
    }
}

$release_version = $r.split(".")[0..2] -join "."
Write-Host "Preparing to build and package $release_version"

$t = "..\src\Ghosts.Client\bin"
if (Test-Path $t -PathType Container) { 
    Write-Host "Removing $t"
    Remove-Item -Path $t -Recurse -Force -Confirm:$false
} 

$msbuild = "& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe'"
$args = " ..\src\ghosts.windows.sln /nologo /v:minimal /p:configuration=$configuration"
$build = "$msbuild $args"
Invoke-Expression $build

$g = (Invoke-Expression "& '..\src\Ghosts.Client\bin\$configuration\geckodriver.exe' --version").split("(")[0]
$c = (Invoke-Expression "& '..\src\Ghosts.Client\bin\$configuration\chromedriver.exe' --version").split("(")[0]

Write-Host "  x32 build completed. Preparing package..." -ForegroundColor Green
Write-Host "    $g" -ForegroundColor Green
Write-Host "    $c" -ForegroundColor Green
if (-not [string]::IsNullOrWhiteSpace($config)) {
    Write-Host "  Copying external config for ..\src\Ghosts.Client\bin\$configuration\config..." -ForegroundColor Green
    Remove-Item -Path "..\src\Ghosts.Client\bin\$configuration\config" -Recurse -Force -Confirm:$false
    Copy-Item -Path $config -Destination "..\src\Ghosts.Client\bin\$configuration\config" -Recurse -Force
}
Rename-Item -Path "..\src\Ghosts.Client\bin\$configuration" -NewName "ghosts-client-x32-v$release_version"
Compress-Archive -Path "..\src\Ghosts.Client\bin\ghosts-client-x32-v$release_version" -DestinationPath "..\src\Ghosts.Client\bin\ghosts-client-x32-v$release_version.zip"
Write-Host "  x32 package complete..." -ForegroundColor Green

$args = " ..\src\ghosts.windows.sln /nologo /v:minimal /p:configuration=$configuration /p:Platform=x64"
$build = "$msbuild $args"
Invoke-Expression $build
Write-Host "  x64 build completed. Preparing package..." -ForegroundColor Green
Write-Host "    $g" -ForegroundColor Green
Write-Host "    $c" -ForegroundColor Green
if (-not [string]::IsNullOrWhiteSpace($config)) {
    Write-Host "  Copying external config for ..\src\Ghosts.Client\bin\x64\$configuration\config..." -ForegroundColor Green
    Remove-Item -Path "..\src\Ghosts.Client\bin\x64\$configuration\config" -Recurse -Force -Confirm:$false
    Copy-Item -Path $config -Destination "..\src\Ghosts.Client\bin\x64\$configuration\config" -Recurse -Force
}
Rename-Item -Path "..\src\Ghosts.Client\bin\x64\$configuration" -NewName "ghosts-client-x64-v$release_version"
Compress-Archive -Path "..\src\Ghosts.Client\bin\x64\ghosts-client-x64-v$release_version" -DestinationPath "..\src\Ghosts.Client\bin\ghosts-client-x64-v$release_version.zip"

Write-Host "  x64 package complete..." -ForegroundColor Green


# clean up folder structure for x64
Move-Item -Path "..\src\Ghosts.Client\bin\x64\ghosts-client-x64-v$release_version" -Destination "..\src\Ghosts.Client\bin\ghosts-client-x64-v$release_version"
Remove-Item -Path "..\src\Ghosts.Client\bin\x64" -Recurse -Force -Confirm:$false

Write-Host "All packages completed." -ForegroundColor Green