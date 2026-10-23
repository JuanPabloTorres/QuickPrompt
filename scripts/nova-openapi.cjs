const fs = require('node:fs');
const path = require('node:path');
const metadata = require('./nova-contract-metadata.cjs').load();
const localModel = { type: 'string', enum: metadata.models.map(model => model.name) };
const string = { type: 'string' }, integer = { type: 'integer' }, boolean = { type: 'boolean' }, uuid = { type: 'string', format: 'uuid' };
const ref = name => ({ $ref: '#/components/schemas/' + name });
const object = (properties, required = Object.keys(properties)) => ({ type: 'object', required, properties });
const dictionary = { type: 'object', additionalProperties: string };
const promptInput = object({ title: { ...string, maxLength: 160 }, templateText: { ...string, maxLength: 32000 }, description: { ...string, maxLength: 2000 }, categoryId: integer, revision: integer, defaults: dictionary }, ['title', 'templateText']);
const schemas = {
  PromptCategory: object({ id: { ...integer, minimum: 0 }, name: string }),
  PromptInput: promptInput,
  PromptUpdateInput: { allOf: [ref('PromptInput'), { type: 'object', required: ['revision'], properties: { revision: { ...integer, minimum: 1 } } }] },
  Prompt: object({ id: uuid, title: string, templateText: string, description: string, categoryId: integer, isFavorite: boolean, isArchived: boolean, revision: integer, createdAt: { ...string, format: 'date-time' }, updatedAt: { ...string, format: 'date-time' }, lastUsedAt: { ...string, format: 'date-time', nullable: true }, uses: integer, variableNames: { type: 'array', items: string }, defaults: dictionary }),
  PromptPage: object({ items: { type: 'array', items: ref('Prompt') }, total: integer }),
  Revision: object({ revision: { ...integer, minimum: 1 } }),
  Confirmation: object({ revision: { ...integer, minimum: 1 }, confirmed: boolean }),
  PortableTemplatePackage: { ...object({ format: { type: 'string', enum: ['quickprompt-template'] }, formatVersion: { type: 'integer', enum: [1] }, prompt: { ...object({ title: string, template: string, description: string, category: { oneOf: [string, integer] } }), additionalProperties: false } }), additionalProperties: false },
  CompileInput: object({ revision: integer, values: dictionary }),
  CompileResult: object({ text: string, templateRevision: integer }),
  UsageInput: object({ revision: integer, values: dictionary, operationId: uuid, expectedText: { ...string, maxLength: 64000 } }),
  InspectInput: object({ templateText: { ...string, maxLength: 32000 } }),
  GroupInput: object({ name: { ...string, maxLength: 80 }, revision: integer }, ['name']),
  GroupUpdateInput: object({ name: { ...string, maxLength: 80 }, revision: { ...integer, minimum: 1 } }),
  Membership: object({ groupId: uuid, revision: integer, member: boolean }),
  ImportInput: object({ data: {}, confirmed: boolean, expectedSnapshot: string }, ['data', 'confirmed']),
  NovaRequest: object({ message: { ...string, maxLength: 1000 }, model: localModel, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true } }, ['message']),
  ProposalInput: object({ intent: { type: 'string', enum: ['create', 'improve'] }, context: { ...string, maxLength: 4000 }, model: string, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true } }, ['intent', 'context']),
  LocalCommandInput: object({ message: { ...string, maxLength: 16000 } }),
  LocalCommand: object({ kind: { ...string, enum: ['NONE', 'OPEN', 'EDIT', 'UPDATE', 'LIBRARY', 'REFRESH', 'FAVORITES', 'RECENT', 'ARCHIVE'] }, candidates: { type: 'array', maxItems: 6, items: ref('Prompt') }, instruction: { ...string, maxLength: 4000 }, message: string }),
  NovaAction: object({ contractVersion: { type: 'integer', enum: [1] }, id: uuid, intent: { type: 'string', enum: metadata.enums.NovaIntent }, arguments: string, sourcePromptIds: { type: 'array', items: uuid }, requiresConfirmation: boolean, proposedEffect: string, validationStatus: string, correlationId: string, candidates: { type: 'array', maxItems: 3, items: ref('Prompt') }, message: string }),
  ExecuteInput: object({ promptId: uuid, revision: integer, values: dictionary, model: string, confirmed: boolean, expectedText: string }),
  TransferInput: object({ promptId: uuid, revision: integer, values: dictionary, providerId: { type: 'string', enum: ['chatgpt', 'claude', 'gemini', 'copilot', 'perplexity'] }, confirmed: boolean }),
  PrepareInput: object({ operationId: uuid, sessionId: { ...uuid, nullable: true }, message: { ...string, maxLength: 16000 }, model: localModel, rememberDraft: boolean, sourcePromptId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, values: dictionary, filter: object({ groupId: { ...uuid, nullable: true }, categoryId: { ...integer, nullable: true }, favorites: boolean }, []) }, ['operationId', 'message']),
  PreparedDraft: object({ id: uuid, state: { ...string, enum: ['READY', 'NEEDS_INFORMATION'] }, intent: string, decision: { ...string, enum: metadata.enums.PreparationDecision }, title: string, description: string, templateText: string, compiledText: { ...string, nullable: true }, variables: { type: 'array', items: object({ name: string, required: boolean }) }, values: { type: 'array', items: object({ name: string, value: string, valueSource: { ...string, enum: ['USER_INPUT', 'REQUEST_QUOTE', 'REQUEST_LABEL', 'TEMPLATE_DEFAULT', 'SESSION_CONTEXT'] }, validationStatus: { ...string, enum: ['VALIDATED'] } }) }, missingVariables: { type: 'array', items: string }, question: string, reason: string, candidates: { type: 'array', maxItems: 6, items: object({ prompt: ref('Prompt'), score: { type: 'number' }, reason: string }) }, sourceId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, categoryId: integer, remembered: boolean, createdAt: { ...string, format: 'date-time' } }),
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
const semanticOperation = { ...string, enum: ['SEARCH_PROMPT','RESOLVE_PROMPT','OPEN_PROMPT','PROPOSE_ADAPTATION','CREATE_DRAFT','VALIDATE_DRAFT','FILL_VARIABLES','COMPILE_PROMPT','PREPARE_LOCAL_EXECUTION','EXECUTE_LOCAL','PREPARE_PROVIDER','COPY_PROMPT','EXPLAIN','ASK_CLARIFICATION','CANCEL','PAUSE','RESUME','REQUEST_SAVE_VERSION','REQUEST_SAVE_COPY','REQUEST_DELETE'] };
const semanticOperations = { type: 'array', maxItems: 12, items: semanticOperation };
schemas.SemanticInterpretation = object({ intentions: semanticOperations,
  targetReference: { ...string, enum: ['NONE','NAMED','SELECTED','PREVIOUS_RESULT','ACTIVE_DRAFT','CANDIDATE','OTHER_CANDIDATE'] }, targetDescription: string, resolvedTargetId: { ...uuid, nullable: true }, query: string, requestedTransformation: string,
  constraints: object({ forbiddenOperations: semanticOperations, preserveOriginal: boolean, language: { ...string, nullable: true }, format: { ...string, nullable: true }, literalClauses: { type: 'array', items: string }, targetTerms: { type: 'array', items: string }, shortening: boolean, userProhibitions: { type: 'array', nullable: true, items: string } }),
  desiredOutcome: { ...string, enum: ['READ','DRAFT','NEW_DRAFT','READY_LOCAL','READY_PROVIDER','LOCAL_RESPONSE','ADVICE','CLARIFICATION','CANCELLED','PAUSED','RESUMED','REVIEW'] },
  proposedSteps: { type: 'array', maxItems: 12, items: object({ id: integer, operation: semanticOperation, dependsOn: { type: 'array', items: integer } }) }, missingInformation: { type: 'array', items: string }, requiresConfirmation: boolean, executionEvidence: string, providerId: { ...string, nullable: true }, explanation: string,
  validationNotes: { type: 'array', nullable: true, items: string }, deferredGoals: { ...semanticOperations, nullable: true }, deferredOutcome: { ...string, nullable: true }, continuesPendingTask: boolean
});
for (const name of ['PreparedDraft','NovaAction','LocalCommand']) schemas[name].properties.semantic = { allOf: [ref('SemanticInterpretation')], nullable: true };
schemas.NovaRequest.properties.sessionId = { ...uuid, nullable: true };
const liveStatus = { ...string, enum: ['CREATED','INTERPRETING','TOOL_PROPOSED','TOOL_APPROVED','TOOL_RUNNING','UI_UPDATED','VERIFYING','AWAITING_APPROVAL','PAUSED','COMPLETED','FAILED','CANCELLED','BLOCKED'] };
const liveActor = { ...string, enum: ['MODEL_SELECTED','RULE_SELECTED','USER_SELECTED','SYSTEM_VALIDATION'] };
schemas.LiveStart = object({ runId: uuid, sessionId: uuid, message: { ...string, maxLength: 16000 }, mode: { ...string, enum: ['LIVE','SAME_TAB','BROWSER'] }, sourceId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, values: dictionary, model: { ...localModel, default: metadata.defaultModel } }, ['runId','sessionId','message','mode']);
schemas.LiveRecord = object({ runId: uuid, sessionId: uuid, fingerprint: string, mode: { ...string, enum: ['LIVE','SAME_TAB','BROWSER'] }, status: liveStatus, sequence: { ...integer, minimum: 0 }, updatedAt: { ...string, format: 'date-time' }, targetId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true }, pauseRequested: boolean, browserOpen: boolean, errorCode: { ...string, nullable: true }, approval: string, effectCommitted: boolean, model: string, contractVersion: string });
schemas.LiveEvent = object({ runId: uuid, eventId: uuid, sequence: { ...integer, minimum: 1 }, timestamp: { ...string, format: 'date-time' }, stage: string, actor: liveActor, toolName: { ...string, nullable: true }, targetId: { ...uuid, nullable: true }, status: liveStatus, durationMs: { type: 'number', nullable: true }, errorCode: { ...string, nullable: true }, safeDisplayMessage: string, actionId: { ...uuid, nullable: true }, actionSequence: { ...integer, nullable: true } });
schemas.UiVisibleState = object({ mode: { ...string, enum: ['discover','focus','create'] }, candidates: { type: 'array', maxItems: 24, items: object({ id: uuid, title: string, revision: integer }) }, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true }, title: { ...string, nullable: true }, templateText: { ...string, nullable: true }, values: dictionary, variableNames: { type: 'array', items: string }, draftChanged: boolean, compiledReady: boolean, compiledText: { ...string, nullable: true }, response: { ...string, nullable: true }, responseComplete: boolean, providerOpen: boolean, providerId: { ...string, nullable: true }, error: { ...string, nullable: true } });
schemas.LiveProposal = object({ id: uuid, hash: string, promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true }, draft: ref('PromptInput'), originalText: string });
schemas.LiveView = object({ run: ref('LiveRecord'), request: { ...string, nullable: true }, question: { ...string, nullable: true }, observation: { allOf: [ref('UiVisibleState')], nullable: true }, proposal: { allOf: [ref('LiveProposal')], nullable: true }, decisions: { type: 'array', maxItems: 16, items: object({ toolName: string, arguments: { type: 'object' }, selectionFormat: { ...string, enum: ['tool_call','json_schema_retry'] } }) } });
schemas.LiveView.properties.semantic = { allOf: [ref('SemanticInterpretation')], nullable: true };
schemas.LiveDeletion = object({ id: uuid, hash: string, promptId: uuid, revision: { ...integer, minimum: 1 }, title: string });
schemas.LiveView.properties.deletion = { allOf: [ref('LiveDeletion')], nullable: true };
schemas.LiveRuntime = object({
  version: { ...string, enum: ['nova-runtime-1'] }, runId: uuid, sequence: integer,
  objective: string, contextSource: string, contextResolved: boolean, model: string,
  modelState: { ...string, enum: ['ready','interpreting','generating','waiting','error'] },
  lastInferenceMs: { type: 'number', nullable: true }, elapsedMs: { type: 'number' }, activeTool: { ...string, nullable: true },
  tools: { type: 'array', items: object({ id: string, label: string, state: { ...string, enum: ['available','selected','running','completed','blocked','paused','unavailable'] } }) },
  memory: object({ sourceBound: boolean, draftActive: boolean, resolvedVariables: integer, totalVariables: integer, reusesDraft: boolean, constraints: { type: 'array', items: string } }),
  flow: { type: 'array', items: object({ id: string, label: string, state: { ...string, enum: ['pending','active','completed','stopped','paused','waiting'] } }) },
  recoverableOnly: boolean
});
schemas.LiveView.properties.runtime = { allOf: [ref('LiveRuntime')], nullable: true };
Object.assign(schemas.UiVisibleState.properties, { deletionPromptId: { ...uuid, nullable: true }, deletionRevision: { ...integer, nullable: true }, deletionRunId: { ...uuid, nullable: true }, deletionConfirmationReady: boolean });
op('post', '/nova/live', 'Start one finite Qwen3 visual run in the original React document. SAME_TAB requires an ephemeral X-Nova-Surface capability (32-128 characters); BROWSER is disabled unless explicit QA configuration. Same RunId never replays effects.', 'LiveStart', 'LiveRecord');
schemas.UiAction = object({ runId: uuid, actionId: uuid, sequence: integer, tool: string, target: { ...uuid, nullable: true }, arguments: { type: 'object' }, executionStatus: string, verificationStatus: string, timestamp: { ...string, format: 'date-time' } });
schemas.UiActionReceipt = object({ actionId: uuid, sequence: integer, executionStatus: { ...string, enum: ['EXECUTED','INTERRUPTED'] }, verificationStatus: { ...string, enum: ['VERIFIED','UNKNOWN'] }, observation: { allOf: [ref('UiVisibleState')], nullable: true }, errorCode: { ...string, nullable: true } });
const surfaceHeader = { name: 'X-Nova-Surface', in: 'header', required: false, schema: { ...string, minLength: 32, maxLength: 128 }, description: 'Ephemeral capability of the requesting document, kept only in memory. Never model input or persisted activity.' };
paths['/api/v1/nova/live'].post.parameters.push(surfaceHeader);
const pollAction = op('get', '/nova/live/{id}/actions', 'Owner-only heartbeat and pending action; hidden/disconnected documents pause. Never authorizes replay.', null, object({ run: ref('LiveRecord'), action: { allOf: [ref('UiAction')], nullable: true } }));
pollAction.parameters.push(surfaceHeader, { name: 'visible', in: 'query', required: true, schema: boolean });
const actionReceipt = op('post', '/nova/live/{id}/actions/receipt', 'Acknowledge exact ActionId/sequence with observed controls. Identical receipt retries are idempotent; wrong target, sequence, changed duplicate and late receipts are rejected.', 'UiActionReceipt', null, 204);
actionReceipt.parameters.push(surfaceHeader);
const actionProgress = op('post', '/nova/live/{id}/actions/progress', 'Report located DOM target or performed DOM interaction. Progress never substitutes for final verified receipt.', object({ actionId: uuid, sequence: integer, executionStatus: { ...string, enum: ['TARGET_LOCATED','EXECUTED'] } }), null, 204);
actionProgress.parameters.push(surfaceHeader);
op('post', '/nova/live/observe', 'Create a metadata/event lifetime for an explicitly selected existing model operation; does not execute it', 'LiveStart', 'LiveRecord');
op('get', '/nova/live/{id}', 'Read persisted control metadata plus temporary authorized request, observation and proposal; content is not a general log', null, 'LiveView');
const activity = op('get', '/nova/live/{id}/events', 'Ordered replayable execution events. Observer disconnect never starts or repeats tools.', null, null);
activity.parameters.push({ name: 'after', in: 'query', schema: { ...integer, minimum: 0 } }, { name: 'Last-Event-ID', in: 'header', schema: string });
activity.responses[200].content = { 'text/event-stream': { schema: { ...string, description: 'event: activity; id: RunId:Sequence; data: LiveEvent. Monotonic sequence, stable EventId. Only transport keep-alive comments are timer based.' } } };
for (const control of ['pause','resume','cancel']) op('post', `/nova/live/{id}/${control}`, 'Control the actual run at operation boundaries; resume validates the current source revision', null, 'LiveRecord');
op('post', '/nova/live/{id}/approve', 'Single proposal/run/content/revision approval. Does not itself write the source; the supervised React save control consumes it.', object({ approvalId: uuid, hash: string, revision: { ...integer, nullable: true }, confirmed: boolean }), 'LiveRecord');
op('post', '/nova/live/{id}/deletion', 'Human-only deletion decision bound to the visible proposal, run, target and revision. Confirming deletes once; cancellation preserves the record. No model commit tool.', object({ approvalId: uuid, hash: string, revision: { ...integer, nullable: true }, confirmed: boolean }), 'LiveRecord');
op('post', '/nova/live/{id}/proposal', 'Review user edits to a pending proposal; invalidates the old approval hash and preserves source', 'PromptUpdateInput', 'LiveView');
op('post', '/nova/live/{id}/commit', 'Consume one current exact-content approval through the real editor control, with source revision guard', 'PromptUpdateInput', 'Prompt');
op('post', '/nova/live/{id}/focus', 'Activate the owned visible browser while preserving its current operation', null, null, 204);
op('post', '/nova/live/{id}/ui-observed', 'React acknowledges the rendered preparation result; server checks draft, source and exact text. Does not repeat domain operations.', object({ preparedId: uuid, sourceId: { ...uuid, nullable: true }, sourceRevision: { ...integer, nullable: true }, renderedText: string }), null, 204);
op('post', '/templates/compile', 'Pure deterministic editor preview; no model, saved prompt or usage mutation', object({ templateText: { ...string, maxLength: 32000 }, values: dictionary }), object({ text: { ...string, nullable: true }, missingVariables: { type: 'array', items: string } }));
op('post', '/nova/editor-drafts', 'Prepare an ephemeral editor snapshot and optional session context. Null source ID/revision represents a new unsaved artifact; never saves a source or grants approval.', object({ promptId: { ...uuid, nullable: true }, revision: { ...integer, nullable: true }, title: { ...string, maxLength: 160 }, templateText: { ...string, maxLength: 32000 }, values: dictionary, sessionId: { ...uuid, nullable: true } }, ['promptId','revision','title','templateText','values']), 'PreparedDraft');
operation('get', '/health', 'Check isolated local database integrity', null, object({ status: string, schemaVersion: integer }));
operation('get', '/api/v1/categories', 'List persisted domain category IDs and display labels', null, { type: 'array', items: ref('PromptCategory') });
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
const shareTemplate = op('get', '/prompts/{id}/share', 'Read the existing portable template allowlist, excluding identity, defaults, usage and history', null, 'PortableTemplatePackage');
shareTemplate.parameters.push({ name: 'revision', in: 'query', required: false, schema: { ...integer, minimum: 1 }, description: 'Expected source revision; conflict if the source changed before its snapshot was read.' });
const restoreVersion = op('post', '/prompts/{id}/versions/{versionRevision}/restore', 'Restore a server-owned historical snapshot as a new revision after confirmation; preserve current operational state and all history', 'Confirmation', 'Prompt');
restoreVersion.parameters.find(parameter => parameter.name === 'versionRevision').schema = { ...integer, minimum: 1 };
op('post', '/templates/inspect', 'Detect legacy placeholders without persistence', 'InspectInput', object({ variableNames: { type: 'array', items: string } }));
op('post', '/templates/toggle-variable', 'Create a variable from a selection or restore literal text from a selected variable. Pure operation; never saves a prompt.', object({ templateText: { ...string, maxLength: 32000 }, start: integer, end: integer, originalText: { ...string, nullable: true, maxLength: 32000 } }, ['templateText', 'start', 'end']), object({ templateText: string, variableNames: { type: 'array', items: string }, start: integer, end: integer, action: { ...string, enum: ['created', 'removed'] }, name: string, originalText: { ...string, nullable: true } }));
op('post', '/templates/rename-variable', 'Rename all occurrences using the existing domain editor; preserves escaped literals and moves defaults. Pure operation; never persists.', object({ templateText: { ...string, maxLength: 32000 }, start: integer, end: integer, name: { ...string, maxLength: 80 }, defaults: dictionary }, ['templateText', 'start', 'end', 'name']), object({ templateText: string, variableNames: { type: 'array', items: string }, defaults: dictionary, start: integer, end: integer, previousName: string, name: string }));
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
op('post', '/nova/command', 'Compatibility projection of real Qwen semantic interpretation. No mutation; compound editing requests continue through preparation. Ambiguous matches require selection.', 'LocalCommandInput', 'LocalCommand');
op('post', '/nova/semantic/interpret', 'Read-only Qwen interpretation using bounded session/UI context; validates typed goals and dependencies. Does not execute the resulting plan.', 'LocalCommandInput', 'SemanticInterpretation');
op('post', '/nova/propose', 'Return a draft for review; never saves it', 'ProposalInput', { type: 'object' });
op('post', '/nova/variables', 'Suggest only known variable values for review', 'NovaRequest', dictionary);
const execute = op('post', '/ai/local/execute', 'Confirmed local execution; exact reviewed snapshot; abort disconnect cancels Ollama', 'ExecuteInput', null);
execute.responses[200].content = { 'text/event-stream': { schema: { type: 'string', description: 'SSE events progress, complete, error. Each data field is one JSON object. Error after headers does not imply successful completion.' } } };
const prepare = op('post', '/nova/prepare', 'Finite retrieval-first preparation with LIVE events. Never saves a final prompt. Executes locally only on an explicit authorized request; optional draft persistence requires rememberDraft.', 'PrepareInput', null);
prepare.responses[200].content = { 'text/event-stream': { schema: { type: 'string', description: 'SSE stage {state}, complete PreparedDraft bound to operationId, or error {code,message}. Partial streams are unusable. Disconnect cancels inference.' } } };
op('get', '/nova/drafts', 'Recover only drafts explicitly opted into local persistence', null, { type: 'array', items: ref('PreparedDraft') });
op('get', '/nova/drafts/{id}', 'Read a remembered or unexpired in-memory draft', null, 'PreparedDraft');
op('delete', '/nova/drafts/{id}', 'Forget draft content; hash-only idempotency metadata remains', null, null, 204);
op('post', '/nova/drafts/{id}/complete', 'Complete named inputs deterministically without Ollama, preserving original draft', object({ operationId: uuid, values: dictionary }), 'PreparedDraft');
op('post', '/nova/drafts/{id}/save', 'Proposal-bound confirmation: save copy by default or approve a new source version with revision check; idempotent', object({ confirmed: boolean, updateSource: boolean }, ['confirmed']), 'Prompt');
op('get', '/nova/runs/{id}', 'Local redacted execution checkpoint; no request, values or generated content', null, object({ runId: uuid, sessionId: uuid, state: string, plan: { type: 'array', items: string }, retrievedPromptIds: { type: 'array', items: uuid }, sourceRevisions: { type: 'object', additionalProperties: integer }, tools: { type: 'array', items: object({ tool: string, outcome: string, durationMs: { type: 'number' } }) }, stepBudget: integer, approval: string, model: string, contractVersion: string, errorCode: { ...string, nullable: true } }));
op('delete', '/nova/runs/{id}', 'Delete local checkpoint metadata; never authorizes replay of a draft', null, null, 204);
op('post', '/nova/sessions/{id}/context', 'Bind UI selection separately from conversational draft. Optional stale context is metadata only and cannot authorize effects.', object({ promptId: uuid, revision: integer, values: dictionary, allowStaleContext: boolean }, ['promptId', 'revision']), null, 204);
op('post', '/nova/sessions/{id}/draft-context', 'Explicitly activate an actual recovered draft as temporary conversation context; never saves a prompt or grants approval.', object({ draftId: uuid }), null, 204);
op('delete', '/nova/sessions/{id}', 'Forget temporary session context', null, null, 204);
const preparationPreferences = object({ language: { ...string, enum: ['es', 'en'], nullable: true }, format: { ...string, enum: ['JSON', 'Markdown', 'Tabla', 'Texto'], nullable: true }, providerId: { ...string, nullable: true } });
op('get', '/nova/preferences', 'Review explicitly approved preparation preferences', null, preparationPreferences);
op('put', '/nova/preferences', 'Confirm local preparation preferences; no conversation retention', object({ preferences: preparationPreferences, confirmed: boolean }), preparationPreferences);
op('delete', '/nova/preferences', 'Delete explicit preparation preferences', null, null, 204);
Object.assign(schemas.PreparedDraft.properties, { sessionId: { ...uuid, nullable: true }, localResponse: { ...string, nullable: true }, providerId: { ...string, nullable: true } });
schemas.PreparedDraft.properties.state.enum.push('CANCELLED', 'USER_ACTION', 'COMPLETED', 'FAILED', 'BLOCKED');
const preparedExecute = op('post', '/nova/drafts/{id}/execute', 'Explicit local execution of the exact reviewed prepared snapshot; operation receipt prevents replay; no final save', object({ operationId: uuid, confirmed: boolean, expectedText: string, model: string }, ['confirmed', 'expectedText']), null);
preparedExecute.responses[200].content = execute.responses[200].content;
op('get', '/providers', 'Capability registry; external automatic submission is unavailable', null, { type: 'array', items: { type: 'object' } });
op('post', '/transfers/prepare', 'Prepare copy/open for an allowlisted destination; no external request', 'TransferInput', { type: 'object' });
op('get', '/openapi.json', 'This versioned API contract', null, { type: 'object' });
const document = { openapi: '3.0.3', info: { title: 'QuickPrompt NOVA local API', version: '1.0.0', description: 'Loopback only. No CORS. Mutations require X-QuickPrompt-Client: nova-v1. JSON schemas describe the new HTTP boundary; legacy Archive v1/v2 compatibility is validated by the existing store.' }, servers: [{ url: 'http://127.0.0.1:5190' }], paths, components: { schemas, securitySchemes: { LocalClientHeader: { type: 'apiKey', in: 'header', name: 'X-QuickPrompt-Client', description: 'Constant nova-v1, CSRF request-shape guard, not an authentication secret.' } } } };
const target = path.resolve(__dirname, '../contracts/openapi/nova-v1.json');
fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, JSON.stringify(document, null, 2) + '\n');
console.log(`Documented ${Object.values(paths).reduce((count, methods) => count + Object.keys(methods).length, 0)} operations.`);
