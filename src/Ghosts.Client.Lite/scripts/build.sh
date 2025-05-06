#!/bin/bash

# Navigate to the root directory of the project
cd ../

# Build the project for Windows
dotnet publish -c Release -r win-x64 --self-contained false

# Build the project for Linux
dotnet publish -c Release -r linux-x64 --self-contained false

# Navigate to the output directory
cd src/bin/Release/net8.0

# Clean up Windows build
rm win-x64/*pdb
rm win-x64/Ghosts.Client.Lite.deps.json
rm -rf win-x64/publish

# Clean up Linux build
rm linux-x64/*pdb
rm linux-x64/Ghosts.Client.Lite.deps.json
rm -rf linux-x64/publish

# Remove existing directories/zips if they exist
rm -rf ghosts-lite-win ghosts-lite-linux
rm ghosts-lite-win.zip ghosts-lite-linux.zip

# Rename and zip
mv win-x64 ghosts-lite-win
zip -r ghosts-lite-win.zip ghosts-lite-win

mv linux-x64 ghosts-lite-linux
zip -r ghosts-lite-linux.zip ghosts-lite-linux
