// Component ablation: headed Edge, real App and actual AgentUiDriver. No inference.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process'), { pathToFileURL } = require('node:url');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), app = path.join(root, 'apps/web'), stamp = Date.now();
const apiBuild = path.resolve(root, process.env.NOVA_TEST_BUILD || '.nova-build/latency/candidate/QuickPrompt.Api.dll');
const samples = Number(process.env.NOVA_UI_SAMPLES || 30);
assert.ok(Number.isInteger(samples) && samples >= 1 && samples <= 30, 'Use one to thirty measured samples per mode.');
const out = path.join(root, 'docs/evidence/ui-latency-' + stamp), temp = path.join(root, '.playwright/ui-latency-' + stamp);
fs.mkdirSync(out, { recursive: true }); fs.mkdirSync(temp, { recursive: true });
let api, vite, browser, assetsVerified = false; const results = [], errors = [];
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const stats = values => { if (!values.length) return { count: 0, p50Ms: null, p95Ms: null, throughputPerSecond: 0 }; const sorted = values.toSorted((a, b) => a - b); return { count: values.length, p50Ms: sorted[Math.ceil(values.length * .5) - 1], p95Ms: sorted[Math.ceil(values.length * .95) - 1], throughputPerSecond: 1000 / (values.reduce((a, b) => a + b, 0) / values.length) }; };
function save(status) { fs.writeFileSync(path.join(out, 'measurements.json'), JSON.stringify({ status, scope: samples < 30 ? 'UI_COMMAND_SMOKE_NOT_LATENCY_QUALIFICATION' : 'COMPONENT_UI_TIMING_NOT_AGENT_LATENCY', headed: true, samePage: true, inference: false, syntheticDataOnly: true, apiBuild: path.relative(root, apiBuild), assetsVerified, graphics: 'isolated Edge software rendering', measuredSamplesPerMode: samples, warmupsExcludedPerMode: 1, normal: stats(results.filter(r => r.speed === 'normal').map(r => r.durationMs)), fast: stats(results.filter(r => r.speed === 'fast').map(r => r.durationMs)), results, errors }, null, 2)); }
(async () => {
  api = spawn('dotnet', [apiBuild, '--QuickPrompt:Port=5199', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(app, 'dist'), '--QuickPrompt:Ollama=http://127.0.0.1:1'], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  let ready = false; api.stdout.on('data', data => { if (String(data).includes('Now listening on')) ready = true; }); api.stderr.on('data', () => {});
  for (let i = 0; i < 200 && !ready; i++) await sleep(100); assert.ok(ready);
  const { createServer } = await import(pathToFileURL(require.resolve('vite', { paths: [app] })).href);
  vite = await createServer({ root: path.join(__dirname, 'benchmarks'), publicDir: path.join(app, 'public'), configFile: path.join(app, 'vite.config.ts'), resolve: { alias: { react: path.join(app, 'node_modules/react'), 'react-dom': path.join(app, 'node_modules/react-dom') } }, server: { port: 5198, strictPort: true, host: '127.0.0.1', fs: { allow: [root] }, proxy: { '/api': { target: 'http://127.0.0.1:5199', changeOrigin: true, configure(proxy) { proxy.on('proxyReq', request => request.setHeader('Origin', 'http://127.0.0.1:5199')); } } } } });
  await vite.listen(); browser = await chromium.launch({ channel: 'msedge', headless: false, args: ['--disable-gpu'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } }); page.on('pageerror', error => errors.push(error.message));
  page.on('crash', () => { errors.push('OWNED_PAGE_CRASHED'); save('INTERRUPTED'); });
  await page.goto('http://127.0.0.1:5198/driver.html'); await page.getByRole('button', { name: 'Nuevo prompt', exact: true }).click(); await page.getByTestId('prompt-editor').waitFor();
  await page.waitForFunction(() => { const logos = Array.from(document.querySelectorAll('.nova-logo img')); return logos.length > 0 && logos.every(img => img.complete && img.naturalWidth > 0); }); assetsVerified = true;
  const body = 'Analiza el material proporcionado y presenta conclusiones verificables. Conserva los datos originales y señala lo que falta. No inventes información. 🚀\n'.repeat(8);
  for (let sample = 0; sample <= samples; sample++) {
    for (const speed of sample % 2 ? ['normal', 'fast'] : ['fast', 'normal']) {
      await page.locator('#prompt-title').fill(''); await page.locator('#prompt-content').fill('');
      const measured = await page.evaluate(async ({ speed, body }) => window.novaMeasureUi(speed, body), { speed, body });
      assert.ok(measured.exactText); assert.equal(measured.cursorTargets, 2); assert.ok(speed === 'fast' ? measured.inputEvents === 1 : measured.inputEvents > 1);
      if (sample) { results.push({ sample, speed, ...measured }); save('RUNNING'); }
    }
    if (sample % 10 === 0) console.log('UI samples per mode', sample);
  }
  await page.screenshot({ path: path.join(out, 'verified-editor.png'), animations: 'disabled' });
  assert.deepEqual(errors, []);
  save('PASS');
  console.log('PASS', out);
})().catch(error => { console.error(error); errors.push(error.message); save('INTERRUPTED'); fs.writeFileSync(path.join(out, 'failure.txt'), error.stack); process.exitCode = 1; }).finally(async () => { await browser?.close(); await vite?.close(); if (api?.exitCode === null) { api.kill(); await new Promise(resolve => api.once('exit', resolve)); } });
