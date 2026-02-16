#!/usr/bin/env node
const { spawn } = require('child_process');
const path = require('path');

const port = process.env.PORT || '4200';
const args = ['serve', '--host', '0.0.0.0', '--port', port];

console.log(`Starting Angular dev server on port ${port}...`);

const ngPath = path.join(__dirname, 'node_modules', '.bin', 'ng');
const ng = spawn(process.platform === 'win32' ? 'ng.cmd' : ngPath, args, {
  stdio: 'inherit'
});

ng.on('exit', (code) => {
  process.exit(code);
});
