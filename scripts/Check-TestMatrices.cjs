// Manifest/traceability checks only; never launches unrelated test workflows.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const value = name => args.includes(name) ? args[args.indexOf(name) + 1] : null;
assert.ok(args.every((arg, index) => ['--area', '--block', '--write-report'].includes(arg)
  || ['--area', '--block'].includes(args[index - 1])), 'Unknown option');
const selected = value('--area');
assert.ok(selected === null || ['backend', 'frontend', 'agent'].includes(selected), 'Choose backend, frontend or agent');
assert.ok(!value('--block') || selected, '--block requires --area');
const canonical = JSON.parse(fs.readFileSync(path.join(root, 'docs/nova/VERSION_CLOSURE_MATRIX.json'), 'utf8'));
const requirements = new Map(canonical.rows.map(row => [row[0], Object.fromEntries(canonical.columns.map((column, i) => [column, row[i]]))]));
for (const area of selected ? [selected] : ['backend', 'frontend', 'agent']) {
  const base = 'docs/nova/testing/' + area.toUpperCase() + '_TEST_MATRIX';
  const matrix = JSON.parse(fs.readFileSync(path.join(root, base + '.json'), 'utf8'));
  assert.equal(matrix.area, area);
  assert.equal(new Set(matrix.blocks.map(block => block.id)).size, matrix.blocks.length, 'Duplicate block');
  const blocks = matrix.blocks.filter(block => !value('--block') || block.id === value('--block'));
  assert.ok(blocks.length > 0, 'Unknown block in selected area');
  for (const block of blocks) {
    assert.ok(block.owners.length && block.requirements.length && block.acceptance && block.selection);
    for (const owner of block.owners) {
      const resolved = path.resolve(root, owner);
      assert.ok(resolved.startsWith(root + path.sep) && fs.existsSync(resolved), 'Missing current capability owner ' + owner);
    }
    assert.ok(matrix.statuses.includes(block.status), 'Unknown evidence status');
    for (const id of block.requirements) assert.ok(requirements.has(id), 'Unknown canonical requirement ' + id);
    for (const evidence of block.evidence) {
      const resolved = path.resolve(root, evidence);
      assert.ok(resolved.startsWith(root + path.sep) && fs.existsSync(resolved), 'Missing evidence ' + evidence);
    }
  }
  console.log(JSON.stringify({ area, manifestValid: true, checkedBlocks: blocks.map(block => block.id),
    workflowsExecuted: 0, releaseAccepted: false }));
  if (args.includes('--write-report')) {
    assert.ok(!value('--block'), 'Do not replace a full matrix report with a selected block');
    const text = ['# Matriz de pruebas: ' + area, '', matrix.scope, '', matrix.rule,
      '', 'Este documento valida trazabilidad; no ejecuta las pruebas ni convierte evidencia histórica en aceptación actual.',
      '', '| Bloque | Estado de evidencia | Requisitos canónicos | Tipo |', '|---|---|---|---|',
      ...blocks.map(block => `| ${block.id} | ${block.status} | ${block.requirements.join(', ')} | ${block.kind} |`), ''];
    for (const block of blocks) text.push('## ' + block.id, '', '**Propietarios:** ' + block.owners.map(owner => '`' + owner + '`').join(', '),
      '', '**Selección:** ' + (typeof block.selection === 'string' ? block.selection : '`' + JSON.stringify(block.selection) + '`'),
      '', '**Aceptación:** ' + block.acceptance, '', '**Evidencia:** ' + block.evidence.map(file => '[' + path.basename(file) + '](../../../' + file + ')').join(', '),
      '', '**Criterios originales:**', '', ...block.requirements.map(id => '- ' + id + ': ' + requirements.get(id).closeWhen), '');
    fs.writeFileSync(path.join(root, base + '.md'), text.join('\n') + '\n');
  }
}
