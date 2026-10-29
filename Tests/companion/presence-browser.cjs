// Product integration harness only: isolated Chromium, local built web, synthetic HTTP, no account/model/backend.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

const base = new URL(process.argv[2] || 'http://127.0.0.1:5288/');
if (base.origin !== 'http://127.0.0.1:5288' || base.pathname !== '/') throw new Error('Only the explicitly allocated local QA origin is allowed.');
const output = path.resolve(process.argv[3] || '.playwright/companion-presence-20261029/browser');
fs.mkdirSync(output, { recursive: true });
const owner = '93000000-0000-4000-8000-000000000001';
const device = '93000000-0000-4000-8000-000000000002';
const started = Date.now(); const calls = []; const checks = []; const unexpected = []; const browserErrors = [];
let phase = 'POLLING'; let result = null;
(async () => {
  const executable = process.env.QUICKPROMPT_TEST_CHROMIUM;
  if (executable && (!path.isAbsolute(executable) || !fs.statSync(executable).isFile())) throw new Error('The explicit existing QA browser executable is invalid.');
  const browser = await chromium.launch({ headless: true, ...(executable ? { executablePath: executable } : {}) });
  const context = await browser.newContext({ viewport: { width: 1365, height: 900 }, serviceWorkers: 'block' });
  try {
    await context.route('**/*', async route => {
      const request = route.request(); const url = new URL(request.url());
      if (url.origin !== base.origin) { unexpected.push('external request'); await route.abort(); return; }
      const json = body => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
      if (url.pathname === '/account/me') { calls.push(url.pathname); return json({ id: owner, email: 'synthetic-browser@example.invalid' }); }
      if (url.pathname === '/account/options') { calls.push(url.pathname); return json({ email: true, google: false, delegation: true, mailDelivery: 'not-used-in-fixture' }); }
      if (url.pathname === '/account/csrf') { calls.push(url.pathname); return json({ token: 'synthetic-browser-only' }); }
      if (url.pathname === '/api/accounts/v1/libraries/') { calls.push(url.pathname); return json([]); }
      if (url.pathname === '/account/connections/') {
        calls.push(url.pathname);
        const observedAt = await page.evaluate(() => Date.now());
        return json([{ id: device, label: 'NOVA · Navegador sintético', profileId: 'ollama-qwen3-8b-proposals',
          active: true, revoked: false, createdAt: '2026-10-29T12:00:00Z', expiresAt: '2030-01-01T00:00:00Z',
          presence: { status: phase, observedAt: new Date(observedAt).toISOString(), expiresAt: new Date(observedAt + (phase === 'STOPPED' ? 0 : 5000)).toISOString() } }]);
      }
      if (url.pathname.startsWith('/account/') || url.pathname.startsWith('/api/') || request.method() !== 'GET') {
        unexpected.push(url.pathname); await route.abort(); return;
      }
      await route.continue();
    });
    const page = await context.newPage();
    page.on('pageerror', error => browserErrors.push(error.name));
    await page.clock.install({ time: new Date('2026-10-29T12:00:00Z') });
    await page.goto(base.href); await page.getByRole('button', { name: 'Conexiones locales', exact: true }).click();
    await page.getByText('Consultando trabajos. La señal no verifica la generación del modelo.', { exact: true }).waitFor();
    await page.screenshot({ path: path.join(output, 'presence-recent.png'), fullPage: true });
    checks.push('Built account workspace renders a synthetic authenticated polling signal');
    const reads = calls.filter(value => value === '/account/connections/').length;
    await page.clock.fastForward(5001);
    await page.getByText('Sin señal reciente. La señal no verifica la generación del modelo.', { exact: true }).waitFor();
    assert.equal(calls.filter(value => value === '/account/connections/').length, reads);
    await page.getByText(/Autorización vigente/).waitFor();
    checks.push('Visual expiry updates in a real browser without another request or revoking consent');
    phase = 'STOPPED'; await page.getByRole('button', { name: 'Actualizar conexiones', exact: true }).click();
    await page.getByText('Componente detenido. La señal no verifica la generación del modelo.', { exact: true }).waitFor();
    checks.push('Explicit refresh renders a stopped observation');
    phase = 'WORKING'; await page.getByRole('button', { name: 'Actualizar conexiones', exact: true }).click();
    await page.getByText('Procesando un trabajo. La señal no verifica la generación del modelo.', { exact: true }).waitFor();
    await page.clock.fastForward(5001);
    await page.getByText('Sin señal reciente. La señal no verifica la generación del modelo.', { exact: true }).waitFor();
    await page.screenshot({ path: path.join(output, 'presence-expired.png'), fullPage: true });
    checks.push('A new working snapshot expires again without automatic polling');
    assert.equal(unexpected.length, 0); assert.equal(browserErrors.length, 0);
    result = { status: 'PASS', checks, accountBackend: 'HTTP_FIXTURE_ONLY', publicBrowserVerified: false, modelCalls: 0, workersStarted: 0,
      accountRequestsToRealService: 0, unexpectedRequests: unexpected, browserErrors, elapsedMs: Date.now() - started };
  } catch (error) {
    result = { status: 'FAIL', checks, error: String(error.message), unexpectedRequests: unexpected, browserErrors, modelCalls: 0, workersStarted: 0 };
    process.exitCode = 1;
  } finally {
    await context.close(); await browser.close();
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
  }
})().catch(error => {
  fs.writeFileSync(path.join(output, 'launch-failure.json'), JSON.stringify({ status: 'BLOCKED_BROWSER_LAUNCH', name: error.name, message: String(error.message).split('\n')[0], modelCalls: 0, workersStarted: 0 }, null, 2));
  console.error(String(error.message).split('\n')[0]); process.exitCode = 1;
});
