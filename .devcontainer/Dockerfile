FROM mcr.microsoft.com/devcontainers/dotnet:10.0

ARG TARGETARCH

# Switch to root for configuring image
USER root

# Custom Cert Support
COPY ./certs /usr/local/share/ca-certificates/custom/
RUN find /usr/local/share/ca-certificates/custom -type f ! -name '*.crt' -delete \
  && if find /usr/local/share/ca-certificates/custom -type f -name '*.crt' -print -quit | grep -q .; then \
  update-ca-certificates; \
  fi

ENV NODE_EXTRA_CA_CERTS=/etc/ssl/certs/ca-certificates.crt \
  AWS_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt \
  REQUESTS_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt \
  PIP_CERT=/etc/ssl/certs/ca-certificates.crt

# Switch back to vscode user
USER vscode
