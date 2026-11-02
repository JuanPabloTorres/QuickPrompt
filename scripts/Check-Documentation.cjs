// Current documentation only: local file links, not content, remote URLs or anchors.
const fs = require('node:fs'), path = require('node:path');
const currentDocuments = [
  'README.md', 'AGENTS.md', 'apps/web/AGENTS.md', 'docs/README.md',
  'docs/ARCHITECTURE.md', 'docs/COMO_USAR_QUICKPROMPT.md', 'docs/nova/README.md',
  'docs/development/DEVELOPMENT.md', 'docs/development/UI_COMPONENTS.md',
  'docs/archive/README.md', 'docs/releases/NOVA_1.0.0.md', 'docs/releases/NOVA_1.0.0_R1.md',
  '.agents/skills/universal-modular-architecture/SKILL.md', 'docs/releases/NOVA_1.0.0_R2.md',
  'docs/development/MCP.md',
  'docs/development/SAAS_DELIVERY.md',
  'docs/development/GO_TO_MARKET.md',
  'docs/development/COMPANION.md',
];

function checkDocuments(root, documents = currentDocuments) {
  root = path.resolve(root);
  const errors = [];
  for (const document of documents) {
    const file = path.resolve(root, document);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) {
      errors.push({ document, reason: 'Document missing or outside repository' }); continue;
    }
    let fenced = false;
    for (const [index, line] of fs.readFileSync(file, 'utf8').split(/\r?\n/).entries()) {
      if (/^\s*```/.test(line)) { fenced = !fenced; continue; }
      if (fenced) continue;
      for (const match of line.matchAll(/\]\(([^)]+)\)/g)) {
        const target = match[1];
        if (!target || target.startsWith('#') || /^[a-z][a-z0-9+.-]*:/i.test(target)) continue;
        let resolved;
        try { resolved = path.resolve(path.dirname(file), decodeURIComponent(target.split('#')[0])); }
        catch { errors.push({ document, line: index + 1, target, reason: 'Invalid encoded path' }); continue; }
        if (!resolved.startsWith(root + path.sep) || !fs.existsSync(resolved))
          errors.push({ document, line: index + 1, target, reason: 'Local target missing or outside repository' });
      }
    }
  }
  return { status: errors.length ? 'FAIL' : 'PASS', scope: 'CURRENT_LOCAL_FILE_LINKS_ONLY', documents: documents.length, errors };
}

if (require.main === module) {
  const result = checkDocuments(path.resolve(__dirname, '..'));
  console.log(JSON.stringify(result, null, 2)); process.exitCode = result.errors.length ? 1 : 0;
}
module.exports = { checkDocuments };
