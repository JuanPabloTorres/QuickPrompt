import { createInterface } from 'node:readline';
import { createRequire } from 'node:module';
import * as fs from 'node:fs';
import * as path from 'node:path';

type Args = Record<string, unknown>;
type Command = { id: string; tool: string; arguments: Args };
type Prompt = { id: string; title: string; revision: number };
type Observation = { mode: string; candidates: Prompt[]; promptId: string | null; revision: number | null; title: string | null;
  templateText: string | null; values: Record<string, string>; variableNames: string[]; draftChanged: boolean; compiledReady: boolean;
  compiledText: string | null; response: string | null; responseComplete: boolean; providerOpen: boolean; error: string | null };
// Runtime is an already installed, explicitly configured Playwright module. No npx/install/download fallback.
const load = createRequire(__filename);
const { chromium } = load(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
// Playwright types come from that runtime, whose location is machine configuration, not an npm dependency of this worker.
let browser: any, context: any, page: any;
let origin = '', runId = '', localAllowed = false, editRequest = '';
let qaDirectory: string | null = null, qaStep = 0;
let stopping = false, updatePrepared = false;
type WorkingDraft = { promptId: string; revision: number; title: string; templateText: string; values: Record<string, string> };
let workingDraft: WorkingDraft | null = null;
let previous: Observation | null = null;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function keys(args: Args, names: string[]) { if (Object.keys(args).some(k => !names.includes(k)) || names.some(k => !(k in args))) throw Error('INVALID_ARGUMENTS'); }
function text(args: Args, key: string, max: number): string { const value = args[key]; if (typeof value !== 'string' || !value.trim() || value.length > max) throw Error('INVALID_ARGUMENTS'); return value; }
async function visible(locator: any) { if (await locator.count() !== 1 || !await locator.isVisible() || !await locator.isEnabled()) throw Error('TARGET_NOT_ACTIONABLE'); return locator; }
async function click(name: string) { await (await visible(page.getByRole('button', { name, exact: true }))).click(); }
async function resultOrError(locator: any, timeout: number) {
  await Promise.race([locator.waitFor({ state: 'visible', timeout }), page.getByRole('alert').first().waitFor({ state: 'visible', timeout }).then(() => { throw Error('UI_REPORTED_ERROR'); })]);
}
async function root() {
  const editor = page.getByTestId('prompt-editor'), focus = page.getByTestId('prompt-focus');
  return await editor.isVisible() ? editor : await focus.isVisible() ? focus : null;
}
async function settledCompilation() {
  if (await page.getByTestId('prompt-editor').isVisible()) await page.getByTestId('variables-inspected').waitFor({ state: 'visible' });
  const fields = await page.getByTestId('variable-field').all();
  for (const field of fields) if (!(await field.inputValue()).trim()) return;
  if (await root()) await page.getByTestId('compile-ready').waitFor({ state: 'visible', timeout: 15000 });
}
async function read(): Promise<Observation> {
  if (new URL(page.url()).origin !== origin) throw Error('ORIGIN_BLOCKED');
  const node = await root(), candidates: Prompt[] = [];
  for (const locator of await page.getByTestId('library-prompt').all()) {
    if (!await locator.isVisible()) continue;
    const id = await locator.getAttribute('data-prompt-id'), revision = Number(await locator.getAttribute('data-revision'));
    if (!uuid.test(id) || !Number.isInteger(revision) || revision < 1) throw Error('INVALID_DOM_IDENTITY');
    candidates.push({ id, title: await locator.getAttribute('aria-label'), revision });
  }
  const values: Record<string, string> = {};
  for (const field of await page.getByTestId('variable-field').all()) if (await field.isVisible()) values[await field.getAttribute('data-variable-name')] = await field.inputValue();
  const editor = node && await page.getByTestId('prompt-editor').isVisible();
  const response = page.getByTestId('local-qwen-response');
  const alert = page.getByRole('alert').first();
  const ready = await page.getByTestId('compile-ready').isVisible();
  const id = node ? await node.getAttribute('data-prompt-id') : null;
  if (id && !uuid.test(id)) throw Error('INVALID_DOM_IDENTITY');
  return {
    mode: editor ? 'create' : node ? 'focus' : 'discover', candidates, promptId: id || null,
    revision: node ? Number(await node.getAttribute('data-revision')) : null,
    title: editor ? await page.getByLabel('Título', { exact: true }).inputValue() : node ? await node.getByTestId('focus-title').innerText() : null,
    templateText: editor ? await page.getByLabel('Contenido', { exact: true }).inputValue() : node ? await node.getAttribute('data-template-text') : null,
    values, variableNames: Object.keys(values), draftChanged: editor ? await page.getByTestId('draft-prepared').isVisible() : false,
    compiledReady: ready, compiledText: ready ? await page.getByTestId('compiled-preview').innerText() : null,
    response: await response.isVisible() ? await response.innerText() : null,
    responseComplete: await response.isVisible() && await response.getAttribute('data-complete') === 'true',
    providerOpen: await page.getByTestId('prepared-provider-dialog').isVisible(), error: await alert.isVisible() ? 'REACT_REPORTED_ERROR' : null
  };
}
async function execute(command: Command): Promise<Observation | null> {
  const { tool, arguments: args } = command;
  if (tool === 'start') {
    keys(args, ['origin', 'runId', 'localExecutionAllowed', 'editRequest']);
    const u = new URL(text(args, 'origin', 100));
    if (u.origin !== args.origin || u.protocol !== 'http:' || u.hostname !== '127.0.0.1' || !u.port || u.username || u.password || !uuid.test(String(args.runId)) || typeof args.localExecutionAllowed !== 'boolean') throw Error('ORIGIN_BLOCKED');
    if (browser) throw Error('ALREADY_STARTED'); origin = u.origin; runId = String(args.runId); localAllowed = args.localExecutionAllowed;
    if (typeof args.editRequest !== 'string' || args.editRequest.length > 16000) throw Error('INVALID_ARGUMENTS'); editRequest = args.editRequest;
    browser = await chromium.launch({ channel: 'msedge', headless: false });
    browser.on('disconnected', () => { if (!stopping) process.exit(2); });
    if (process.env.NOVA_UI_QA_OUT && process.env.NOVA_UI_QA_APPROVED === 'synthetic-only') { qaDirectory = path.join(path.resolve(process.env.NOVA_UI_QA_OUT), runId); fs.mkdirSync(qaDirectory, { recursive: true }); }
    context = await browser.newContext({ acceptDownloads: false, serviceWorkers: 'block', viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce', extraHTTPHeaders: { 'X-Nova-Live-Run': runId }, ...(qaDirectory ? { recordVideo: { dir: qaDirectory, size: { width: 1280, height: 900 } } } : {}) });
    await context.route('**/*', async (route: any) => {
      const request = route.request(), target = new URL(request.url());
      const mutation = !['GET', 'HEAD'].includes(request.method());
      const allowedPath = ['/api/v1/templates/inspect', '/api/v1/templates/compile', '/api/v1/nova/propose', '/api/v1/nova/editor-drafts', `/api/v1/nova/live/${runId}/commit`].includes(target.pathname)
        || localAllowed && /^\/api\/v1\/nova\/drafts\/[0-9a-f-]+\/execute$/.test(target.pathname)
        || /^\/api\/v1\/prompts\/[0-9a-f-]+\/compile$/.test(target.pathname)
        || target.pathname.startsWith(`/api/v1/nova/live/${runId}/`) && /\/(pause|resume|cancel|approve|focus|proposal)$/.test(target.pathname);
      if (target.origin !== origin || mutation && !allowedPath) return route.abort('blockedbyclient');
      return route.continue();
    });
    page = await context.newPage(); page.setDefaultTimeout(20000);
    page.on('popup', (popup: any) => popup.close().catch(() => {})); page.on('dialog', (dialog: any) => dialog.dismiss().catch(() => {}));
    await page.goto(origin + '/?novaRun=' + runId, { waitUntil: 'domcontentloaded' });
    await page.getByTestId('library-ready').waitFor({ state: 'visible' }); return read();
  }
  if (!browser || !page || page.isClosed()) throw Error('BROWSER_CLOSED');
  if (new URL(page.url()).origin !== origin) throw Error('ORIGIN_BLOCKED');
  if (tool === 'set_working_context') {
    keys(args, ['promptId', 'revision', 'title', 'templateText', 'values']);
    if (workingDraft || !uuid.test(String(args.promptId)) || !Number.isInteger(args.revision) || Number(args.revision) < 1 || typeof args.title !== 'string' || args.title.length > 160
      || typeof args.templateText !== 'string' || args.templateText.length > 32000 || !args.values || Array.isArray(args.values) || typeof args.values !== 'object'
      || Object.keys(args.values).length > 60 || Object.entries(args.values).some(([name, value]) => name.length > 80 || typeof value !== 'string' || value.length > 8000)) throw Error('INVALID_WORKING_CONTEXT');
    workingDraft = args as unknown as WorkingDraft; return read();
  }
  if (tool === 'focus') { keys(args, []); await page.bringToFront(); return null; }
  if (tool === 'close') { keys(args, []); stopping = true; await browser.close(); return null; }
  if (previous) {
    const actual = await read();
    if (actual.mode !== previous.mode || actual.promptId !== previous.promptId || actual.revision !== previous.revision
      || tool !== 'commit_approved' && (actual.templateText !== previous.templateText || JSON.stringify(actual.values) !== JSON.stringify(previous.values))) throw Error('VISIBLE_STATE_CHANGED');
  }
  if (args.promptId !== undefined && tool !== 'ui_open_prompt' && String(args.promptId).toLowerCase() !== (await read()).promptId?.toLowerCase()) throw Error('UI_IDENTITY_MISMATCH');
  if (tool === 'ui_search_prompt') {
    keys(args, ['query']); const query = text(args, 'query', 200);
    if ((await read()).mode !== 'discover') throw Error('UI_PRECONDITION');
    const input = await visible(page.getByLabel('Buscar en la biblioteca', { exact: true }));
    const response = page.waitForResponse((r: any) => new URL(r.url()).pathname === '/api/v1/prompts' && new URL(r.url()).searchParams.get('search') === query && r.ok());
    await input.fill(query); await response; await page.getByTestId('library-ready').waitFor({ state: 'visible' });
  } else if (tool === 'ui_open_prompt') {
    keys(args, ['promptId']); const id = text(args, 'promptId', 36).toLowerCase(); const state = await read();
    if (!uuid.test(id) || !state.candidates.some(p => p.id === id)) throw Error('UI_TARGET_NOT_FOUND');
    const item = page.getByTestId('library-prompt').filter({ has: page.getByTestId('identity-' + id) });
    await (await visible(item)).click(); await page.getByTestId('prompt-focus').waitFor({ state: 'visible' });
    if ((await read()).promptId !== id) throw Error('UI_IDENTITY_MISMATCH'); await settledCompilation();
  } else if (tool === 'ui_open_editor') {
    keys(args, ['promptId']); if ((await read()).mode !== 'focus') throw Error('UI_PRECONDITION');
    await click('Editar'); await page.getByTestId('prompt-editor').waitFor({ state: 'visible' }); await settledCompilation();
  } else if (tool === 'ui_load_active_draft') {
    keys(args, ['promptId']); const state = await read();
    if (!workingDraft || args.promptId !== workingDraft.promptId || state.promptId !== workingDraft.promptId || state.revision !== workingDraft.revision || updatePrepared) throw Error('INVALID_WORKING_CONTEXT');
    if (state.mode === 'focus') { await click('Editar'); await page.getByTestId('prompt-editor').waitFor({ state: 'visible' }); }
    else if (state.mode !== 'create') throw Error('UI_PRECONDITION');
    await (await visible(page.getByLabel('Título', { exact: true }))).fill(workingDraft.title);
    await (await visible(page.getByLabel('Contenido', { exact: true }))).fill(workingDraft.templateText);
    await page.getByTestId('variables-inspected').waitFor({ state: 'visible' });
    for (const [name, value] of Object.entries(workingDraft.values)) if (value.trim()) {
      const field = page.getByTestId('variable-field').and(page.getByLabel(name, { exact: true })); await (await visible(field)).fill(value);
    }
    await settledCompilation();
  } else if (tool === 'ui_fill_variable') {
    keys(args, ['promptId', 'name', 'value', 'evidence']); const name = text(args, 'name', 80), value = text(args, 'value', 8000); text(args, 'evidence', 16000);
    const state = await read(); if (!state.variableNames.includes(name)) throw Error('UNKNOWN_VARIABLE');
    const field = page.getByTestId('variable-field').and(page.getByLabel(name, { exact: true }));
    await (await visible(field)).fill(value); if (await field.inputValue() !== value) throw Error('UI_VALUE_MISMATCH'); await settledCompilation();
  } else if (tool === 'ui_prepare_update') {
    keys(args, ['promptId']); if (!editRequest.trim() || editRequest.length > 4000) throw Error('UI_EDIT_REQUEST_TOO_LONG');
    const before = await read(); if (before.mode !== 'create' || updatePrepared) throw Error('UI_PRECONDITION');
    await (await visible(page.getByLabel('Cambio que necesitas', { exact: true }))).fill(editRequest);
    await click('Preparar cambio en el editor'); await resultOrError(page.getByTestId('draft-prepared'), 120000);
    if ((await read()).templateText === before.templateText) throw Error('UI_DRAFT_UNCHANGED'); updatePrepared = true; await settledCompilation();
    await page.getByLabel('Contenido', { exact: true }).scrollIntoViewIfNeeded();
  } else if (tool === 'ui_show_compiled_prompt') { keys(args, ['promptId']); await settledCompilation(); await page.getByTestId('compiled-preview').scrollIntoViewIfNeeded(); }
  else if (tool === 'ui_open_provider_picker' || tool === 'ui_execute_local') {
    keys(args, ['promptId']); if (!(await read()).compiledReady) throw Error('MISSING_INPUTS');
    if (!await page.getByTestId('prepared-provider-dialog').isVisible()) { await click('Usar en…'); await page.getByTestId('prepared-provider-dialog').waitFor({ state: 'visible' }); }
    if (tool === 'ui_execute_local') {
      if (!localAllowed || (await read()).responseComplete) throw Error('LOCAL_EXECUTION_NOT_ALLOWED');
      await click('Ejecutar con Qwen3'); await resultOrError(page.getByTestId('local-response-complete'), 150000);
      if (!(await read()).response?.trim()) throw Error('EMPTY_MODEL_RESPONSE');
      await page.getByTestId('local-qwen-response').scrollIntoViewIfNeeded();
      // Keep the response in the visible dialog. The observed prompt identity still comes from the underlying editor/focus.
    }
  } else if (tool === 'commit_approved') {
    keys(args, []);
    const before = await read(); if (!before.draftChanged) throw Error('UI_PRECONDITION');
    if (await page.getByTestId('prepared-provider-dialog').isVisible()) await page.getByRole('dialog', { name: 'Usa tu prompt preparado', exact: true }).getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
    await page.getByRole('button', { name: 'Aplicar versión aprobada', exact: true }).waitFor({ state: 'visible' });
    await click('Aplicar versión aprobada'); await page.getByTestId('prompt-focus').waitFor({ state: 'visible' });
    const after = await read(); if (after.promptId !== before.promptId || after.revision !== before.revision! + 1) throw Error('SAVE_NOT_VERIFIED'); await settledCompilation();
  } else if (tool === 'ui_read_visible_state' || tool === 'ui_verify_prompt_state') { keys(args, tool === 'ui_verify_prompt_state' ? ['promptId'] : []); }
  else throw Error('UNKNOWN_UI_TOOL');
  return read();
}
export { execute };
if (require.main === module) {
  const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
  let chain = Promise.resolve();
  lines.on('line', line => {
    if (Buffer.byteLength(line, 'utf8') > 200000) { process.exitCode = 1; lines.close(); return; }
    const handle = async () => {
      let id = '';
      try {
        const command = JSON.parse(line) as Command;
        if (!command || Object.keys(command).some(k => !['id', 'tool', 'arguments'].includes(k)) || typeof command.id !== 'string' || command.id.length > 80 || typeof command.tool !== 'string' || !command.arguments || Array.isArray(command.arguments)) throw Error('INVALID_COMMAND');
        id = command.id; const observation = await execute(command);
        if (observation) previous = observation;
        if (qaDirectory && observation) { const label = `${String(++qaStep).padStart(2, '0')}-${command.tool}`; await page.screenshot({ path: path.join(qaDirectory, label + '.png'), fullPage: true }); fs.writeFileSync(path.join(qaDirectory, label + '.json'), JSON.stringify({ runId, commandId: id, tool: command.tool, observation }, null, 2)); fs.writeFileSync(path.join(qaDirectory, 'browser.json'), JSON.stringify({ runId, channel: 'msedge', version: browser.version(), headed: true, isolatedContext: true, steps: qaStep })); }
        process.stdout.write(JSON.stringify({ id, ok: true, observation }) + '\n');
      } catch (error) {
        // Never print locator text, values, page bodies or Playwright exception details.
        const known = error instanceof Error && /^[A-Z_]{3,80}$/.test(error.message) ? error.message : 'BROWSER_ACTION_FAILED';
        if (qaDirectory && page && !page.isClosed()) { try { await page.screenshot({ path: path.join(qaDirectory, `failed-${++qaStep}.png`), fullPage: true }); fs.writeFileSync(path.join(qaDirectory, 'failure.json'), JSON.stringify({ runId, commandId: id, errorCode: known })); } catch { /* A closed browser cannot produce a screenshot; the typed failure remains authoritative. */ } }
        process.stdout.write(JSON.stringify({ id, ok: false, errorCode: known }) + '\n');
      }
    };
    // Focus is a trusted transport control, never a model tool. It must work while a long UI action awaits Qwen.
    let immediate = false; try { immediate = JSON.parse(line)?.tool === 'focus'; } catch { /* The strict handler reports malformed input. */ }
    if (immediate) void handle(); else chain = chain.then(handle);
  });
  const close = async () => { stopping = true; await browser?.close().catch(() => {}); };
  lines.on('close', () => { void close(); }); process.on('SIGTERM', () => { void close().finally(() => process.exit()); });
}
