const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const packages = [];
function scan(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (!entry.isDirectory() || entry.name.startsWith('.')) continue;
    const item = path.join(directory, entry.name);
    if (entry.name.startsWith('@')) { scan(item); continue; }
    const manifest = path.join(item, 'package.json');
    if (!fs.existsSync(manifest)) continue;
    const data = JSON.parse(fs.readFileSync(manifest, 'utf8'));
    packages.push({ name: data.name, version: data.version, license: data.license ?? 'NOT_DECLARED', engines: data.engines ?? null });
    if (fs.existsSync(path.join(item, 'node_modules'))) scan(path.join(item, 'node_modules'));
  }
}
scan(path.join(root, 'apps/web/node_modules'));
const counts = {};
for (const item of packages) { const license = typeof item.license === 'string' ? item.license : JSON.stringify(item.license); counts[license] = (counts[license] ?? 0) + 1; }
const target = path.join(root, 'docs/evidence/nova/dependencies.json');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, JSON.stringify({ date: new Date().toISOString(), packageLockSha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(root, 'apps/web/package-lock.json'))).digest('hex'), installedPackageCount: packages.length, declaredLicenseCounts: counts, packages: packages.sort((a, b) => a.name.localeCompare(b.name)), caveat: 'Declared metadata inventory, not a legal opinion or exhaustive source-license audit.' }, null, 2));
console.log(JSON.stringify({ installedPackageCount: packages.length, declaredLicenseCounts: counts }));
const nuget = new Map();
for (const project of ['QuickPrompt.Api', 'QuickPrompt.Tests']) {
  const assets = JSON.parse(fs.readFileSync(path.join(root, 'backend', project, 'obj/project.assets.json'), 'utf8'));
  const cache = Object.keys(assets.packageFolders)[0];
  for (const [key, value] of Object.entries(assets.libraries)) {
    if (value.type !== 'package' || nuget.has(key)) continue;
    const [name, version] = key.split('/');
    const directory = path.join(cache, name.toLowerCase(), version);
    const manifest = fs.readdirSync(directory).find(file => file.endsWith('.nuspec'));
    const xml = manifest ? fs.readFileSync(path.join(directory, manifest), 'utf8') : '';
    const license = xml.match(/<license\s+type="([^"]+)"[^>]*>([^<]+)<\/license>/);
    nuget.set(key, { name, version, licenseType: license?.[1] ?? 'NOT_DECLARED', license: license?.[2] ?? 'NOT_DECLARED' });
  }
}
fs.writeFileSync(path.join(root, 'docs/evidence/nova/nuget-licenses.json'), JSON.stringify({ date: new Date().toISOString(), caveat: 'Nuspec declarations from existing cache; file references are not a legal audit of their contents.', packages: [...nuget.values()] }, null, 2));
console.log(`Inventoried ${nuget.size} cached NuGet package license declarations.`);
