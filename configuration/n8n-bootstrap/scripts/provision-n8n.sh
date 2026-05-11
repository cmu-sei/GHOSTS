#!/bin/sh
set -e

SENTINEL="/home/node/.n8n/.bootstrap_complete"
WORKFLOW_DIR="/bootstrap/workflows"
CREDENTIAL_DIR="/bootstrap/credentials"
N8N_HOST="${N8N_HOST:-n8n}"
N8N_URL="http://${N8N_HOST}:5678"

echo "[provisioner] Starting n8n provisioning..."

# Wait for n8n REST API to be fully ready (not just healthz)
echo "[provisioner] Waiting for n8n REST API to become ready..."
MAX_ATTEMPTS=60
ATTEMPT=0
while true; do
  ATTEMPT=$((ATTEMPT + 1))
  if [ "$ATTEMPT" -ge "$MAX_ATTEMPTS" ]; then
    echo "[provisioner] ERROR: n8n REST API did not become ready after $MAX_ATTEMPTS attempts."
    exit 1
  fi
  RESPONSE=$(wget -qO- "$N8N_URL/rest/settings" 2>/dev/null || true)
  if echo "$RESPONSE" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{JSON.parse(d);process.exit(0)}catch{process.exit(1)}})" 2>/dev/null; then
    break
  fi
  echo "[provisioner] Attempt $ATTEMPT/$MAX_ATTEMPTS — n8n not ready yet, waiting 5s..."
  sleep 5
done
echo "[provisioner] n8n REST API is ready."

