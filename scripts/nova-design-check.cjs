const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const css = fs.readFileSync(path.join(root, 'apps/web/src/design/tokens.css'), 'utf8');
function luminance(hex) { const channels = hex.match(/[a-f\d]{2}/gi).map(value => parseInt(value, 16) / 255).map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4); return channels[0] * .2126 + channels[1] * .7152 + channels[2] * .0722; }
function ratio(a, b) { const x = luminance(a), y = luminance(b); return (Math.max(x, y) + .05) / (Math.min(x, y) + .05); }
const rows = [];
for (const [index, block] of [...css.matchAll(/:root[^\{]*\{([^}]+)\}/g)].entries()) {
  const tokens = Object.fromEntries([...block[1].matchAll(/--([\w-]+):\s*(#[a-f\d]{6})\s*;/gi)].map(match => [match[1], match[2]]));
  for (const surface of ['background', 'surface']) for (const role of ['foreground', 'muted', 'accent', 'error', 'input-line']) {
    const measured = ratio(tokens[role], tokens[surface]); const minimum = role === 'input-line' ? 3 : 4.5;
    rows.push({ theme: index ? 'light' : 'dark', role, surface, ratio: measured, minimum, status: measured >= minimum ? 'EXECUTED_PASS' : 'EXECUTED_FAIL' });
  }
  const measured = ratio(tokens['accent-foreground'], tokens.accent);
  rows.push({ theme: index ? 'light' : 'dark', role: 'primary button text', surface: 'accent', ratio: measured, minimum: 4.5, status: measured >= 4.5 ? 'EXECUTED_PASS' : 'EXECUTED_FAIL' });
}
fs.writeFileSync(path.join(root, 'docs/evidence/nova/contrast.json'), JSON.stringify({ caveat: 'Token contrast only. Not a WCAG certification or an audit of every composed state.', rows }, null, 2));
console.log(`${rows.filter(row => row.status === 'EXECUTED_PASS').length}/${rows.length} token contrast checks passed.`);
if (rows.some(row => row.status === 'EXECUTED_FAIL')) process.exitCode = 1;
