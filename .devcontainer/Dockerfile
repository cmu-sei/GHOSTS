# The base Python 3.14 is reused by LiteLLM, Open Terminal, SearXNG, and supervisor
FROM mcr.microsoft.com/devcontainers/python:3.14-trixie

# Custom certificate support. Copy the folder, not a *.crt glob: COPY fails the build
# when a glob matches nothing, and a clone outside a TLS-inspecting network has no certs.
COPY ./features/sei-certs/ /tmp/sei-certs/
RUN mkdir -p /usr/local/share/ca-certificates/custom \
    && find /tmp/sei-certs -maxdepth 1 -name '*.crt' -exec cp {} /usr/local/share/ca-certificates/custom/ \; \
    && rm -rf /tmp/sei-certs \
    && update-ca-certificates
