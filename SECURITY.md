# Security Policy

## Reporting a Vulnerability

The GHOSTS team at Carnegie Mellon University's Software Engineering Institute (CMU SEI) takes security seriously. If you discover a security vulnerability in GHOSTS, please report it responsibly.

That being said, GHOSTS is designed for use in isolated training environments and does not include built-in authentication or access controls. Therefore, vulnerabilities that require physical access or are only exploitable in a test environment may be considered lower priority. However, we still encourage responsible disclosure of all security issues.

### Scope

The following are in scope for security reports:

- **GHOSTS API** (Ghosts.Api) — authentication bypass, injection, unauthorized access
- **GHOSTS Clients** (Windows, Universal, Lite) — privilege escalation, arbitrary code execution
- **Docker Compose configuration** — exposed secrets, insecure defaults
- **Pandora** — content injection, server-side vulnerabilities
- **Frontend** (Ghosts.Frontend) — XSS, CSRF, sensitive data exposure

### Out of Scope

- Denial of service against test/demo instances
- Social engineering of project maintainers
- Issues in third-party dependencies (report those upstream, but let us know)
- Vulnerabilities that require physical access to the host machine

### Security Design Considerations

GHOSTS is designed for use in **isolated training and exercise networks**. It does not include built-in authentication — deployments exposed to untrusted networks should be placed behind an authentication proxy (e.g., OAuth2 Proxy, Keycloak). See the [deployment documentation](https://cmu-sei.github.io/GHOSTS/core/api/) for guidance.
