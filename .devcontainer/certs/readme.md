# Custom Certificates

Place PEM-encoded root CA certificates in this folder with a `.crt` extension before
rebuilding the dev container. The Dockerfile installs every `.crt` file here
into the container's system trust store via `update-ca-certificates`; all other file
types are ignored.

If you rely on Zscaler (or another SSL inspection solution), copy the issued root certificate into this folder as `*.crt`, rebuild, and the container will trust outbound TLS through that proxy.