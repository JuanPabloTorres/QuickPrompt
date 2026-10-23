// Reproducible semantic color checks, no third-party accessibility claims.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..'), css = fs.readFileSync(path.join(root, 'apps/web/src/design/tokens.css'), 'utf8');
const blocks = css.split(':root[data-theme="light"]');
const parse = text => Object.fromEntries([...text.matchAll(/--([\w-]+):\s*(#[a-f\d]{6});/g)].map(match => [match[1], match[2]]));
const dark = parse(blocks[0]), light = { ...dark, ...parse(blocks[1]) };
const rgb = hex => hex.slice(1).match(/../g).map(part => parseInt(part, 16));
const luminance = hex => rgb(hex).map(value => value / 255).map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4).reduce((sum, value, index) => sum + value * [.2126, .7152, .0722][index], 0);
const contrast = (a, b) => { const values = [luminance(a), luminance(b)].sort((x, y) => y - x); return (values[0] + .05) / (values[1] + .05); };
const mix = (a, b, tint) => '#' + rgb(a).map((value, index) => Math.round(value * tint + rgb(b)[index] * (1 - tint)).toString(16).padStart(2, '0')).join('');
const results = [];
for (const [theme, tokens] of Object.entries({ dark, light })) {
  const tint = Number(blocks[theme === 'dark' ? 0 : 1].match(/--state-tint:\s*(\d+)%/)[1]) / 100;
  const surfaces = ['background', 'surface', 'surface-raised', 'surface-hover'];
  for (const surface of surfaces) {
    for (const text of ['foreground', 'muted', 'accent', 'intelligence', 'success', 'warning', 'error']) results.push({ theme, foreground: text, background: surface, ratio: contrast(tokens[text], tokens[surface]), minimum: 4.5 });
    results.push({ theme, foreground: 'input-line', background: surface, ratio: contrast(tokens['input-line'], tokens[surface]), minimum: 3 });
  }
  for (const [text, background] of [['accent-foreground', 'accent'], ['error-foreground', 'error']]) results.push({ theme, foreground: text, background, ratio: contrast(tokens[text], tokens[background]), minimum: 4.5 });
  for (const accent of ['accent', 'intelligence', 'success', 'error']) for (const text of ['foreground', 'muted', accent]) results.push({ theme, foreground: text, background: accent + '-soft', ratio: contrast(tokens[text], mix(tokens[accent], tokens.surface, tint)), minimum: 4.5 });
}
const failures = results.filter(result => result.ratio < result.minimum);
const out = path.join(root, 'docs/evidence/identity-contrast.json'); fs.mkdirSync(path.dirname(out), { recursive: true }); fs.writeFileSync(out, JSON.stringify({ status: failures.length ? 'FAIL' : 'PASS', results, failures, excludes: ['disabled controls', 'decorative separators', 'manual screen-reader and full WCAG certification'] }, null, 2));
assert.deepEqual(failures, []); console.log('PASS', results.length, 'semantic color pairs', out);
