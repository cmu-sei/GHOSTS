#!/bin/bash
set -euo pipefail

# Show git dirty status in zsh prompt
git config devcontainers-theme.show-dirty 1

sudo chown -R $(whoami): /home/vscode/.microsoft

echo ""
echo "========================================="
echo "Installing Microsoft Aspire..."
echo "========================================="
dotnet tool install -g Aspire.Cli

# Install Angular CLI 21 and setup Angular project
echo ""
echo "========================================="
echo "Installing Angular CLI 21..."
echo "========================================="
npm install -g @angular/cli@latest

echo ""
echo "Installing Angular dependencies..."
cd src/ghosts.ng
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
