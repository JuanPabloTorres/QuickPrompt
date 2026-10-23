const { test } = require('node:test'), assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { checkDocuments } = require('../../scripts/Check-Documentation.cjs');

function fixture(t, source) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'quickprompt-doc-links-'));
  assert.equal(path.dirname(root), path.resolve(os.tmpdir()));
  assert.ok(path.basename(root).startsWith('quickprompt-doc-links-'));
  t.after(() => fs.rmSync(root, { recursive: true }));
  fs.mkdirSync(path.join(root, 'guide')); fs.writeFileSync(path.join(root, 'guide/index.md'), source);
  fs.writeFileSync(path.join(root, 'README.md'), '# Root'); return root;
}
test('resolves relative files and ignores remote URLs, fragments and code examples', t => {
  const root = fixture(t, '[Root](../README.md#section) [Web](https://example.test/) [Here](#here)\n```md\n[Example](missing.md)\n```');
  assert.equal(checkDocuments(root, ['guide/index.md']).status, 'PASS');
});
test('reports a missing local target instead of accepting a document that merely exists', t => {
  const root = fixture(t, '[Missing](missing.md)');
  const result = checkDocuments(root, ['guide/index.md']);
  assert.equal(result.status, 'FAIL'); assert.equal(result.errors[0].line, 1);
  assert.equal(result.errors[0].target, 'missing.md');
});
test('rejects links outside the selected repository and absent input documents', t => {
  const root = fixture(t, '[Outside](../../outside.md)');
  const result = checkDocuments(root, ['guide/index.md', 'absent.md']);
  assert.equal(result.status, 'FAIL'); assert.equal(result.errors.length, 2);
});