# Check actual n8n state — if owner is already configured and sentinel exists, skip
if [ -f "$SENTINEL" ]; then
  SETUP_CHECK=$(node -e "
    const http = require('http');
    http.get('$N8N_URL/rest/settings', res => {
      let body='';
      res.on('data', c => body+=c);
      res.on('end', () => {
        const d = JSON.parse(body);
        const show = d.data ? d.data.userManagement.showSetupOnFirstLoad : d.userManagement.showSetupOnFirstLoad;
        console.log(show ? 'yes' : 'no');
      });
    });
  " 2>/dev/null || echo "yes")
  if [ "$SETUP_CHECK" = "no" ]; then
    echo "[provisioner] Sentinel exists and owner is configured — already provisioned. Exiting."
    exit 0
  fi
  echo "[provisioner] Sentinel exists but n8n needs setup (DB was recreated). Re-provisioning..."
  rm -f "$SENTINEL"
fi

# Create owner account via REST API if not already set up
if [ -n "$N8N_OWNER_EMAIL" ] && [ -n "$N8N_OWNER_PASSWORD" ]; then
  echo "[provisioner] Checking if owner account needs setup..."
  SETUP_NEEDED=$(node -e "
    const http = require('http');
    http.get('$N8N_URL/rest/settings', res => {
      let body='';
      res.on('data', c => body+=c);
      res.on('end', () => {
        const d = JSON.parse(body);
        const show = d.data ? d.data.userManagement.showSetupOnFirstLoad : d.userManagement.showSetupOnFirstLoad;
        console.log(show ? 'yes' : 'no');
      });
    });
  ")

  if [ "$SETUP_NEEDED" = "yes" ]; then
    echo "[provisioner] Creating owner account for $N8N_OWNER_EMAIL..."
    RESULT=$(node -e "
      const http = require('http');
      const data = JSON.stringify({
        email: process.env.N8N_OWNER_EMAIL,
        firstName: process.env.N8N_OWNER_FIRST_NAME || 'Admin',
        lastName: process.env.N8N_OWNER_LAST_NAME || 'User',
        password: process.env.N8N_OWNER_PASSWORD
      });
      const req = http.request({
        hostname: process.env.N8N_HOST || 'n8n', port: 5678, path: '/rest/owner/setup',
        method: 'POST',
        headers: {'Content-Type':'application/json','Content-Length':data.length}
      }, res => {
        let body='';
        res.on('data', c => body+=c);
        res.on('end', () => {
          if (res.statusCode === 200) { console.log('OK'); }
          else { console.error('FAIL: ' + res.statusCode + ' ' + body); process.exit(1); }
        });
      });
      req.write(data);
      req.end();
    ")
    if [ "$RESULT" != "OK" ]; then
      echo "[provisioner] ERROR: Failed to create owner account."
      exit 1
    fi
    echo "[provisioner] Owner account created successfully."
  else
    echo "[provisioner] Owner account already exists — skipping."
  fi
else
  echo "[provisioner] N8N_OWNER_EMAIL/N8N_OWNER_PASSWORD not set — skipping owner setup."
fi

# Import credentials first (they may be referenced by workflows)
if [ -d "$CREDENTIAL_DIR" ] && ls "$CREDENTIAL_DIR"/*.json 1>/dev/null 2>&1; then
  echo "[provisioner] Importing credentials from $CREDENTIAL_DIR..."
  n8n import:credentials --separate --input="$CREDENTIAL_DIR"
  echo "[provisioner] Credentials imported successfully."
else
  echo "[provisioner] No credential files found — skipping."
fi

# Import workflows via REST API to avoid CLI webhook/FK issues on n8n 2.x
if [ -d "$WORKFLOW_DIR" ] && ls "$WORKFLOW_DIR"/*.json 1>/dev/null 2>&1; then
  echo "[provisioner] Importing workflows via REST API..."

  # Get a session cookie by logging in
  SESSION=$(node -e "
    const http = require('http');
    const data = JSON.stringify({
      emailOrLdapLoginId: process.env.N8N_OWNER_EMAIL,
      password: process.env.N8N_OWNER_PASSWORD
    });
    const req = http.request({
      hostname: process.env.N8N_HOST || 'n8n', port: 5678, path: '/rest/login',
      method: 'POST',
      headers: {'Content-Type':'application/json','Content-Length':data.length}
    }, res => {
      const cookies = res.headers['set-cookie'] || [];
      const session = cookies.find(c => c.startsWith('n8n-auth='));
      if (session) { console.log(session.split(';')[0]); }
      else { console.error('Login failed: ' + res.statusCode); process.exit(1); }
    });
    req.write(data);
    req.end();
  ")

  # Import each workflow
  node -e "
    const http = require('http');
    const fs = require('fs');
    const path = require('path');
    const dir = '$WORKFLOW_DIR';
    const cookie = '$SESSION';

    async function importWorkflow(filePath) {
      const wf = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      const name = wf.name || path.basename(filePath, '.json');
      wf.active = false;
      const data = JSON.stringify(wf);
      return new Promise((resolve, reject) => {
        const req = http.request({
          hostname: process.env.N8N_HOST || 'n8n', port: 5678, path: '/rest/workflows',
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(data),
            'Cookie': cookie
          }
        }, res => {
          let body = '';
          res.on('data', c => body += c);
          res.on('end', () => {
            if (res.statusCode === 200) {
              console.log('  Imported: ' + name);
              resolve();
            } else if (res.statusCode === 409) {
              console.log('  Exists:   ' + name + ' (skipped)');
              resolve();
            } else {
              console.error('  FAILED:   ' + name + ' (' + res.statusCode + ')');
              console.error('            ' + body.substring(0, 200));
              reject(new Error('Import failed for ' + name));
            }
          });
        });
        req.write(data);
        req.end();
      });
    }

    (async () => {
      const files = fs.readdirSync(dir).filter(f => f.endsWith('.json'));
      for (const f of files) {
        await importWorkflow(path.join(dir, f));
      }
      console.log('Done: ' + files.length + ' workflows processed.');
    })().catch(e => { console.error(e.message); process.exit(1); });
  "
  echo "[provisioner] Workflow import complete."
else
  echo "[provisioner] No workflow files found — skipping."
fi

# Create API key for GHOSTS integration
if [ -n "$N8N_OWNER_EMAIL" ] && [ -n "$N8N_OWNER_PASSWORD" ]; then
  echo "[provisioner] Creating n8n API key..."
  API_KEY=$(node -e "
    const http = require('http');
    const loginData = JSON.stringify({
      emailOrLdapLoginId: process.env.N8N_OWNER_EMAIL,
      password: process.env.N8N_OWNER_PASSWORD
    });
    const req = http.request({
      hostname: process.env.N8N_HOST || 'n8n', port: 5678, path: '/rest/login',
      method: 'POST',
      headers: {'Content-Type':'application/json','Content-Length':Buffer.byteLength(loginData)}
    }, res => {
      const cookies = res.headers['set-cookie'] || [];
      const sessionCookie = cookies.find(c => c.startsWith('n8n-auth='));
      if (!sessionCookie) { console.error('Login failed'); process.exit(1); }
      const session = sessionCookie.split(';')[0];
      const expiresAt = Date.now() + (10 * 365.25 * 24 * 60 * 60 * 1000);
      const keyData = JSON.stringify({
        label: 'ghosts-api',
        expiresAt,
        scopes: [
          'workflow:create','workflow:read','workflow:update','workflow:delete','workflow:list',
          'workflow:activate','workflow:deactivate',
          'execution:read','execution:list','execution:retry','execution:delete'
        ]
      });
      const req2 = http.request({
        hostname: process.env.N8N_HOST || 'n8n', port: 5678, path: '/rest/api-keys',
        method: 'POST',
        headers: {'Content-Type':'application/json','Cookie':session,'Content-Length':Buffer.byteLength(keyData)}
      }, res2 => {
        let b='';
        res2.on('data', c => b+=c);
        res2.on('end', () => {
          if (res2.statusCode === 200) {
            const parsed = JSON.parse(b);
            console.log(parsed.data.rawApiKey);
          } else {
            console.error('API key creation failed: ' + res2.statusCode + ' ' + b);
            process.exit(1);
          }
        });
      });
      req2.write(keyData);
      req2.end();
    });
    req.write(loginData);
    req.end();
  ")

  if [ -n "$API_KEY" ]; then
    echo "$API_KEY" > /home/node/.n8n/.api_key
    echo "[provisioner] API key created and saved to /home/node/.n8n/.api_key"
    echo "[provisioner] N8N_API_KEY=$API_KEY"
  else
    echo "[provisioner] WARNING: Failed to create API key."
  fi
fi

# Write sentinel
touch "$SENTINEL"
echo "[provisioner] Provisioning complete. Sentinel written."
exit 0
