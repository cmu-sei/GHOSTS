#!/usr/bin/env node
// Generate a bcrypt password hash for N8N_OWNER_PASSWORD_HASH.
// Usage: node generate-owner-password-hash.mjs 'YourPassword'
// Or via Docker: docker run --rm -v ./scripts:/scripts docker.n8n.io/n8nio/n8n:1.93.0 node /scripts/generate-owner-password-hash.mjs 'YourPassword'

const password = process.argv[2];
if (!password) {
  console.error('Usage: node generate-owner-password-hash.mjs <password>');
  process.exit(1);
}

let bcrypt;
try {
  bcrypt = await import('bcryptjs');
} catch {
  try {
    bcrypt = await import('bcrypt');
  } catch {
    console.error('Error: Neither bcryptjs nor bcrypt found. Run this inside the n8n container image.');
    process.exit(1);
  }
}

const hash = bcrypt.default?.hashSync?.(password, 10) ?? bcrypt.hashSync(password, 10);
console.log(hash);
