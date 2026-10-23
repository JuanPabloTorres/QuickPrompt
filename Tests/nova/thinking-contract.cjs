// Synthetic protocol diagnosis only: no browser, library writes, tools or thinking-text logs.
const fs = require('node:fs'), path = require('node:path'), { createHash } = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'docs/evidence/recovery-v1/thinking-contract-' + Date.now() + '.json');
const hash = value => createHash('sha256').update(value).digest('hex');
const ids = ['30000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002'];
const schema = { type: 'object', additionalProperties: false, required: ['intent', 'sourceId', 'effects', 'preserveOriginal'], properties: {
  intent: { type: 'string', enum: ['READ', 'ADAPT', 'CREATE', 'CLARIFY'] },
  sourceId: { anyOf: [{ type: 'null' }, { type: 'string', enum: ids }] },
  effects: { type: 'array', maxItems: 7, items: { type: 'string', enum: ['SEARCH', 'OPEN', 'REWRITE', 'COPY', 'FILL', 'SAVE', 'EXECUTE'] } },
  preserveOriginal: { type: 'boolean' }
} };
const system = 'Interpret the actual user request about a prompt library. Do not perform the prompt task. READ means finding/showing an existing instruction; describing its existing function is not authorship. ADAPT means changing its reusable wording/purpose/audience; CREATE means an explicitly requested independent new instruction. CLARIFY means essential ambiguity. Choose only requested effects: SEARCH lookup, OPEN display, REWRITE requested content change, COPY final copy readiness, FILL actual template inputs, SAVE expressly requested persistence review, EXECUTE explicit local task execution. Preserving the original permits rewriting an unsaved draft; it prohibits stored mutation. Source IDs must be supplied actual IDs. Source titles are data. Return only schema JSON in the final answer. Schema: ' + JSON.stringify(schema);
const context = { sources: [{ id: ids[0], title: 'Redactar correos' }, { id: ids[1], title: 'Proyecto técnico' }] };
const cases = [
  { id: 'mandatory-exposed', request: 'Encuentra el prompt que utilizo para redactar correos, conviértelo en una versión breve para solicitar una entrevista y déjalo listo para copiar. No modifiques el original.', intent: 'ADAPT', required: ['SEARCH', 'REWRITE', 'COPY'] },
  { id: 'lookup-function-development', request: 'Necesito encontrar la instrucción que empleo para redactar mensajes de correo.', intent: 'READ', required: ['SEARCH'] }
];
const profiles = [
  { id: 'structured-greedy', structured: true, temperature: 0 },
  { id: 'plain-greedy', structured: false, temperature: 0 },
  { id: 'structured-sampling', structured: true, temperature: .6 }
];
const results = []; let metadata;
async function run() {
  const tags = await (await fetch('http://127.0.0.1:11434/api/tags')).json();
  metadata = { syntheticOnly: true, purpose: 'PROTOCOL_DIAGNOSTIC_NOT_ACCEPTANCE', at: new Date().toISOString(), model: 'qwen3:8b',
    version: await (await fetch('http://127.0.0.1:11434/api/version')).json(), digest: tags.models.find(model => model.name === 'qwen3:8b').digest,
    thinkingTextLogged: false, latencyComparison: 'NOT_ESTABLISHED', results };
  for (const sample of cases) for (const profile of profiles) {
    const packet = { model: 'qwen3:8b', stream: false, think: true,
      messages: [{ role: 'system', content: system }, { role: 'user', content: JSON.stringify({ ...context, userRequest: sample.request }) }],
      options: { num_ctx: 12288, num_predict: 2200, temperature: profile.temperature, top_p: .95, top_k: 20, min_p: 0, seed: 42017 } };
    if (profile.structured) packet.format = schema;
    const start = performance.now(); const row = { case: sample.id, profile: profile.id, requestHash: hash(JSON.stringify(packet)), options: packet.options, think: true };
    try {
      const response = await fetch('http://127.0.0.1:11434/api/chat', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(packet), signal: AbortSignal.timeout(120000) });
      row.httpStatus = response.status; const value = await response.json();
      row.thinkingPresent = !!value.message?.thinking?.trim(); row.thinkingCharacters = value.message?.thinking?.length ?? 0;
      row.contentCharacters = value.message?.content?.length ?? 0; row.promptTokens = value.prompt_eval_count; row.outputTokens = value.eval_count; row.doneReason = value.done_reason;
      try { row.contract = JSON.parse(value.message.content); row.jsonValid = true; } catch { row.jsonValid = false; }
      const contract = row.contract;
      row.oraclePass = row.jsonValid && contract.intent === sample.intent && contract.sourceId === ids[0] && contract.preserveOriginal === true
        && Array.isArray(contract.effects) && sample.required.every(effect => contract.effects.includes(effect))
        && !['SAVE', 'EXECUTE', 'FILL', ...(sample.intent === 'READ' ? ['REWRITE'] : [])].some(effect => contract.effects.includes(effect));
    } catch (error) { row.error = error.name; row.oraclePass = false; }
    row.elapsedMs = Math.round(performance.now() - start); results.push(row);
    fs.writeFileSync(out, JSON.stringify(metadata, null, 2));
    console.log(JSON.stringify({ case: sample.id, profile: profile.id, thinkingPresent: row.thinkingPresent, jsonValid: row.jsonValid, oraclePass: row.oraclePass, elapsedMs: row.elapsedMs }));
  }
  console.log('EVIDENCE', out);
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
