FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dev

# copy csproj and restore as distinct layers
COPY ghosts.linux.sln ./app/
COPY ghosts.client.linux/*csproj ./app/ghosts.client.linux/
COPY Ghosts.Domain/*csproj ./app/Ghosts.Domain/
COPY Ghosts.Domain/*.config ./app/Ghosts.Domain/

WORKDIR /app
RUN dotnet restore

# copy everything else and build app
COPY ghosts.client.linux/     ./ghosts.client.linux/
COPY Ghosts.Domain/     ./Ghosts.Domain/
WORKDIR /app/ghosts.client.linux/
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS prod

RUN apk add curl && \
    apk add nmap && \
    apk add busybox-extras && \
    apk add nikto && \
    apk add openssh-client && \
    apk add openssh-keygen && \
    apk add python && \
    wget 'https://github.com/sqlmapproject/sqlmap/tarball/master' --output-document=sqlmap.tar.gz && \
    mkdir sqlmap && \
    tar -xvf sqlmap.tar.gz -C sqlmap && \
    cd sqlmap/
# python sqlmap.py --version

COPY --from=dev /app/ghosts.client.linux/out ./app
WORKDIR /app/  

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000/tcp

CMD ["dotnet", "./ghosts.client.linux.dll"]
