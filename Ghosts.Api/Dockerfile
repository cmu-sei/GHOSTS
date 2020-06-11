# Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# [Obsolete] - use ./Dockerfile-api
# this dockerfile is replaced with ./Dockerfile-api in the project root
# in order to better support automated builds for multiple project .net solutions

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

COPY bin/publish/. /app/

WORKDIR /app

ENV GHOSTS_VERSION=3.0.0.0
ENV GHOSTS_API_VERSION=v2
ENV ASPNETCORE_URLS=http://*:5000

EXPOSE 5000

CMD ["dotnet", "ghosts.api.dll"]