# multi-stage target: dev
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev

ENV ASPNETCORE_URLS=http://*:5000 \
    ASPNETCORE_ENVIRONMENT=DEVELOPMENT

COPY . /app
WORKDIR /app

# Create Payloads directory for custom payload files
RUN mkdir -p /app/Payloads

RUN dotnet publish -c Release -o /app/dist

CMD ["dotnet", "run"]

# multi-stage target: prod
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod

# Install SkiaSharp dependencies
RUN apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    apt-get update -o Acquire::AllowInsecureRepositories=true -o Acquire::AllowDowngradeToInsecureRepositories=true && \
    apt-get install -y -o APT::Get::AllowUnauthenticated=true \
    libfontconfig1 \
    libfreetype6 \
    libx11-6 \
    libxcb1 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

ARG commit
ENV COMMIT=$commit

COPY --from=dev /app/dist /app

WORKDIR /app

# Create necessary directories
RUN mkdir -p /app/Payloads && \
    mkdir -p /app/_data

# Application configuration
ENV ASPNETCORE_URLS=http://*:5000

# Mode configuration (can be overridden at runtime)
# MODE_TYPE: "social" for social media mode, "website" for website mode
ENV MODE_TYPE=social \
    DEFAULT_THEME=facebook \
    SITE_TYPE=news \
    SITE_NAME="The Daily Paranormal" \
    ARTICLE_COUNT=12

# To override these values, use:
# docker run -e MODE_TYPE=website -e SITE_TYPE=shopping ghosts-pandora
# Or in docker-compose.yml:
#   environment:
#     - MODE_TYPE=website
#     - SITE_TYPE=shopping

EXPOSE 5000

CMD ["dotnet", "ghosts-pandora.dll"]
