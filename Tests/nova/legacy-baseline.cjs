const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..');
const temporary = path.join(root, '.playwright/nova-legacy-' + Date.now());
fs.mkdirSync(temporary, { recursive: true });
let server, browser;
(async () => {
  let output = '';
  server = spawn('dotnet', [path.join(root, '.playwright/nova-baseline/Debug/net8.0/QuickPrompt.Web.dll'), '--QuickPrompt:Port=5196', '--QuickPrompt:Demo=true', `--QuickPrompt:DataPath=${path.join(temporary, 'legacy.sqlite')}`], { cwd: path.join(root, 'src/QuickPrompt.Web'), windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  server.stdout.on('data', chunk => { output += chunk; });
  server.stderr.on('data', chunk => { output += chunk; });
  for (let attempt = 0; attempt < 150; attempt++) {
    if (server.exitCode !== null) throw new Error('Owned legacy baseline failed to start');
    if (output.includes('Now listening on: http://127.0.0.1:5196')) break;
    await new Promise(resolve => setTimeout(resolve, 100));
    if (attempt === 149) throw new Error('Legacy baseline did not start');
  }
  browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.goto('http://127.0.0.1:5196');
  await page.getByRole('button', { name: 'Nueva plantilla', exact: true }).waitFor();
  await page.screenshot({ path: path.join(root, 'docs/evidence/nova/legacy-desktop.png'), fullPage: true });
  console.log('EXECUTED_PASS legacy visual baseline, isolated synthetic data only');
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => { if (browser) await browser.close(); if (server && server.exitCode === null) server.kill(); });
