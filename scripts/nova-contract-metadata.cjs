// Bridge from the existing C# metadata owners, never a second model/intent policy.
const path = require('node:path'), assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
function validate(value) {
  const keys = object => Object.keys(object).sort();
  const names = list => Array.isArray(list) && list.length > 0 && list.length <= 128
    && list.every(name => typeof name === 'string' && name.length > 0 && name.length <= 200)
    && new Set(list).size === list.length;
  assert.ok(value && typeof value === 'object' && !Array.isArray(value), 'Metadata must be an object.');
  assert.deepEqual(keys(value), ['contractVersion', 'defaultModel', 'enums', 'models']);
  assert.equal(value.contractVersion, 1);
  assert.ok(Array.isArray(value.models) && value.models.every(model => model && typeof model === 'object'
    && !Array.isArray(model) && JSON.stringify(keys(model)) === '["name","thinking"]' && typeof model.thinking === 'boolean'));
  assert.ok(names(value.models.map(model => model.name)), 'Unique model identities required.');
  assert.ok(value.models.some(model => model.name === value.defaultModel), 'Default model must belong to its policy.');
  assert.ok(value.enums && typeof value.enums === 'object' && !Array.isArray(value.enums));
  assert.deepEqual(keys(value.enums), ['NovaIntent', 'PreparationDecision']);
  assert.ok(Object.values(value.enums).every(names), 'Unique reflected enum names required.');
  return value;
}
function load() {
  const root = path.resolve(__dirname, '..');
  const bytes = execFileSync('dotnet', ['run', '--project', 'backend/QuickPrompt.ContractExport/QuickPrompt.ContractExport.csproj', '--no-launch-profile', '--verbosity', 'quiet', '--', '--api-metadata'],
    { cwd: root, encoding: 'utf8', timeout: 120000, maxBuffer: 1024 * 1024, windowsHide: true, env: { ...process.env, DOTNET_NOLOGO: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1' } });
  return validate(JSON.parse(bytes));
}
module.exports = { load, validate };
