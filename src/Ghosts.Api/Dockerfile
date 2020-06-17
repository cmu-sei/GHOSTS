# Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# $ docker build -f Dockerfile-api  . -t ghosts/api

#
#multi-stage target: dev
#
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS dev

ENV ASPNETCORE_URLS=http://*:5000 \
    ASPNETCORE_ENVIRONMENT=DEVELOPMENT

COPY . /app

WORKDIR /app
RUN dotnet publish ghosts.api.csproj -c Release -o /app/dist
CMD ["dotnet", "run"]

#
#multi-stage target: prod
#
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS prod
ARG commit
ENV COMMIT=$commit
COPY --from=dev /app/dist /app
WORKDIR /app

ENV GHOSTS_VERSION=3.0.0.0
ENV GHOSTS_API_VERSION=v2
ENV ASPNETCORE_URLS=http://*:5000

EXPOSE 5000
ENV ASPNETCORE_URLS=http://*:5000
CMD ["dotnet", "ghosts.api.dll"]