#!/bin/bash
set -euo pipefail

# Show git dirty status in zsh prompt
git config devcontainers-theme.show-dirty 1

# Navigate to src directory and restore/build the API
echo "========================================="
echo "Restoring and building GHOSTS API..."
echo "========================================="
cd src
dotnet restore ghosts.api.sln
dotnet build ghosts.api.sln --no-restore

# Install Angular CLI 20 and setup Angular project
echo ""
echo "========================================="
echo "Installing Angular CLI 20..."
echo "========================================="
sudo npm install -g @angular/cli@20

echo ""
echo "Installing Angular dependencies..."
cd ghosts.ng
npm install
cd ../..

# Install Python packages for AI integrations
echo ""
echo "========================================="
echo "Installing Python packages..."
echo "========================================="
pip install --no-cache-dir --break-system-packages openai anthropic

echo ""
echo "========================================="
echo "Setup completed successfully!"
echo "========================================="

# Welcome message
cat <<'EOF'


                   ('-. .-.               .-')    .-') _     .-')
                 ( OO )  /              ( OO ). (  OO) )   ( OO ).
        ,----.    ,--. ,--. .-'),-----. (_)---\_)/     '._ (_)---\_)
       '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |
       |  |_( O- )|   .|  |/   |  | |  |\  :` `. '--.  .--'\  :` `.
       |  | .--, \|       |\_) |  |\|  | '..`''.)   |  |    '..`''.)
      (|  | '. (_/|  .-.  |  \ |  | |  |.-._)   \   |  |   .-._)   \
       |  '--'  | |  | |  |   `'  '-'  '\       /   |  |   \       /
       `------'  `--' `--'     `-----'  `-----'    `--'    `-----'

              Welcome to the GHOSTS Development Container!

Type Ctrl-Shift-` (backtick) to open a new terminal and get started building. 🤓👻

EOF
