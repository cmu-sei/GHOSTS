$current_version = "6.0.2"

$t = "..\src\Ghosts.Client\bin"
if (Test-Path $t -PathType Container) { 
    Write-Host "Removing $t"
    Remove-Item -Path $t -Recurse -Force -Confirm:$false
} 

$msbuild = "& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe'"
$args = " ..\src\ghosts.windows.sln /p:configuration=debug"
$build = "$msbuild $args"
Invoke-Expression $build
Write-Host "x32 build completed. Compressing..." -ForegroundColor Green
Rename-Item -Path "..\src\Ghosts.Client\bin\Debug" -NewName "ghosts-client-x32-v$current_version"
Compress-Archive -Path "..\src\Ghosts.Client\bin\ghosts-client-x32-v$current_version" -DestinationPath "..\src\Ghosts.Client\bin\ghosts-client-x32-v$current_version.zip"

$args = " ..\src\ghosts.windows.sln /p:configuration=debug /p:Platform=x64"
$build = "$msbuild $args"
Invoke-Expression $build
Write-Host "x64 build completed. Compressing..." -ForegroundColor Green
Rename-Item -Path "..\src\Ghosts.Client\bin\x64\Debug" -NewName "ghosts-client-x64-v$current_version"
Compress-Archive -Path "..\src\Ghosts.Client\bin\x64\ghosts-client-x64-v$current_version" -DestinationPath "..\src\Ghosts.Client\bin\ghosts-client-x64-v$current_version.zip"

Move-Item -Path "..\src\Ghosts.Client\bin\x64\ghosts-client-x64-v$current_version" -Destination "..\src\Ghosts.Client\bin\ghosts-client-x64-v$current_version"
Remove-Item -Path "..\src\Ghosts.Client\bin\x64" -Recurse -Force -Confirm:$false
Write-Host "All builds completed" -ForegroundColor Green
