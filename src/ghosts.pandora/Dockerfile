# Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

# pull the official base image
FROM python:3.8.5-alpine

# set work directory
WORKDIR /usr/src/app

# set environment variables
ENV PYTHONDONTWRITEBYTECODE 1
ENV PYTHONUNBUFFERED 1

# install system dependencies
RUN apk add --no-cache zlib-dev jpeg-dev gcc musl-dev ffmpeg libxml2-dev libxslt-dev

# upgrade pip
RUN python3 -m pip install --upgrade pip

# copy requirements and install Python dependencies
COPY ./requirements.txt /usr/src/app
RUN pip install --no-cache-dir -r requirements.txt

# copy project files
COPY ./app /usr/src/app

LABEL MAINTAINER Dustin Updyke <ddupdyke[at]sei.cmu.edu>
LABEL PRODUCT GHOSTS PANDORA
LABEL VERSION Browse to /about

# expose port
EXPOSE 80

# set entrypoint
ENTRYPOINT [ "python3" ]

# run the application
CMD [ "app.py", "80" ]
