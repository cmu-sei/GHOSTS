#!/usr/bin/env bash

set -e

[ "${DEBUG:-false}" == 'true' ] && { set -x; S3FS_DEBUG='-d -d'; }

# Defaults
: ${FILESYSTEM:='local'} #s3
: ${STORAGE_PATH:='/opt/data'}

: ${AWS_S3_AUTHFILE:='/root/.s3fs'}
: ${AWS_S3_URL:='https://s3.amazonaws.com'}
: ${AWS_S3_REGION:='us-east-1'}
: ${S3FS_ARGS:=''}

# If no command specified, print error
[ "$1" == "" ] && set -- "$@" bash -c 'echo "Error: Please specify a command to run."; exit 128'


if [ "$FILESYSTEM" = "s3" ] ; then
    # Configuration checks
    if [ -z "$AWS_S3_BUCKET_NAME" ]; then
        echo "Error: AWS_S3_BUCKET_NAME is not specified"
        exit 128
    fi

    if [ ! -f "${AWS_S3_AUTHFILE}" ] && [ -z "$AWS_ACCESS_KEY_ID" ]; then
        echo "Error: AWS_ACCESS_KEY_ID not specified, or ${AWS_S3_AUTHFILE} not provided"
        exit 128
    fi

    if [ ! -f "${AWS_S3_AUTHFILE}" ] && [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
        echo "Error: AWS_SECRET_ACCESS_KEY not specified, or ${AWS_S3_AUTHFILE} not provided"
        exit 128
    fi

    # Write auth file if it does not exist
    if [ ! -f "${AWS_S3_AUTHFILE}" ]; then
       echo "${AWS_ACCESS_KEY_ID}:${AWS_SECRET_ACCESS_KEY}" > ${AWS_S3_AUTHFILE}
       chmod 400 ${AWS_S3_AUTHFILE}
    fi

    echo "---> Mounting S3 Filesystem ${STORAGE_PATH}"

    # s3fs mount command
    s3fs $S3FS_DEBUG $S3FS_ARGS -o default_acl=public-read -o passwd_file=${AWS_S3_AUTHFILE} -o url=${AWS_S3_URL} -o endpoint=${AWS_S3_REGION} -o allow_other ${AWS_S3_BUCKET_NAME} ${STORAGE_PATH}
fi

nginx=False
file="/usr/src/app/app.config"
while read -r line; do
    if [[ $line == "nginx_enabled=True" ]] ; then
        nginx=True
        break
    fi
done <$file 

if [ "$nginx" == "True" ] ; then
    echo "launching pandora on port 8081..."
    nohup bash -c "python3 app.py 8081 &"

    echo "serving video on port 1935..."
    nohup bash -c "python3 serve_movie_file.py &"

    echo "configuring and launching nginx..."
    # Run NGINX
    envsubst "$(env | sed -e 's/=.*//' -e 's/^/\$/g')" < \
    /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf && \
    nginx
else
    echo "nginx disabled, skipping..."

    echo "launching pandora on port 80..."
    nohup bash -c "python3 app.py 80"
fi