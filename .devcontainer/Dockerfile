# The base Python 3.14 is reused by LiteLLM, Open Terminal, SearXNG, and supervisor
FROM mcr.microsoft.com/devcontainers/python:3.14-trixie

# Custom certificate support.
COPY ./features/sei-certs/*.crt /usr/local/share/ca-certificates/custom/
RUN update-ca-certificates
