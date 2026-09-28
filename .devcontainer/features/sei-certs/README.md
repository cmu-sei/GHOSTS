# Custom Certificates

On a network that inspects TLS (Zscaler), place the PEM-encoded root CA
certificates in this folder with a `.crt` extension, then rebuild the dev container.
`install.sh` installs every `.crt` file here into the system trust store.

The `.crt` files are gitignored, so they never reach the public repository. The feature
files are tracked, so the container builds on any clone, with or without certificates.
