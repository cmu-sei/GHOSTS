# .devcontainer/server/Dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

USER root
ENV DEBIAN_FRONTEND=noninteractive

# Install Node.js 22.x (required for Angular 20)
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends curl ca-certificates; \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash -; \
    apt-get install -y nodejs; \
    npm install -g npm@latest; \
    rm -rf /var/lib/apt/lists/*

# Switch back to vscode user
USER vscode
