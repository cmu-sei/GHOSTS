dotnet publish -c Release -o bin/publish

docker image rm ghosts/staypuft
docker build . -f dockerfile-alpine -t ghosts/staypuft

# docker run -d -p 7000:5000 --name ghosts-staypuft-001 ghosts/staypuft

# rm ~/Downloads/ghosts-staypuft.tar
# docker save ghosts/staypuft > ~/Downloads/ghosts-staypuft.tar
# cat ghosts-staypuft.tar | docker load
