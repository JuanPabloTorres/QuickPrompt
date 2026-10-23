// Mechanical verification only. The closure matrix still owns release requirements.
const fs = require('node:fs'), path = require('node:path'), { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const root = path.resolve(__dirname, '..');
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const options = process.argv.slice(2);
if (options.length !== 0 && (options.length !== 2 || options[0] !== '--out')) throw Error('Usage: node scripts/Verify-NovaCandidate.cjs [--out new-workspace-directory]');
const out = path.resolve(root, options[1] || '.playwright/nova-candidate-' + Date.now());
const relative = path.relative(root, out);
if (!relative || relative.startsWith('..') || path.isAbsolute(relative) || fs.existsSync(out)) throw Error('Use a NEW directory inside the workspace; nothing will be overwritten');
// Refuse junctions/symlinks in existing ancestors before creating any artifact.
for (let parent = path.dirname(out); parent !== root; parent = path.dirname(parent)) {
  if (fs.existsSync(parent) && fs.lstatSync(parent).isSymbolicLink()) throw Error('Output ancestor must not be a symlink or junction');
}
fs.mkdirSync(out, { recursive: true });
const reportFile = path.join(out, 'verification.json');
const report = { status: 'RUNNING', acceptanceComplete: false, startedAt: new Date().toISOString(),
  scope: 'BUILD_TESTS_AND_MATRIX_GATE_NOT_AGENT_OR_RELEASE_ACCEPTANCE', configuration: 'Debug', commands: [], checks: [] };
const save = () => fs.writeFileSync(reportFile, JSON.stringify(report, null, 2));
let active;
async function command(name, executable, args, cwd = root, acceptedCodes = [0]) {
  const logFile = path.join(out, name + '.log');
  const row = { name, executable, args, cwd: path.relative(root, cwd) || '.', status: 'RUNNING', log: path.relative(root, logFile) };
  report.commands.push(row); save(); const started = Date.now();
  const stream = fs.createWriteStream(logFile, { flags: 'wx' }); let buffer = '';
  try {
    const code = await new Promise((resolve, reject) => {
      active = spawn(executable, args, { cwd, windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'] });
      const timer = setTimeout(() => { terminate(); reject(Error(name + ' exceeded five-minute limit')); }, 300000);
      const collect = data => { stream.write(data); buffer += data.toString(); };
      active.stdout.on('data', collect); active.stderr.on('data', collect);
      active.once('error', error => { clearTimeout(timer); reject(error); });
      active.once('exit', (code, signal) => { clearTimeout(timer); resolve(signal ? -1 : code); });
    });
    row.exitCode = code; row.status = acceptedCodes.includes(code) ? 'PASS_COMMAND_ONLY' : 'FAIL';
    if (!acceptedCodes.includes(code)) throw Error(name + ' failed; see ' + row.log);
    return buffer;
  } catch (error) { row.status = 'FAIL'; row.error = String(error); throw error; }
  finally { active = undefined; row.elapsedMs = Date.now() - started; await new Promise(resolve => stream.end(resolve)); save(); console.log(name, row.status, row.elapsedMs + 'ms'); }
}
function terminate() {
  if (!active || active.exitCode !== null) return;
  if (process.platform === 'win32') spawn('taskkill.exe', ['/PID', String(active.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
  else active.kill('SIGTERM');
}
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { terminate(); report.status = 'INTERRUPTED'; save(); process.exit(1); });
async function sourceSnapshot(label) {
  const output = await command(label, 'git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', 'backend', 'src', 'apps/web', 'apps/mobile', 'agents', 'contracts', 'scripts', 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.config']);
  return Object.fromEntries([...new Set(output.split('\0').filter(Boolean))].sort()
    .filter(file => !/(^|\/)(bin|obj|node_modules|dist)(\/|$)/.test(file) && !/(^|\/)\.env(?:\.|$)|\.(?:sqlite|db)(?:-|$)/i.test(file))
    .map(file => [file, fs.existsSync(path.join(root, file)) ? hash(path.join(root, file)) : null]));
}
function check(label, condition, evidence) {
  report.checks.push({ label, status: condition ? 'PASS' : 'FAIL', evidence }); save();
  if (!condition) throw Error(label);
}
function trx(label, file) {
  const xml = fs.readFileSync(file, 'utf8'), counters = xml.match(/<Counters\b([^>]+)>/);
  if (!counters) throw Error('Missing TRX counters: ' + file);
  const values = Object.fromEntries([...counters[1].matchAll(/(\w+)="(\d+)"/g)].map(match => [match[1], Number(match[2])]));
  check(label, values.total > 0 && values.executed === values.total && values.passed === values.total && values.failed === 0 && values.notExecuted === 0,
    { file: path.relative(root, file), sha256: hash(file), counters: values });
}
(async () => {
  save(); report.sourceBefore = await sourceSnapshot('source-before'); save();
  await command('backend-tests', 'dotnet', ['test', 'backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj', '--no-restore', '-c', 'Debug', '--logger', 'trx;LogFileName=backend.trx', '--results-directory', out]);
  trx('Backend complete, no skipped tests', path.join(out, 'backend.trx'));
  await command('legacy-tests', 'dotnet', ['test', 'src/QuickPrompt.Tests/QuickPrompt.Tests.csproj', '--no-restore', '-c', 'Debug', '--logger', 'trx;LogFileName=legacy.trx', '--results-directory', out]);
  trx('Legacy complete, no skipped tests', path.join(out, 'legacy.trx'));
  const vitest = path.join(root, 'apps/web/node_modules/vitest/vitest.mjs');
  await command('react-tests', process.execPath, [vitest, 'run', '--maxWorkers=1', '--reporter=json', '--outputFile=' + path.join(out, 'react.json')], path.join(root, 'apps/web'));
  const react = JSON.parse(fs.readFileSync(path.join(out, 'react.json')));
  check('React complete, no skipped tests', react.success && react.numTotalTests > 0 && react.numPassedTests === react.numTotalTests && react.numFailedTests === 0 && react.numPendingTests === 0,
    { file: path.relative(root, path.join(out, 'react.json')), sha256: hash(path.join(out, 'react.json')), total: react.numTotalTests, passed: react.numPassedTests, failed: react.numFailedTests, pending: react.numPendingTests });
  for (const [name, project] of [['api', 'QuickPrompt.Api'], ['evaluation', 'QuickPrompt.Evaluation']])
    await command('build-' + name, 'dotnet', ['build', 'backend/' + project + '/' + project + '.csproj', '--no-restore', '-c', 'Debug', '-o', path.join(out, name)]);
  await command('build-web-typescript', process.execPath, [path.join(root, 'apps/web/node_modules/typescript/bin/tsc'), '--noEmit'], path.join(root, 'apps/web'));
  await command('build-web-vite', process.execPath, [path.join(root, 'apps/web/node_modules/vite/bin/vite.js'), 'build', '--outDir', path.join(out, 'web')], path.join(root, 'apps/web'));
  const shared = ['QuickPrompt.Domain.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll'];
  report.sharedAssemblies = shared.map(file => ({ file, api: hash(path.join(out, 'api', file)), evaluation: hash(path.join(out, 'evaluation', file)) }));
  check('API/evaluation shared assemblies identical', report.sharedAssemblies.every(item => item.api === item.evaluation), report.sharedAssemblies);
  report.webHashes = Object.fromEntries(fs.readdirSync(path.join(out, 'web/assets')).filter(file => /\.(js|css)$/.test(file)).map(file => [file, hash(path.join(out, 'web/assets', file))]));
  report.sourceAfter = await sourceSnapshot('source-after');
  check('Sources unchanged throughout verification', JSON.stringify(report.sourceBefore) === JSON.stringify(report.sourceAfter), { sourceCount: Object.keys(report.sourceBefore).length });
  const raw = await command('closure-matrix', process.execPath, ['scripts/Check-VersionClosure.cjs'], root, [0, 2]);
  const gate = JSON.parse(raw); report.closureGate = gate;
  check('Closure matrix structurally valid', gate.matrixValid === true, { matrixValid: gate.matrixValid, errors: gate.errors });
  check('Closure summary contract valid', Number.isInteger(gate.open) && gate.open >= 0 && Number.isInteger(gate.requirements) && gate.requirements > 0 && gate.open <= gate.requirements && ['NO_CERRADA', 'LISTA_PARA_REVISION', 'CERRADA'].includes(gate.status), { status: gate.status, open: gate.open, requirements: gate.requirements });
  report.status = gate.open > 0 || gate.status === 'NO_CERRADA' ? 'CHECKS_PASSED_VERSION_OPEN' : 'READY_FOR_REVIEW';
  report.finishedAt = new Date().toISOString(); save();
  process.exitCode = report.status === 'CHECKS_PASSED_VERSION_OPEN' ? 2 : 0;
  console.log(report.status, reportFile);
})().catch(error => { report.status = 'FAIL'; report.error = String(error); report.finishedAt = new Date().toISOString(); save(); console.error(String(error)); process.exitCode = 1; });
