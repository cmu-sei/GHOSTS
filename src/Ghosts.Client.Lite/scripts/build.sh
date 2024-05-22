# Navigate to the root directory of the project
cd ../

# Build the project assuming the dotnet version is already installed
dotnet publish -c Release -r win-x64

# Navigate to the directory where ghosts-lite is located
cd src/bin/Release/net8.0

# Remove unnecessary files
rm win-x64/*pdb
rm win-x64/Ghosts.Client.Lite.deps.json
rm -rf win-x64/publish

# Remove the existing ghosts-lite directory and zip if they exist
rm -rf ghosts-lite
rm ghosts-lite.zip

# Move the win-x64 directory to ghosts-lite
mv win-x64 ghosts-lite

# Create the zip file from within the net8.0 directory
zip -r ghosts-lite.zip ghosts-lite