const fs = require('node:fs');
const path = require('node:path');
const string = { type: 'string' }, integer = { type: 'integer' }, boolean = { type: 'boolean' }, uuid = { type: 'string', format: 'uuid' };
const ref = name => ({ $ref: '#/components/schemas/' + name });
const object = (properties, required = Object.keys(properties)) => ({ type: 'object', required, properties });
const dictionary = { type: 'object', additionalProperties: string };
const promptInput = object({ title: { ...string, maxLength: 160 }, templateText: { ...string, maxLength: 32000 }, description: { ...string, maxLength: 2000 }, categoryId: integer, revision: integer, defaults: dictionary }, ['title', 'templateText']);
const schemas = {
  PromptInput: promptInput,
  PromptUpdateInput: { allOf: [ref('PromptInput'), { type: 'object', required: ['revision'], properties: { revision: { ...integer, minimum: 1 } } }] },
  Prompt: object({ id: uuid, title: string, templateText: string, description: string, categoryId: integer, isFavorite: boolean, isArchived: boolean, revision: integer, createdAt: { ...string, format: 'date-time' }, updatedAt: { ...string, format: 'date-time' }, lastUsedAt: { ...string, format: 'date-time', nullable: true }, uses: integer, variableNames: { type: 'array', items: string }, defaults: dictionary }),
  PromptPage: object({ items: { type: 'array', items: ref('Prompt') }, total: integer }),
  Revision: object({ revision: { ...integer, minimum: 1 } }),
  Confirmation: object({ revision: { ...integer, minimum: 1 }, confirmed: boolean }),
  CompileInput: object({ revision: integer, values: dictionary }),
  CompileResult: object({ text: string, templateRevision: integer }),
  UsageInput: object({ revision: integer, values: dictionary, operationId: uuid, expectedText: { ...string, maxLength: 64000 } }),
  InspectInput: object({ templateText: { ...string, maxLength: 32000 } }),
  GroupInput: object({ name: { ...string, maxLength: 80 }, revision: integer }, ['name']),
  GroupUpdateInput: object({ name: { ...string, maxLength: 80 }, revision: { ...integer, minimum: 1 } }),
  Membership: object({ groupId: uuid, revision: integer, member: boolean }),
  ImportInput: object({ data: {}, confirmed: boolean, expectedSnapshot: string }, ['data', 'confirmed']),
  NovaRequest: object({ message: { ...string, maxLength: 1000 }, model: { type: 'string', enum: ['qwen3:8b', 'qwen2.5:7b-instruct'] }, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true } }, ['message']),
  ProposalInput: object({ intent: { type: 'string', enum: ['create', 'improve'] }, context: { ...string, maxLength: 4000 }, model: string, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true } }, ['intent', 'context']),
  LocalCommandInput: object({ message: { ...string, maxLength: 16000 } }),
  LocalCommand: object({ kind: { ...string, enum: ['NONE', 'OPEN', 'EDIT', 'UPDATE', 'LIBRARY', 'REFRESH', 'FAVORITES', 'RECENT', 'ARCHIVE'] }, candidates: { type: 'array', maxItems: 6, items: ref('Prompt') }, instruction: { ...string, maxLength: 4000 }, message: string }),
  NovaAction: object({ contractVersion: { type: 'integer', enum: [1] }, id: uuid, intent: { type: 'string', enum: ['SEARCH', 'OPEN', 'CREATE_DRAFT', 'IMPROVE_DRAFT', 'FILL_VARIABLES', 'COMPILE', 'RUN_LOCAL', 'SELECT_PROVIDER', 'HELP', 'UNKNOWN'] }, arguments: string, sourcePromptIds: { type: 'array', items: uuid }, requiresConfirmation: boolean, proposedEffect: string, validationStatus: string, correlationId: string, candidates: { type: 'array', maxItems: 3, items: ref('Prompt') }, message: string }),
  ExecuteInput: object({ promptId: uuid, revision: integer, values: dictionary, model: string, confirmed: boolean, expectedText: string }),
  TransferInput: object({ promptId: uuid, revision: integer, values: dictionary, providerId: { type: 'string', enum: ['chatgpt', 'claude', 'gemini', 'copilot', 'perplexity'] }, confirmed: boolean }),
  PrepareInput: object({ operationId: uuid, message: { ...string, maxLength: 16000 }, model: { ...string, enum: ['qwen3:8b', 'qwen2.5:7b-instruct'] }, rememberDraft: boolean, sourcePromptId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, values: dictionary }, ['operationId', 'message']),
  PreparedDraft: object({ id: uuid, state: { ...string, enum: ['READY', 'NEEDS_INFORMATION'] }, intent: string, decision: { ...string, enum: ['REUSE_EXISTING', 'ADAPT_EXISTING', 'CREATE_NEW', 'ASK_CLARIFICATION'] }, title: string, description: string, templateText: string, compiledText: { ...string, nullable: true }, variables: { type: 'array', items: object({ name: string, required: boolean }) }, values: { type: 'array', items: object({ name: string, value: string, valueSource: { ...string, enum: ['USER_INPUT', 'REQUEST_QUOTE', 'REQUEST_LABEL', 'TEMPLATE_DEFAULT'] }, validationStatus: { ...string, enum: ['VALIDATED'] } }) }, missingVariables: { type: 'array', items: string }, question: string, reason: string, candidates: { type: 'array', maxItems: 6, items: object({ prompt: ref('Prompt'), score: { type: 'number' }, reason: string }) }, sourceId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, categoryId: integer, remembered: boolean, createdAt: { ...string, format: 'date-time' } }),
  Problem: object({ title: string, status: integer, detail: string, correlationId: string }, ['title', 'status']),
};
const paths = {};
function operation(method, route, summary, input, output, status = 200) {
  const parameters = [...route.matchAll(/\{([^}]+)\}/g)].map(match => ({ name: match[1], in: 'path', required: true, schema: uuid }));
  const success = { description: summary };
  if (output) success.content = { 'application/json': { schema: typeof output === 'string' ? ref(output) : output } };
  const responses = { [status]: success };
  for (const code of [400, 403, 404, 409, 429, 503]) responses[code] = { description: 'Validation, origin, missing resource, conflict, busy or unavailable service. A security rejection can have an empty body.', content: { 'application/problem+json': { schema: ref('Problem') } } };
  const item = { summary, operationId: method + route.replace(/[^a-zA-Z0-9]/g, '_'), parameters, responses };
  if (method !== 'get') item.security = [{ LocalClientHeader: [] }];
  if (input) item.requestBody = { required: true, content: { 'application/json': { schema: typeof input === 'string' ? ref(input) : input } } };
  (paths[route] ??= {})[method] = item;
  return item;
}
const prefix = '/api/v1';
const op = (method, route, ...args) => operation(method, prefix + route, ...args);
operation('get', '/health', 'Check isolated local database integrity', null, object({ status: string, schemaVersion: integer }));
const list = op('get', '/prompts', 'Search saved prompts without an LLM', null, 'PromptPage');
list.parameters.push(...['search', 'sort', 'groupId'].map(name => ({ name, in: 'query', schema: string })), ...['favorites', 'recent', 'archived'].map(name => ({ name, in: 'query', schema: boolean })), ...['page', 'size'].map(name => ({ name, in: 'query', schema: integer })));
op('post', '/prompts', 'Create a prompt; server owns identity and revision', 'PromptInput', 'Prompt', 201);
op('get', '/prompts/{id}', 'Read an existing prompt', null, 'Prompt');
op('put', '/prompts/{id}', 'Update content with a mandatory current revision', 'PromptUpdateInput', 'Prompt');
op('delete', '/prompts/{id}', 'Delete after explicit confirmation; preserve compiled history', 'Confirmation', null, 204);
for (const action of ['favorite', 'archive', 'restore']) op('post', `/prompts/{id}/${action}`, 'Change prompt state with optimistic concurrency', 'Revision', 'Prompt');
op('post', '/prompts/{id}/duplicate', 'Duplicate a stored template without copying its history', null, 'Prompt');
op('post', '/prompts/{id}/compile', 'Read-only deterministic compile; no usage is recorded', 'CompileInput', 'CompileResult');
op('post', '/prompts/{id}/usage', 'Acknowledge completed use with a durable idempotency receipt and exact snapshot', 'UsageInput', null, 204);
op('get', '/prompts/{id}/versions', 'Read preserved legacy-compatible revisions', null, { type: 'array', items: { type: 'object' } });
op('post', '/templates/inspect', 'Detect legacy placeholders without persistence', 'InspectInput', object({ variableNames: { type: 'array', items: string } }));
op('post', '/templates/toggle-variable', 'Create a variable from a selection or restore literal text from a selected variable. Pure operation; never saves a prompt.', object({ templateText: { ...string, maxLength: 32000 }, start: integer, end: integer, originalText: { ...string, nullable: true, maxLength: 32000 } }, ['templateText', 'start', 'end']), object({ templateText: string, variableNames: { type: 'array', items: string }, start: integer, end: integer, action: { ...string, enum: ['created', 'removed'] }, name: string, originalText: { ...string, nullable: true } }));
op('get', '/groups', 'List collections and membership counts', null, { type: 'array', items: { type: 'object' } });
op('post', '/groups', 'Create a collection explicitly', 'GroupInput', { type: 'object' });
op('put', '/groups/{id}', 'Rename collection preserving its other metadata; revision required', 'GroupUpdateInput', { type: 'object' });
op('delete', '/groups/{id}', 'Delete confirmed collection without deleting prompts', 'Confirmation', null, 204);
op('get', '/prompts/{id}/groups', 'Read collection memberships', null, { type: 'array', items: uuid });
op('post', '/prompts/{id}/groups', 'Assign or remove collection membership', 'Membership', null, 204);
op('get', '/library/export', 'Export complete compatible Archive v2 (private)', null, { type: 'object' });
op('post', '/library/import/preview', 'Validate selected JSON without writing', {}, { type: 'object' });
op('post', '/library/import', 'Confirmed additive import; no overwrite', 'ImportInput', object({ imported: integer }));
op('post', '/library/restore/preview', 'Validate restore and capture current library fingerprint', {}, { type: 'object' });
op('post', '/library/restore', 'Confirmed atomic restore, fingerprint check and verified SQLite backup first', 'ImportInput', object({ imported: integer }));
op('post', '/library/backup', 'Create and integrity-check SQLite backup beside configured owned data', null, object({ fileName: string, integrity: string }));
const legacy = op('post', '/library/legacy/convert', 'Convert an explicitly uploaded legacy SQLite file (maximum 4 MB); never reads arbitrary paths', null, { type: 'object' });
legacy.requestBody = { required: true, content: { 'application/octet-stream': { schema: { type: 'string', format: 'binary' } } } };
op('get', '/ai/local/models', 'Check installed allowed Ollama models; never downloads', null, object({ models: { type: 'array', items: string } }));
op('post', '/nova/interpret', 'Bounded read-only interpretation and retrieval; no writer capability', 'NovaRequest', 'NovaAction');
op('post', '/nova/command', 'Resolve explicit local navigation and edit requests to real IDs; no mutation, inference or external browser action. Ambiguous matches require selection.', 'LocalCommandInput', 'LocalCommand');
op('post', '/nova/propose', 'Return a draft for review; never saves it', 'ProposalInput', { type: 'object' });
op('post', '/nova/variables', 'Suggest only known variable values for review', 'NovaRequest', dictionary);
const execute = op('post', '/ai/local/execute', 'Confirmed local execution; exact reviewed snapshot; abort disconnect cancels Ollama', 'ExecuteInput', null);
execute.responses[200].content = { 'text/event-stream': { schema: { type: 'string', description: 'SSE events progress, complete, error. Each data field is one JSON object. Error after headers does not imply successful completion.' } } };
const prepare = op('post', '/nova/prepare', 'Finite retrieval-first preparation. Never saves a final prompt or executes. Optional draft persistence requires rememberDraft.', 'PrepareInput', null);
prepare.responses[200].content = { 'text/event-stream': { schema: { type: 'string', description: 'SSE stage {state}, complete PreparedDraft bound to operationId, or error {code,message}. Partial streams are unusable. Disconnect cancels inference.' } } };
op('get', '/nova/drafts', 'Recover only drafts explicitly opted into local persistence', null, { type: 'array', items: ref('PreparedDraft') });
op('get', '/nova/drafts/{id}', 'Read a remembered or unexpired in-memory draft', null, 'PreparedDraft');
op('delete', '/nova/drafts/{id}', 'Forget draft content; hash-only idempotency metadata remains', null, null, 204);
op('post', '/nova/drafts/{id}/complete', 'Complete named inputs deterministically without Ollama, preserving original draft', object({ operationId: uuid, values: dictionary }), 'PreparedDraft');
op('post', '/nova/drafts/{id}/save', 'Explicit idempotent final save; reuse or exact-content deduplication; adaptation creates a copy', object({ confirmed: boolean }), 'Prompt');
const preparedExecute = op('post', '/nova/drafts/{id}/execute', 'Explicit local execution of the exact reviewed prepared snapshot; no final save', object({ confirmed: boolean, expectedText: string, model: string }, ['confirmed', 'expectedText']), null);
preparedExecute.responses[200].content = execute.responses[200].content;
op('get', '/providers', 'Capability registry; external automatic submission is unavailable', null, { type: 'array', items: { type: 'object' } });
op('post', '/transfers/prepare', 'Prepare copy/open for an allowlisted destination; no external request', 'TransferInput', { type: 'object' });
op('get', '/openapi.json', 'This versioned API contract', null, { type: 'object' });
const document = { openapi: '3.0.3', info: { title: 'QuickPrompt NOVA local API', version: '1.0.0', description: 'Loopback only. No CORS. Mutations require X-QuickPrompt-Client: nova-v1. JSON schemas describe the new HTTP boundary; legacy Archive v1/v2 compatibility is validated by the existing store.' }, servers: [{ url: 'http://127.0.0.1:5190' }], paths, components: { schemas, securitySchemes: { LocalClientHeader: { type: 'apiKey', in: 'header', name: 'X-QuickPrompt-Client', description: 'Constant nova-v1, CSRF request-shape guard, not an authentication secret.' } } } };
const target = path.resolve(__dirname, '../contracts/openapi/nova-v1.json');
fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, JSON.stringify(document, null, 2) + '\n');
console.log(`Documented ${Object.values(paths).reduce((count, methods) => count + Object.keys(methods).length, 0)} operations.`);
