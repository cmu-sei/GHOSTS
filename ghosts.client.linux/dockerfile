# dotnet publish -c release -o bin/published

# docker build . -t ghosts/client
# docker run -d -p 28085:80 --network ghosts-network --name ghosts-client ghosts/client
# Change network to ghosts-network

FROM microsoft/dotnet:2.1-runtime-alpine

WORKDIR /root/  
COPY  bin/published .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000/tcp
CMD ["dotnet", "./ghosts.client.linux.dll"]