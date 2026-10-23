// Cold-process instrumentation contract. No model requests, browser or private DB.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const build = path.resolve(root, process.env.NOVA_TEST_BUILD || '.playwright/nova-debug/api/QuickPrompt.Api.dll');
const temp = path.join(root, '.playwright', 'debug-startup-' + stamp);
const out = path.join(root, 'docs/evidence', 'debug-startup-' + stamp);
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
const report = { syntheticOnly: true, build, checks: [], status: 'RUNNING' };
async function launch(name, enabled, rejected = false) {
  const dataPath = rejected ? path.join(root, 'debug-rejected-' + stamp + '.sqlite') : path.join(temp, name + '.sqlite');
  assert.equal(fs.existsSync(dataPath), false);
  const args = [build, '--QuickPrompt:Port=5262', '--QuickPrompt:DataPath=' + dataPath, '--Logging:LogLevel:Microsoft=Warning'];
  if (enabled) args.push('--QuickPrompt:Diagnostics:DebugPoints=true');
  const child = spawn('dotnet', args, { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  let output = ''; child.stdout.on('data', bytes => { output += bytes; }); child.stderr.on('data', bytes => { output += bytes; });
  const ended = new Promise(resolve => child.once('exit', resolve));
  try {
    const deadline = Date.now() + 30000; let ready = false;
    while (Date.now() < deadline) {
      if (child.exitCode !== null || child.signalCode !== null) break;
      try { ready = (await fetch('http://127.0.0.1:5262/health', { signal: AbortSignal.timeout(500) })).ok; } catch {}
      if (ready) break;
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    if (rejected) {
      assert.equal(ready, false, 'Capture must reject the non-QA path before serving');
      assert.ok(child.exitCode !== null || child.signalCode !== null, 'Capture guard did not stop the owned API');
      await ended; assert.notEqual(child.exitCode, 0);
      assert.match(output, /Debug point capture requires an owned/); assert.equal(fs.existsSync(dataPath), false);
    } else {
      assert.equal(ready, true, output);
      const points = output.split('\n').filter(line => line.startsWith('NOVA_DEBUG_POINT ')).map(line => JSON.parse(line.slice(17)));
      if (enabled) { assert.equal(points.length, 1); assert.equal(points[0].Boundary, 'capture.started'); assert.match(output, /points=1 observerErrors=0 listeners=True/); }
      else assert.doesNotMatch(output, /NOVA_DEBUG_/);
    }
    report.checks.push({ name, status: 'PASS' });
  } finally { if (child.exitCode === null && child.signalCode === null) child.kill(); await ended; }
}
(async () => {
  await launch('cold-start-enabled', true);
  await launch('disabled-no-stream', false);
  await launch('reject-outside-qa-without-creating-db', true, true);
  report.status = 'PASS';
})().catch(error => { report.status = 'FAIL'; report.failure = String(error.stack); process.exitCode = 1; }).finally(() => {
  fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2)); console.log(JSON.stringify({ status: report.status, evidence: out }));
});
