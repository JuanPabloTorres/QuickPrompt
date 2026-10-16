const fs = require('node:fs'), path = require('node:path');
const { build } = require('./Build-PracticalLibrary.cjs');
async function install(origin) {
  const url = new URL(origin); if (url.origin !== origin || url.protocol !== 'http:' || url.hostname !== '127.0.0.1' || !url.port) throw Error('Usa un origen local exacto con puerto.');
  async function api(route, body) { const response = await fetch(origin + '/api/v1' + route, { method: body === undefined ? 'GET' : 'POST', redirect: 'error', headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' }, body: body === undefined ? undefined : JSON.stringify(body) }); const result = await response.json(); if (!response.ok) throw Error(result.detail || 'No se pudo añadir la biblioteca.'); return result; }
  const pack = build(), before = await api('/prompts?size=1'), preview = await api('/library/import/preview', pack);
  if (preview.conflicts) throw Error('Hay conflictos: no se importó ni reemplazó nada.');
  const backup = preview.newTemplates ? await api('/library/backup', {}) : null;
  const result = await api('/library/import', { data: pack, confirmed: true });
  const repeated = await api('/library/import/preview', pack); if (repeated.newTemplates !== 0 || repeated.conflicts) throw Error('No se pudo verificar la idempotencia.');
  const after = await api('/prompts?size=1');
  return { origin, before: before.total, added: result.imported, after: after.total, duplicatesOnRepeat: repeated.duplicates, backup: backup?.fileName ?? null, status: 'PASS' };
}
if (require.main === module) install(process.argv[2] || 'http://127.0.0.1:5190').then(result => { console.log(JSON.stringify(result, null, 2)); const dir = path.resolve(__dirname, '../docs/evidence/learning'); fs.mkdirSync(dir, { recursive: true }); fs.writeFileSync(path.join(dir, 'install-' + new URL(result.origin).port + '.json'), JSON.stringify(result, null, 2)); }).catch(error => { console.error(error.message); process.exitCode = 1; });
module.exports = { install };
