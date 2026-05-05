# n8n Bootstrap for GHOSTS

Self-hosted n8n instance with automatic owner account creation and workflow import on first boot. Fully idempotent — safe to restart without duplicating data.

## Quick Start

1. **Generate an encryption key:**

   ```bash
   openssl rand -hex 32
   ```

2. **Generate an owner password hash:**

   ```bash
   docker run --rm -v ./scripts:/scripts docker.n8n.io/n8nio/n8n:1.93.0 \
     node /scripts/generate-owner-password-hash.mjs 'YourSecurePassword'
   ```

3. **Create your `.env` file:**

   ```bash
   cp .env.example .env
   ```

   Fill in `N8N_ENCRYPTION_KEY` and `N8N_OWNER_PASSWORD_HASH` (escape `$` as `$$`).

4. **Start the stack:**

   ```bash
   docker compose up -d
   ```

   The provisioner container will import all workflows from `../n8n-workflows/` and exit. The main n8n container will report healthy only after provisioning is complete.

5. **Access n8n:** http://localhost:5678

## How It Works

### Owner Account Bootstrap

n8n 1.88+ supports environment-managed owner accounts via:

- `N8N_INSTANCE_OWNER_MANAGED_BY_ENV=true`
- `N8N_INSTANCE_OWNER_EMAIL`, `N8N_INSTANCE_OWNER_FIRST_NAME`, `N8N_INSTANCE_OWNER_LAST_NAME`
- `N8N_INSTANCE_OWNER_PASSWORD_HASH` (bcrypt)

The owner account is created or updated on every n8n startup based on these env vars. No manual setup wizard is required.

### Workflow Import

The `n8n-provisioner` sidecar container:

1. Waits for n8n to become reachable at `/healthz`
2. Imports credentials from `./credentials/` (if any exist)
3. Imports workflows from `../n8n-workflows/` using `n8n import:workflow --separate`
4. Writes a sentinel file (`/home/node/.n8n/.bootstrap_complete`)
5. Exits successfully

Workflow files use stable IDs (e.g., `qEsKWGTWD4WKpm_6gQxLD`). Re-importing the same ID updates the existing workflow rather than creating a duplicate.

### Credential Import

Place exported credential JSON files in `./credentials/`. These are imported before workflows so that workflow references resolve correctly. The credentials directory is mounted read-only.

## API Key for GHOSTS Integration

n8n public API keys are created through the UI — there is no official env var to bootstrap a default API key.

After first boot:

1. Log in to n8n at http://localhost:5678
2. Go to **Settings > API > Create API Key**
3. Copy the key and add it to your GHOSTS API `.env`:

   ```
   N8N_API_KEY=your-generated-api-key
   ```

### Seed Database Snapshot (Advanced)

For operators who need a known API key at first boot (e.g., CI/CD):

1. Start the stack and configure everything via the UI (API key, credentials, etc.)
2. Dump the PostgreSQL database: `docker compose exec postgres pg_dump -U n8n n8n > seed.sql`
3. On fresh environments, restore from the dump before starting n8n

## Encryption Key Warning

The `N8N_ENCRYPTION_KEY` is used to encrypt all stored credentials. If you change or lose this key, all saved credentials become permanently unreadable. Treat it like a database master password.

## Bcrypt Hash Escaping in `.env`

Bcrypt hashes contain `$` characters. In `.env` files consumed by Docker Compose, each `$` must be escaped as `$$`:

```
# Raw hash from the generator:
$2b$10$abcdef...

# Escaped for .env:
$$2b$$10$$abcdef...
```

## Troubleshooting

**n8n never becomes healthy:**
- Check that the provisioner ran successfully: `docker compose logs n8n-provisioner`
- The healthcheck requires both `/healthz` responding AND the sentinel file existing
- If the provisioner failed, fix the issue and restart: `docker compose up -d n8n-provisioner`

**Workflows not appearing:**
- Verify workflow JSON files exist in `../n8n-workflows/`
- Check provisioner logs for import errors
- Ensure each workflow file has a valid `id` field

**"Invalid credentials" after restart:**
- The encryption key likely changed. Credentials encrypted with the old key cannot be decrypted.
- Restore the original `N8N_ENCRYPTION_KEY` or re-create credentials.

**Owner login fails:**
- Verify the password hash was generated correctly
- Ensure `$` characters are escaped as `$$` in the `.env` file
- Regenerate: `docker run --rm -v ./scripts:/scripts docker.n8n.io/n8nio/n8n:1.93.0 node /scripts/generate-owner-password-hash.mjs 'password'`

**Provisioner keeps restarting:**
- The provisioner has `restart: "no"` — it should run once and exit
- If it exited with an error, check logs and re-run manually: `docker compose run --rm n8n-provisioner`
