#
#multi-stage target: dev
#
#FROM microsoft/dotnet:2.1-sdk AS dev

#ENV ASPNETCORE_URLS=http://*:5000 \
#   ASPNETCORE_ENVIRONMENT=DEVELOPMENT

#COPY . /app/src
#WORKDIR /app/src

#RUN dotnet restore
#RUN dotnet build
#RUN dotnet publish -c Release -o bin/publish

#CMD ["dotnet", "run"]



#
#multi-stage target: prod
#
FROM microsoft/dotnet:2.1-aspnetcore-runtime-alpine

COPY bin/publish/. /app/

ARG commit
ENV COMMIT=$commit

WORKDIR /app
ENV ASPNETCORE_URLS=http://*:5000
EXPOSE 5000

CMD ["dotnet", "ghosts.api.dll"]

#CMD ["ls"]
