# multi-stage target: dev
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dev

ENV ASPNETCORE_URLS=http://*:5000 \
    ASPNETCORE_ENVIRONMENT=DEVELOPMENT

COPY . /app
WORKDIR /app

RUN dotnet publish -c Release -o /app/dist

CMD ["dotnet", "run"]

# multi-stage target: prod
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS prod

ARG commit
ENV COMMIT=$commit

COPY --from=dev /app/dist /app

WORKDIR /app

ENV ASPNETCORE_URLS=http://*:5000

EXPOSE 5000

CMD ["dotnet", "ghosts-socializer.dll"]
