# Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# pull the official base image
FROM python:3.8.5-alpine

# set work directory
WORKDIR /usr/src/app

# set environment variables
ENV PYTHONDONTWRITEBYTECODE 1
ENV PYTHONUNBUFFERED 1

# install dependencies
RUN apk add -u zlib-dev jpeg-dev gcc musl-dev
RUN python3 -m pip install --upgrade pip
COPY ./requirements.txt /usr/src/app
COPY ./readme.md /usr/src
COPY ../../LICENSE.md /usr/src
RUN pip install -r requirements.txt

# copy project
COPY ./app /usr/src/app

LABEL maintainer="ddupdyke[at]sei.cmu.edu"
LABEL product="GHOSTS PANDORA"

EXPOSE 443

ENTRYPOINT [ "python3" ]

CMD [ "app.py", "80" ]
