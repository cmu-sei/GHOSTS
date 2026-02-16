#!/bin/bash
set -euo pipefail

# bash profile configuration
cat >>$HOME/.bashrc <<'EOF'

# Load aws environment variables
set -a; source .devcontainer/aws.env; set +a

# Tell Claude Code to use Amazon Bedrock
export CLAUDE_CODE_USE_BEDROCK=1

# Configure Claude Code for AWS Commercial or GovCloud
if [[ "$AWS_ENABLE_GOVCLOUD" == "1" ]]; then
    export AWS_REGION=us-gov-east-1
    export ANTHROPIC_MODEL=us-gov.anthropic.claude-sonnet-4-5-20250929-v1:0
    export ANTHROPIC_DEFAULT_HAIKU_MODEL=us-gov.anthropic.claude-3-haiku-20240307-v1:0
else
    export AWS_REGION=us-east-1
    export ANTHROPIC_DEFAULT_HAIKU_MODEL=us.anthropic.claude-haiku-4-5-20251001-v1:0
fi

EOF

# zsh profile configuration (same as bash since zsh is the default shell)
cat >>$HOME/.zshrc <<'EOF'

# Load aws environment variables
set -a; source .devcontainer/aws.env; set +a

# Tell Claude Code to use Amazon Bedrock
export CLAUDE_CODE_USE_BEDROCK=1

# Configure Claude Code for AWS Commercial or GovCloud
if [[ "$AWS_ENABLE_GOVCLOUD" == "1" ]]; then
    export AWS_REGION=us-gov-east-1
    export ANTHROPIC_MODEL=us-gov.anthropic.claude-sonnet-4-5-20250929-v1:0
    export ANTHROPIC_DEFAULT_HAIKU_MODEL=us-gov.anthropic.claude-3-haiku-20240307-v1:0
else
    export AWS_REGION=us-east-1
    export ANTHROPIC_DEFAULT_HAIKU_MODEL=us.anthropic.claude-haiku-4-5-20251001-v1:0
fi

EOF

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
cd src/Ghosts.Frontend
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
echo "Installing Claude Code..."
echo "========================================="
curl -fsSL https://claude.ai/install.sh | bash

echo ""
echo "========================================="
echo "Setup completed successfully!"
echo "========================================="

