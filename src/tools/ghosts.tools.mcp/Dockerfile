FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

COPY .devcontainer/certs /usr/local/share/ca-certificates/custom/
RUN find /usr/local/share/ca-certificates/custom -type f ! -name '*.crt' -delete \
    && if find /usr/local/share/ca-certificates/custom -type f -name '*.crt' -print -quit | grep -q .; then \
    update-ca-certificates; \
    fi

COPY src/tools/ghosts.tools.mcp/. /src

WORKDIR /src
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:5055

EXPOSE 5055

CMD ["dotnet", "ghosts.tools.mcp.dll", "--http"]
