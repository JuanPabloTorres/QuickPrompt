// Static layout contracts only: no inference, installation, writes or mobile build.
const fs = require('node:fs'), path = require('node:path');
const root = path.resolve(__dirname, '..'), failures = [], checks = [];
const read = name => fs.readFileSync(path.join(root, name), 'utf8').replace(/^\uFEFF/, '');
function check(label, fn) { try { fn(); checks.push({ label, status: 'PASS' }); } catch (e) { failures.push(label + ': ' + e.message); checks.push({ label, status: 'FAIL' }); } }
function requireFile(name) { if (!fs.statSync(path.join(root, name)).isFile()) throw Error('Not a file: ' + name); }
check('Application and agent owners have concrete entry points', () => {
  for (const file of ['apps/mobile/QuickPrompt.csproj', 'apps/web/package.json', 'backend/QuickPrompt.Api/Program.cs',
    'agents/nova-core/nova_worker.py', 'agents/nova-core/start-nova.ps1']) requireFile(file);
});
check('Mobile and web solutions reference existing project files', () => {
  for (const solution of ['QuickPrompt.sln', 'QuickPrompt.Nova.sln', 'src/QuickPrompt.Web.sln', 'apps/mobile/tests/portable/QuickPrompt.Tests.sln'])
    for (const match of read(solution).matchAll(/=\s*"[^"]+",\s*"([^"]+\.csproj)"/g))
      requireFile(path.join(path.dirname(solution), match[1].replaceAll('\\', '/')));
});
check('Mobile tests point to the relocated application and are excluded from its source globs', () => {
  if (!read('apps/mobile/QuickPrompt.csproj').includes('$(DefaultItemExcludes);tests\\**')) throw Error('Nested tests can enter mobile compilation');
  for (const test of ['windows', 'portable']) {
    const project = `apps/mobile/tests/${test}/QuickPrompt.Tests.csproj`;
    for (const match of read(project).matchAll(/ProjectReference Include="([^"]+)"/g))
      requireFile(path.join(path.dirname(project), match[1].replaceAll('\\', '/')));
  }
});
check('Shared domain linked source directories are nonempty', () => {
  for (const match of read('src/QuickPrompt.Domain/QuickPrompt.Domain.csproj').matchAll(/Compile Include="([^"]+\/\*\.cs)"/g)) {
    const directory = path.resolve(root, 'src/QuickPrompt.Domain', path.dirname(match[1]));
    if (!fs.readdirSync(directory).some(name => name.endsWith('.cs'))) throw Error('Empty linked source glob: ' + match[1]);
  }
});
check('Core default worker and editor tasks use the new agent owner', () => {
  if (!read('backend/QuickPrompt.Api/NovaAgentSettings.cs').includes('agents/nova-core/nova_worker.py')) throw Error('Stale worker default');
  for (const task of JSON.parse(read('.vscode/tasks.json')).tasks)
    for (const arg of task.args ?? []) if (arg.includes('integrations/adaptive_core')) throw Error('Stale task: ' + task.label);
});
check('Old destructive restructuring script is not executable at its previous entry point', () => {
  if (fs.existsSync(path.join(root, 'scripts/Restructure-Folders.ps1'))) throw Error('Obsolete script is still active');
  requireFile('scripts/archive/Restructure-Folders.ps1.disabled');
});
check('Relocated mobile secrets and local agent configuration stay ignored', () => {
  const ignored = require('node:child_process').execFileSync('git', ['check-ignore', 'apps/mobile/Resources/Secrets/synthetic-check.json', 'agents/nova-core/config.local.json'], { cwd: root, encoding: 'utf8', stdio: 'pipe' }).trim().split(/\r?\n/);
  if (ignored.length !== 2) throw Error('One relocated private path is no longer ignored');
});
console.log(JSON.stringify({ status: failures.length ? 'FAIL' : 'PASS', scope: 'STATIC_LAYOUT_ONLY', checks, errors: failures }, null, 2));
process.exitCode = failures.length ? 1 : 0;
