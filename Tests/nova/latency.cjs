// Real same-tab probe. Optional diagnostics retain synthetic task contracts only, never thinking.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const assert = require('node:assert/strict'), { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { authoredContentRequested, caseAcceptance, campaignAcceptance } = require('./acceptance.cjs');
async function resourceSnapshot() {
  async function read(command, args) {
    return new Promise(resolve => {
      const child = spawn(command, args, { windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] }); let output = '';
      const timeout = setTimeout(() => { child.kill(); resolve({ status: 'NOT_TESTED', reason: 'snapshot timeout' }); }, 5000);
      child.stdout.on('data', bytes => { output += bytes; });
      child.once('error', () => { clearTimeout(timeout); resolve({ status: 'NOT_TESTED', reason: 'snapshot command unavailable' }); });
      child.once('exit', code => { clearTimeout(timeout); resolve(code === 0 ? { status: 'MEASURED', output: output.trim() } : { status: 'NOT_TESTED', reason: 'snapshot command failed' }); });
    });
  }
  const [ram, gpu] = await Promise.all([
    read('powershell.exe', ['-NoProfile', '-Command', 'Get-CimInstance Win32_OperatingSystem | Select-Object TotalVisibleMemorySize,FreePhysicalMemory | ConvertTo-Json -Compress']),
    read('nvidia-smi.exe', ['--query-gpu=memory.total,memory.used,utilization.gpu,temperature.gpu', '--format=csv,noheader,nounits'])
  ]);
  return { at: new Date().toISOString(), ram, gpu, peak: 'NOT_TESTED: before/after snapshots are not a sampled peak' };
}
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..'), stamp = Date.now();
const searchFixturePath = process.env.NOVA_PERF_SOURCE_FIXTURE ? path.resolve(root, process.env.NOVA_PERF_SOURCE_FIXTURE) : null;
assert.ok(!searchFixturePath || searchFixturePath.toLowerCase().startsWith((path.join(root, '.playwright') + path.sep).toLowerCase()), 'Source metadata must be an owned QA fixture');
const searchFixture = searchFixturePath ? JSON.parse(fs.readFileSync(searchFixturePath, 'utf8')) : null;
assert.ok(!searchFixture || searchFixture.syntheticOnly === true && process.env.NOVA_PERF_FIXTURE && process.env.NOVA_PERF_SCENARIOS === 'search', 'Custom source cases require an owned synthetic search-only fixture');
const variant = process.env.NOVA_PERF_VARIANT || 'A0', count = Number(process.env.NOVA_PERF_COUNT || 1);
const contractDiagnostics = process.env.NOVA_SYNTHETIC_CONTRACT_DIAGNOSTICS === '1';
assert.ok(!contractDiagnostics || process.env.NOVA_PERF_FIXTURE, 'Contract diagnostics require an explicitly owned synthetic fixture');
const debugPointsEnabled = process.env.NOVA_SYNTHETIC_DEBUG_POINTS === '1', debugPoints = [];
assert.ok(!debugPointsEnabled || process.env.NOVA_PERF_FIXTURE, 'Debug points require an explicitly owned synthetic fixture');
const initialSourceTitle = process.env.NOVA_PERF_START_SOURCE;
assert.ok(!initialSourceTitle || process.env.NOVA_PERF_FIXTURE, 'Initial detail context requires an owned synthetic fixture');
// Replay isolates browser/API/approval failures from inference. It is never model
// quality or latency evidence and is restricted to explicitly owned synthetic QA.
const replayFile = process.env.NOVA_PERF_REPLAY;
assert.ok(!replayFile || process.env.NOVA_PERF_FIXTURE && process.env.NOVA_PERF_PREWARM !== '1', 'Replay requires a synthetic fixture and no model prewarm');
const replayPath = replayFile ? path.resolve(root, replayFile) : null;
assert.ok(!replayPath || replayPath.toLowerCase().startsWith((path.join(root, 'docs/evidence') + path.sep).toLowerCase()), 'Replay must use owned QA evidence');
assert.ok(!replayPath || JSON.parse(fs.readFileSync(path.join(path.dirname(replayPath), 'environment.json'), 'utf8')).syntheticDataOnly === true, 'Replay evidence must be synthetic');
const replay = replayPath ? JSON.parse(fs.readFileSync(replayPath, 'utf8'))[0] : null;
// Respect the exclusive lease held by the existing .NET semantic diagnostics.
// Acquire before provider metadata/warmup or creating an API/browser. A shared
// Node handle does not serialize other Node harnesses; run those sequentially.
let modelLease = null;
if (!replay) {
  try { modelLease = fs.openSync(path.join(require('node:os').tmpdir(), 'QuickPrompt.Nova.SemanticModel.lease'), 'a+'); }
  catch (error) { throw new Error('MODEL_EVALUATION_BUSY: another diagnostic holds the Qwen lease. Preserve it and retry after it completes.', { cause: error }); }
}
const replayOffsets = new Map();
function recordedReply(property) {
  const replies = replay.inference.filter(item => item[property]);
  const index = replayOffsets.get(property) || 0;
  assert.ok(index < replies.length, 'Unexpected additional replay request: ' + property);
  replayOffsets.set(property, index + 1);
  return replies[index][property];
}
const sourceBuild = path.resolve(root, process.env.NOVA_TEST_BUILD || 'backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll');
const temp = path.join(root, '.playwright/latency-' + stamp), out = path.join(root, 'docs/evidence/latency-' + stamp);
fs.mkdirSync(temp, { recursive: true }); fs.mkdirSync(out, { recursive: true });
fs.cpSync(path.dirname(sourceBuild), path.join(temp, 'api'), { recursive: true });
fs.cpSync(path.resolve(root, process.env.NOVA_WEB_BUILD || 'apps/web/dist'), path.join(temp, 'web'), { recursive: true });
if (process.env.NOVA_PERF_FIXTURE) {
  const fixture = path.resolve(root, process.env.NOVA_PERF_FIXTURE);
  assert.ok(fixture.toLowerCase().startsWith((path.join(root, '.playwright') + path.sep).toLowerCase()), 'Only an owned synthetic fixture is permitted');
  fs.copyFileSync(fixture, path.join(temp, 'synthetic.sqlite'));
}
const origin = 'http://127.0.0.1:5236', headers = { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1' };
const chats = [], transport = [], results = [], errors = []; let proxy, server, browser, context, page, runId, active, faultPending = false, faultPersistent = false;
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const digest = bytes => createHash('sha256').update(bytes).digest('hex');
async function api(route, body) { const response = await fetch(origin + '/api/v1' + route, { method: body ? 'POST' : 'GET', headers, body: body ? JSON.stringify(body) : undefined }); assert.ok(response.ok, route + ':' + response.status); return response.status === 204 ? null : response.json(); }
async function until(read, timeout = 240000) { const end = Date.now() + timeout; while (Date.now() < end) { const item = await read(); if (item) return item; await wait(100); } throw Error('PROBE_TIMEOUT'); }
async function events(view) {
  const response = await fetch(origin + '/api/v1/nova/live/' + runId + '/events?after=0', { signal: AbortSignal.timeout(8000) });
  const reader = response.body.getReader(), decoder = new TextDecoder(); let buffer = '', items = [];
  try { while (items.at(-1)?.sequence < view.run.sequence || !items.length) { const chunk = await reader.read(); if (chunk.done) break; buffer += decoder.decode(chunk.value, { stream: true }); let end;
    while ((end = buffer.indexOf('\n\n')) >= 0) { const packet = buffer.slice(0, end); buffer = buffer.slice(end + 2); const data = packet.split('\n').find(line => line.startsWith('data: ')); if (data) items.push(JSON.parse(data.slice(6))); }
  } } finally { await reader.cancel().catch(() => {}); }
  return items.map(({ sequence, timestamp, stage, actor, toolName, status, durationMs, errorCode }) => ({ sequence, timestamp, stage, actor, toolName, status, durationMs, errorCode }));
}
const scenarios = {
  reliability: 'Encuentra el prompt que utilizo para redactar correos, conviértelo en una versión breve para solicitar una entrevista y déjalo listo para copiar. No modifiques el original.',
  interview: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y déjalo listo para copiar.',
  paraphrase: 'Localiza la plantilla de correos y prepara una versión para pedir una entrevista. Quiero poder copiarla.',
  search: 'Muéstrame el prompt guardado para redactar correos.',
  create: 'Crea un prompt para organizar una reunión de equipo con agenda, objetivos y acuerdos. No inventes información.',
  unsavedAdapt: 'Crea un prompt para organizar una reunión de equipo con agenda, objetivos y acuerdos. No inventes información.',
  unsavedSaveCopy: 'Crea un prompt para organizar una reunión de equipo con agenda, objetivos y acuerdos. No inventes información.',
  recoverUnsaved: 'Crea un prompt para organizar una reunión de equipo con agenda, objetivos y acuerdos. No inventes información.',
  variables: 'Completa el prompt Proyecto técnico con proyecto: un catálogo de productos con búsqueda. Déjalo listo para copiar.',
  boundVariables: 'Completa el prompt Proyecto técnico y déjalo listo para copiar.\nproyecto: un catálogo de productos con búsqueda',
  quickVariables: 'Acción rápida real: completar Proyecto técnico con el valor literal del formulario.',
  provider: 'Deja el prompt Redactar correos preparado para Claude sin modificarlo, ejecutarlo ni enviarlo.',
  local: 'Ejecuta mi prompt Resultado breve con Qwen local ahora.',
  selectedLocal: 'Ejecuta mi prompt Resultado breve con Qwen local ahora.',
  long: 'Adapta mi prompt Revisión extensa para analizar riesgos de mantenimiento y deja el borrador sin guardar.',
  selectedLong: 'Adapta mi prompt Revisión extensa para analizar riesgos de mantenimiento y deja el borrador sin guardar.',
  readOnly: 'Abre el prompt Reescribir anuncios y no cambies su contenido.',
  ambiguity: 'Busca mi prompt de anuncios. Si hay varios compatibles, pregúntame cuál usar antes de abrir uno.',
  notFound: 'Abre mi prompt guardado Protocolo de navegación submarina. Si no existe, indícalo sin elegir otro ni crear uno.',
  cancel: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y déjalo listo para copiar.',
  fault: 'Muéstrame el prompt guardado para redactar correos.',
  persistentFault: 'Muéstrame el prompt guardado para redactar correos.',
  approval: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y déjalo listo para copiar.',
  saveVersion: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y solicita mi aprobación para guardar el cambio como nueva versión del mismo prompt.',
  saveCopy: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y solicita mi aprobación para guardarlo como una copia separada. Conserva el original.',
  rejectSave: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y solicita mi aprobación para guardar el cambio como nueva versión del mismo prompt.',
  pauseResume: 'Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y déjalo listo para copiar.',
};
if (searchFixture) scenarios.search = searchFixture.message;
// Acceptance v2: original tasks stay frozen. Case data and subsequent workflow
// are specified independently, before any candidate UI execution.
const fieldCopyAcceptance = Object.freeze({
  variables: Object.freeze({ proyecto: 'un catálogo de productos con búsqueda.' }),
  boundVariables: Object.freeze({ proyecto: 'un catálogo de productos con búsqueda' }),
  quickVariables: Object.freeze({ proyecto: 'un catálogo de productos con búsqueda' }),
});
async function measure(index, scenario, source) {
  assert.ok(!replay || replay.scenario === scenario, 'Replay must preserve the recorded task scenario');
  replayOffsets.clear();
  if (fieldCopyAcceptance[scenario]) {
    const fixtures = (await api('/prompts?size=100')).items;
    source = fixtures.find(prompt => prompt.title === 'Proyecto técnico');
    assert.ok(source, 'Declared field-copy source fixture must exist before the request');
  }
  source = await api('/prompts/' + source.id);
  const versionReview = ['approval', 'saveVersion'].includes(scenario), copyReview = ['saveCopy', 'unsavedSaveCopy'].includes(scenario);
  const resourcesBefore = await resourceSnapshot();
  context = await browser.newContext({ viewport: { width: 1280, height: 900 }, recordVideo: { dir: path.join(out, 'video') } });
  // Exercise the real Copy control and exact compiled handoff without replacing the user's OS clipboard.
  // This boundary capture does not claim OS permission/clipboard-format integration coverage.
  await context.addInitScript(() => {
    let clipboard = '';
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => { clipboard = String(value); }, readText: async () => clipboard } });
  });
  const approvalTrace = [];
  const captureApproval = async response => {
    const ownerPage = page;
    if (response.request().method() === 'POST' && response.url() === origin + '/api/v1/nova/live/' + runId + '/commit') {
      try { const body = await response.json(); approvalTrace.push({ commitStatus: response.status(), detail: response.ok() ? null : body.detail }); } catch { /* Closed owned page. */ }
      return;
    }
    if (response.request().method() !== 'GET' || response.url() !== origin + '/api/v1/nova/live/' + runId) return;
    try {
      const view = await response.json(); if (view.run?.approval !== 'APPROVED') return;
      for (const ms of [0, 200, 800]) {
        await wait(ms); if (approvalTrace.length >= 24 || ownerPage.isClosed()) return;
        approvalTrace.push({ serverSequence: view.run.sequence, serverStatus: view.run.status, ui: await ownerPage.evaluate(() => {
          const live = document.querySelector('[data-testid="nova-live"]'), dock = document.querySelector('[data-testid="nova-dock"]');
          const controls = [...document.querySelectorAll('button')].filter(button => button.textContent.trim() === 'Aplicar versión aprobada' || button.getAttribute('aria-label') === 'Actividad de NOVA');
          return { live: live ? { ...live.dataset } : null, dockExpanded: dock?.getAttribute('data-expanded'), controls: controls.map(button => ({ label: button.getAttribute('aria-label') || button.textContent.trim(), expanded: button.getAttribute('aria-expanded'), shown: !!button.getClientRects().length, focused: document.activeElement === button })) };
        }) });
      }
    } catch { /* The owned page may finish/close while diagnostic snapshots are pending. */ }
  };
  const speed = process.env.NOVA_PERF_SPEED || 'normal';
  await context.addInitScript(speed => localStorage.setItem('nova-interface-v1', JSON.stringify({ view: 'cards', executionMode: 'SAME_TAB', rememberDraft: false, agentSpeed: speed })), speed);
  await context.addInitScript(() => {
    window.__novaPerf = { start: null, firstCursorMs: null, inputs: [], eventDeliveryMs: [] };
    const observer = new MutationObserver(() => { const probe = window.__novaPerf; if (probe.start === null || probe.firstCursorMs !== null) return; const cursor = document.querySelector('[data-testid="nova-agent-cursor"]'); if (cursor?.getClientRects().length) { probe.firstCursorMs = performance.now() - probe.start; observer.disconnect(); } });
    observer.observe(document, { subtree: true, childList: true });
    document.addEventListener('input', event => { const probe = window.__novaPerf, node = event.target; if (probe.start === null || probe.inputs.length >= 256 || !node.matches('input,textarea')) return; probe.inputs.push({ control: node.id || (node.closest('.library-search') ? 'library-search' : 'other'), characters: node.value.length, atMs: performance.now() - probe.start }); }, true);
    const NativeEventSource = window.EventSource;
    window.EventSource = class extends NativeEventSource {
      constructor(...args) { super(...args); this.addEventListener('activity', event => { const probe = window.__novaPerf; if (probe.start === null || probe.eventDeliveryMs.length >= 512) return; try { const value = JSON.parse(event.data); probe.eventDeliveryMs.push({ sequence: value.sequence, ms: Date.now() - Date.parse(value.timestamp) }); } catch {} }); }
    };
  });
  const externalRequests = [];
  context.on('request', request => { const url = new URL(request.url()); if (['http:', 'https:'].includes(url.protocol) && url.origin !== origin) externalRequests.push({ origin: url.origin, method: request.method() }); });
  page = await context.newPage(); page.on('pageerror', error => errors.push(error.message)); page.on('response', captureApproval);
  const network = []; page.on('requestfinished', request => { if (!active || !request.url().includes('/api/v1/')) return; const t = request.timing(); network.push({ method: request.method(), path: request.url().replace(origin, '').replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/g, ':id').split('?')[0], durationMs: t.responseEnd, responseStartMs: t.responseStart }); });
  await page.goto(origin);
  // Explicit user selection is a separate scenario, not a fallback that hides
  // failed source resolution from the library/home screen.
  const selectedTitle = scenario === 'selectedLocal' ? 'Resultado breve' : scenario === 'selectedLong' ? 'Revisión extensa' : null;
  let selectedPromptId = null;
  if (selectedTitle) { await page.getByRole('button', { name: selectedTitle, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor(); selectedPromptId = await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'); }
  let initialContextSource = null;
  if (initialSourceTitle) {
    assert.ok(!selectedTitle, 'Do not replace the frozen explicit-selection setup');
    await page.getByRole('button', { name: initialSourceTitle, exact: true }).click(); await page.getByTestId('prompt-focus').waitFor();
    initialContextSource = { title: initialSourceTitle, id: await page.getByTestId('prompt-focus').getAttribute('data-prompt-id'), revision: await page.getByTestId('prompt-focus').getAttribute('data-revision') };
  }
  await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
  if (scenario === 'quickVariables') {
    await page.getByRole('button', { name: 'Más acciones de NOVA', exact: true }).click();
    await page.getByRole('button', { name: 'Completar variables', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Completar variables', exact: true });
    await dialog.getByRole('textbox', { name: '¿Qué prompt?', exact: true }).fill('Proyecto técnico');
    await dialog.getByRole('button', { name: 'Proyecto técnico' }).click();
    await dialog.getByRole('textbox', { name: 'proyecto', exact: true }).fill(fieldCopyAcceptance[scenario].proyecto);
  } else await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(scenarios[scenario]);
  const originalLibrary = JSON.stringify(await api('/prompts?size=100'));
  const firstChat = chats.length, firstTransport = transport.length, started = performance.now(); active = true;
  faultPersistent = scenario === 'persistentFault'; faultPending = scenario === 'fault' || faultPersistent;
  await page.evaluate(() => { window.__novaPerf.start = performance.now(); });
  const pending = page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST');
  await page.getByRole('button', { name: scenario === 'quickVariables' ? 'Completar con NOVA' : 'Preparar prompt', exact: true }).click(); runId = (await (await pending).json()).runId;
  console.log('RUN', variant, scenario, index, runId);
  let result, cancelledAt = null, cancellationLatencyMs = null, approvedDraft = null, timeToReadyMs = null, rejectedProposal = false;
  let manualTouchPaused = null, pauseObserved = null, pausedFieldsStable = null; const precedingRuns = [];
  let unsavedCreation = null, recoveredDraft = null;
  async function ready() { return until(async () => { const view = await api('/nova/live/' + runId); return ['COMPLETED', 'AWAITING_APPROVAL', 'FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status) ? view : null; }); }
  try {
  if (scenario === 'cancel') {
    await until(() => chats.length > firstChat, 10000);
    await page.getByRole('button', { name: 'Actividad de NOVA', exact: true }).click();
    cancelledAt = performance.now(); await page.getByRole('button', { name: 'Detener NOVA', exact: true }).click();
  }
  if (scenario === 'pauseResume') {
    await until(() => chats.length > firstChat, 10000);
    await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).click();
    manualTouchPaused = (await api('/nova/live/' + runId)).run.pauseRequested;
    const activity = page.getByRole('button', { name: 'Actividad de NOVA', exact: true });
    if (await activity.getAttribute('aria-expanded') !== 'true') await activity.click();
    await page.getByRole('button', { name: 'Pausar', exact: true }).click();
    pauseObserved = !!await until(async () => (await api('/nova/live/' + runId)).run.pauseRequested, 5000);
    const fields = () => page.locator('#prompt-content,#prompt-title').evaluateAll(nodes => nodes.map(node => ({ id: node.id, value: node.value })));
    const pausedFields = JSON.stringify(await fields()); await wait(500);
    pausedFieldsStable = JSON.stringify(await fields()) === pausedFields;
    await page.getByRole('button', { name: 'Continuar', exact: true }).click();
    await until(async () => !(await api('/nova/live/' + runId)).run.pauseRequested, 5000);
  }
    result = await ready();
    if (['unsavedAdapt', 'recoverUnsaved', 'unsavedSaveCopy'].includes(scenario) && result.run.status === 'COMPLETED') {
      unsavedCreation = { runId, status: result.run.status, observation: result.observation, semantic: result.semantic };
      await page.screenshot({ path: path.join(out, `${scenario}-${index}-created.png`), fullPage: true });
      const remember = async () => precedingRuns.push({ runId, status: result.run.status, errorCode: result.run.errorCode, semantic: result.semantic,
        observation: result.observation, timeline: await events(result), performance: await (await fetch(origin + '/api/v1/nova/live/' + runId + '/performance')).json() });
      const followup = async message => {
        if (!await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).isVisible())
          await page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true }).click();
        await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(message);
        const pendingTurn = page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); runId = (await (await pendingTurn).json()).runId;
      };
      await remember();
      const message = 'Adapta este borrador para que la reunión termine con responsables y fechas de seguimiento. Conserva la regla de no inventar información.';
      if (scenario === 'recoverUnsaved') {
        const beforeInference = chats.length; await followup(message); await until(() => chats.length > beforeInference, 10000);
        const activity = page.getByRole('button', { name: 'Actividad de NOVA', exact: true });
        if (await activity.getAttribute('aria-expanded') !== 'true') await activity.click();
        await page.getByRole('button', { name: 'Detener NOVA', exact: true }).click(); result = await ready();
        recoveredDraft = { interruptedRun: runId, status: result.run.status, bodyBefore: unsavedCreation.observation?.templateText,
          bodyAfter: await page.locator('#prompt-content').inputValue() };
        await remember();
      }
      await followup(message); result = await ready();
      if (scenario === 'unsavedSaveCopy' && result.run.status === 'COMPLETED') {
        await remember();
        await followup('Solicita mi aprobación para guardar este borrador como una copia separada. Conserva su contenido y las variables sin rellenar.');
        result = await ready();
      }
    }
    if (cancelledAt !== null) cancellationLatencyMs = Math.round(performance.now() - cancelledAt);
    // Preserve the existing source-confirmation boundary: this is a QA human
    // selecting the known synthetic fixture, not a production resolution shortcut.
    if (scenario === 'paraphrase' && result.run.status === 'BLOCKED' && result.run.errorCode === 'NEEDS_CLARIFICATION'
      && result.semantic?.validationNotes?.includes('UNVERIFIED_NAMED_CONTENT_SOURCE')) {
      precedingRuns.push({ runId, status: result.run.status, errorCode: result.run.errorCode, timeline: await events(result), performance: await (await fetch(origin + '/api/v1/nova/live/' + runId + '/performance')).json() });
      await page.getByRole('textbox', { name: '¿Qué quieres hacer hoy?', exact: true }).fill(source.title);
      const pendingReply = page.waitForResponse(r => r.url() === origin + '/api/v1/nova/live' && r.request().method() === 'POST');
      await page.getByRole('button', { name: 'Preparar prompt', exact: true }).click(); runId = (await (await pendingReply).json()).runId;
      result = await ready();
    }
    if ((versionReview || copyReview || scenario === 'rejectSave') && result.run.status === 'AWAITING_APPROVAL') {
      timeToReadyMs = Math.round(performance.now() - started);
      // Do not approve an effect that differs from the synthetic request.
      const expectedCopy = copyReview;
      if (!result.proposal || (expectedCopy ? result.proposal.promptId !== null : result.proposal.promptId !== source.id))
        throw new Error('Requested persistence target differs from the proposal; approval withheld.');
      const activity = page.getByRole('button', { name: 'Actividad de NOVA', exact: true });
      if (await activity.getAttribute('aria-expanded') !== 'true') await activity.click();
      await page.getByRole('button', { name: expectedCopy ? 'Revisar y guardar copia' : 'Revisar y guardar versión', exact: true }).click();
      if (scenario === 'rejectSave' || process.env.NOVA_PERF_REJECT_SAVE === '1') {
        rejectedProposal = true; await page.getByRole('button', { name: 'Rechazar propuesta', exact: true }).click();
      } else {
        approvedDraft = result.proposal.draft;
        await page.getByRole('button', { name: expectedCopy ? 'Confirmar guardado de esta copia' : 'Confirmar guardado de esta versión', exact: true }).click();
      }
      result = await until(async () => { const view = await api('/nova/live/' + runId); return ['COMPLETED', 'FAILED', 'BLOCKED', 'CANCELLED'].includes(view.run.status) ? view : null; });
    }
  }
  catch (error) { result = await api('/nova/live/' + runId); errors.push(error.message); }
  const elapsedMs = Math.round(performance.now() - started); active = false;
  let copyReady = null, clipboardMatches = null, libraryBeforeManualCopy = null, manualCopyAt = null;
  if ((scenario === 'reliability' || fieldCopyAcceptance[scenario]) && ['COMPLETED', 'AWAITING_APPROVAL'].includes(result.run.status)) {
    if (fieldCopyAcceptance[scenario] && !result.observation?.providerOpen) {
      const closeDock = page.getByRole('button', { name: 'Cerrar panel de NOVA', exact: true });
      if (await closeDock.count()) await closeDock.click();
    }
    const copy = fieldCopyAcceptance[scenario] ? page.getByRole('button', { name: 'Copiar prompt', exact: true }) : page.getByRole('button', { name: 'Copiar borrador', exact: true });
    copyReady = await copy.count() === 1 && await copy.isEnabled();
    if (copyReady) {
      if (fieldCopyAcceptance[scenario]) libraryBeforeManualCopy = await api('/prompts?size=100');
      manualCopyAt = Date.now();
      await copy.click();
      clipboardMatches = await until(async () => await page.evaluate(() => navigator.clipboard.readText()) === result.observation?.compiledText, 5000).then(() => true).catch(() => false);
      if (fieldCopyAcceptance[scenario] && clipboardMatches) await until(async () => (await api('/prompts/' + source.id)).uses === source.uses + 1, 5000).catch(() => false);
    }
  }
  const timeline = await events(result), inference = chats.slice(firstChat), calls = transport.slice(firstTransport);
  const observation = result.observation;
  const actualText = await page.locator('#prompt-content').count() ? await page.locator('#prompt-content').inputValue() : await page.getByTestId('prompt-focus').count() ? await page.getByTestId('prompt-focus').getAttribute('data-template-text') : null;
  const nativeFields = await page.evaluate(() => ({
    title: document.querySelector('#prompt-title')?.value ?? document.querySelector('[data-testid="focus-title"]')?.textContent ?? null,
    values: Object.fromEntries([...document.querySelectorAll('[data-testid="variable-field"]')].map(field => [field.getAttribute('data-variable-name'), field.value])),
    compiled: document.querySelector('[data-testid="compiled-preview"]')?.textContent ?? null,
  }));
  const nativeTitleMatches = !observation || nativeFields.title === observation.title;
  const nativeValuesMatch = !observation || Object.entries(observation.values || {}).every(([name, value]) => nativeFields.values[name] === value);
  const nativePreviewMatches = !observation?.compiledReady || nativeFields.compiled === observation.compiledText;
  const nativeFieldsMatch = !observation || actualText === observation.templateText && nativeTitleMatches && nativeValuesMatch && nativePreviewMatches;
  const screen = await page.evaluate(() => ({ editor: !!document.querySelector('[data-testid="prompt-editor"]'), focus: !!document.querySelector('[data-testid="prompt-focus"]'), cards: document.querySelectorAll('[data-testid="library-prompt"]').length, visible: document.visibilityState }));
  const savedSource = await api('/prompts/' + source.id), currentLibrary = await api('/prompts?size=100');
  const unchanged = JSON.stringify(savedSource) === JSON.stringify(source) && JSON.stringify(currentLibrary) === originalLibrary;
  const copyUsageOnly = !!fieldCopyAcceptance[scenario] && clipboardMatches && JSON.stringify(libraryBeforeManualCopy) === originalLibrary
    && JSON.stringify({ ...savedSource, uses: source.uses, lastUsedAt: source.lastUsedAt }) === JSON.stringify(source)
    && savedSource.uses === source.uses + 1 && Date.parse(savedSource.lastUsedAt) >= manualCopyAt && Date.parse(savedSource.lastUsedAt) <= Date.now()
    && JSON.stringify({ ...currentLibrary, items: currentLibrary.items.map(item => item.id === source.id ? { ...item, uses: source.uses, lastUsedAt: source.lastUsedAt } : item) }) === originalLibrary;
  const versionWriteMatches = versionReview && !!approvedDraft && result.run.effectCommitted && savedSource.revision === source.revision + 1 && savedSource.templateText === approvedDraft.templateText
    && savedSource.title === approvedDraft.title && result.observation?.revision === savedSource.revision && actualText === savedSource.templateText
    && JSON.stringify(currentLibrary.items.filter(p => p.id !== source.id)) === JSON.stringify(JSON.parse(originalLibrary).items.filter(p => p.id !== source.id)) && currentLibrary.total === JSON.parse(originalLibrary).total;
  const originalItems = JSON.parse(originalLibrary).items;
  const savedCopy = copyReview && result.run.targetId ? currentLibrary.items.find(prompt => prompt.id === result.run.targetId) : null;
  const copyWriteMatches = copyReview && !!approvedDraft && result.run.effectCommitted && !!savedCopy && savedCopy.id !== source.id && savedCopy.revision === 1
    && !originalItems.some(prompt => prompt.id === savedCopy.id) && savedCopy.templateText === approvedDraft.templateText && savedCopy.title === approvedDraft.title
    && actualText === savedCopy.templateText && observation?.promptId === savedCopy.id && observation?.revision === savedCopy.revision
    && currentLibrary.total === JSON.parse(originalLibrary).total + 1 && JSON.stringify(savedSource) === JSON.stringify(source)
    && JSON.stringify(currentLibrary.items.filter(prompt => prompt.id !== savedCopy.id)) === JSON.stringify(originalItems);
  const authorizedWrite = !!(versionWriteMatches || copyWriteMatches);
  const normalized = (observation?.templateText || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
  const semanticCorrect = ['interview', 'paraphrase', 'approval', 'reliability', 'saveVersion', 'saveCopy', 'pauseResume'].includes(scenario)
    ? observation?.promptId === (copyReview ? savedCopy?.id : source.id) && (versionReview || copyReview ? authorizedWrite : observation?.draftChanged) && normalized.includes('entrevista') && observation?.compiledReady && actualText === observation?.templateText
      && observation?.templateText?.includes('No inventes nombres, fechas ni información personal.') && (scenario !== 'reliability' || observation.templateText.length < source.templateText.length)
    : scenario === 'search' || scenario === 'fault' ? observation?.promptId === source.id && observation?.templateText === source.templateText
    // A labeled value is literal through the end of the input, per the existing contract.
    : fieldCopyAcceptance[scenario] ? observation?.values?.proyecto === fieldCopyAcceptance[scenario].proyecto && observation?.compiledReady
      && observation?.promptId === source.id && observation?.templateText === source.templateText && copyReady && clipboardMatches
      && !observation?.providerOpen && result.semantic?.intentions?.includes(scenario === 'quickVariables' ? 'FILL_VARIABLES' : 'COPY_PROMPT')
      && result.semantic.intentions.every(operation => ['SEARCH_PROMPT', 'RESOLVE_PROMPT', 'OPEN_PROMPT', 'FILL_VARIABLES', 'COMPILE_PROMPT', 'COPY_PROMPT'].includes(operation))
    : scenario === 'create' ? observation?.mode === 'create' && !observation?.promptId && observation?.draftChanged && actualText === observation?.templateText
      && !result.semantic?.intentions?.some(operation => ['COPY_PROMPT', 'EXECUTE_LOCAL', 'PREPARE_LOCAL_EXECUTION', 'PREPARE_PROVIDER', 'REQUEST_SAVE_COPY', 'REQUEST_SAVE_VERSION', 'FILL_VARIABLES', 'EXPLAIN'].includes(operation))
    : ['unsavedAdapt', 'recoverUnsaved'].includes(scenario) ? unsavedCreation?.status === 'COMPLETED' && observation?.mode === 'create' && !observation?.promptId
      && actualText === observation?.templateText && observation?.templateText !== unsavedCreation.observation?.templateText
      && result.semantic?.intentions?.includes('PROPOSE_ADAPTATION') && !result.semantic?.intentions?.some(operation => ['COPY_PROMPT', 'CREATE_DRAFT', 'EXECUTE_LOCAL', 'REQUEST_SAVE_COPY', 'REQUEST_SAVE_VERSION'].includes(operation))
      && (scenario !== 'recoverUnsaved' || recoveredDraft?.status === 'CANCELLED' && recoveredDraft.bodyAfter === recoveredDraft.bodyBefore)
    : scenario === 'unsavedSaveCopy' ? unsavedCreation?.status === 'COMPLETED' && precedingRuns.some(run => run.semantic?.intentions?.includes('PROPOSE_ADAPTATION'))
      && authorizedWrite && savedCopy?.templateText === approvedDraft?.templateText && savedCopy?.title === approvedDraft?.title
    : scenario === 'provider' ? observation?.providerOpen && observation?.providerId === 'claude' && !observation?.responseComplete
    : scenario === 'local' || scenario === 'selectedLocal' ? observation?.responseComplete && observation?.response?.trim() === 'LISTO' && await page.getByTestId('local-qwen-response').textContent() === observation.response
    // Opening alone must not prepare a provider, mutate inputs, or add effects.
    : scenario === 'readOnly' ? observation?.title === 'Reescribir anuncios' && !observation?.draftChanged && !observation?.providerOpen
      && result.semantic?.desiredOutcome === 'READ' && result.semantic?.intentions?.length > 0
      && result.semantic.intentions.every(operation => ['SEARCH_PROMPT', 'RESOLVE_PROMPT', 'OPEN_PROMPT'].includes(operation))
    : scenario === 'long' || scenario === 'selectedLong' ? observation?.draftChanged && normalized.includes('mantenimiento') && observation.templateText.includes('No inventes datos.') && actualText === observation.templateText : false;
  const injectedFailures = inference.filter(call => call.outcome === 'INJECTED_HTTP_503').length;
  const expectedControl = scenario === 'rejectSave' || rejectedProposal ? rejectedProposal && result.run.status === 'CANCELLED' : scenario === 'cancel' ? result.run.status === 'CANCELLED' : scenario === 'persistentFault' ? result.run.status === 'FAILED' && result.run.errorCode === 'MODEL_UNAVAILABLE' && injectedFailures === 2 && inference.length === 2
    : ['ambiguity', 'notFound'].includes(scenario) ? result.run.status === 'BLOCKED' && result.run.errorCode === 'NEEDS_CLARIFICATION' && !!result.question && !observation?.promptId
      && !result.semantic?.intentions?.some(operation => ['CREATE_DRAFT', 'PROPOSE_ADAPTATION', 'EXECUTE_LOCAL', 'REQUEST_SAVE_COPY', 'REQUEST_SAVE_VERSION'].includes(operation)) : false;
  const selectedIdentityMatches = !selectedTitle || !!selectedPromptId && observation?.promptId === selectedPromptId && JSON.parse(originalLibrary).items.some(prompt => prompt.id === selectedPromptId && prompt.title === selectedTitle);
  const controlAndFieldsCorrect = (!!semanticCorrect && ['AWAITING_APPROVAL', 'COMPLETED'].includes(result.run.status) || expectedControl) && (scenario !== 'reliability' || copyReady && clipboardMatches)
    && (scenario !== 'pauseResume' || manualTouchPaused === false && pauseObserved === true && pausedFieldsStable === true && !result.run.pauseRequested)
    && nativeFieldsMatch && selectedIdentityMatches && (scenario !== 'fault' || injectedFailures === 1) && ((versionReview || copyReview) && !rejectedProposal ? authorizedWrite : (fieldCopyAcceptance[scenario] ? copyUsageOnly : unchanged) && !result.run.effectCommitted) && context.pages().length === 1 && page.url().startsWith(origin + '/') && !externalRequests.length;
  // A safe refusal does not prove the offered clarification is relevant. The
  // public question is free text, without typed candidate IDs: retain evidence
  // for independent review instead of declaring semantic success from BLOCKED.
  const referenceQuestionReview = ['ambiguity', 'notFound'].includes(scenario) ? {
    status: 'NOT_TESTED', controlSafetyPass: !!controlAndFieldsCorrect,
    expectedSources: scenario === 'ambiguity' ? JSON.parse(originalLibrary).items.filter(prompt => ['Anuncios A', 'Anuncios B'].includes(prompt.title)).map(({ id, title }) => ({ id, title })) : [],
    reason: 'Independent review must verify relevance of the question and offered sources; no natural-language pattern oracle is used',
  } : null;
  const authoredContent = authoredContentRequested(result.semantic, ...precedingRuns.map(run => run.semantic));
  const correct = caseAcceptance(!!controlAndFieldsCorrect, { questionReview: !!referenceQuestionReview, authoredContent });
  const record = { variant, inferenceMode: replay ? 'SYNTHETIC_REPLAY_UI_ONLY' : 'REAL_OLLAMA', scenario, index, runId, speed, elapsedMs, correct, referenceQuestionReview, status: result.run.status, errorCode: result.run.errorCode, sourceUnchanged: unchanged, pauseRequested: result.run.pauseRequested,
    controlCorrect: !!controlAndFieldsCorrect,
    contentAcceptance: { required: authoredContent, status: authoredContent ? 'NOT_TESTED' : 'NOT_APPLICABLE',
      reason: authoredContent ? 'UI equality and model self-review do not establish fidelity; independent review against the original request and source is required.' : null },
    customSearchFixture: searchFixture ? { message: searchFixture.message, sourceId: source.id, sourceTitle: source.title, sourceTextHash: digest(source.templateText) } : null,
    sessionId: result.run.sessionId, question: result.question, initialContextSource, entryPoint: scenario === 'quickVariables' ? 'REAL_QUICK_ACTION_FORM' : initialContextSource ? 'FREE_REQUEST_FROM_DETAIL' : 'FREE_REQUEST',
    continuityStage: ['unsavedAdapt', 'recoverUnsaved', 'unsavedSaveCopy'].includes(scenario) ? unsavedCreation ? 'FOLLOWUP_REACHED' : 'BLOCKED_BY_CREATION' : 'NOT_APPLICABLE',
    draftIdentityVerification: 'NOT_OBSERVABLE_IN_PUBLIC_LIVE_VIEW: UI checks content/session continuity; internal DraftRef identity requires transport tests',
    observedTemplateCharacters: observation?.templateText?.length, observedTextHash: observation?.templateText ? digest(observation.templateText) : null, actualEditorMatches: actualText === null ? null : actualText === observation?.templateText,
    nativeFieldsMatch, nativeTitleMatches, nativeValuesMatch, nativePreviewMatches, approvalTrace,
    fieldCopyVerification: fieldCopyAcceptance[scenario] ? { acceptanceVersion: 4, expected: fieldCopyAcceptance[scenario], actual: observation?.values,
      compiledText: observation?.compiledText, sourceId: source.id, sourceRevision: source.revision, sourceBody: source.templateText,
      observedSourceId: observation?.promptId, observedRevision: observation?.revision, observedBody: observation?.templateText,
      usageOnlyAfterManualCopy: copyUsageOnly, agentLeftLibraryUnchanged: JSON.stringify(libraryBeforeManualCopy) === originalLibrary } : null,
    syntheticPersistence: contractDiagnostics && (versionReview || copyReview || scenario === 'rejectSave') ? {
      sourceBefore: source, libraryBefore: JSON.parse(originalLibrary), libraryAfter: currentLibrary,
      approvedDraft, targetId: result.run.targetId, approval: result.run.approval, observation,
    } : null,
    semanticSummary: result.semantic ? { intentions: result.semantic.intentions, desiredOutcome: result.semantic.desiredOutcome, reference: result.semantic.targetReference, providerId: result.semantic.providerId,
      notes: result.semantic.validationNotes?.filter(note => /^[A-Z_0-9]+$/.test(note)) } : null,
    precedingRuns, unsavedCreation, recoveredDraft, automatedSourceConfirmation: scenario === 'paraphrase' && precedingRuns.length > 0, automatedApproval: !!approvedDraft, rejectedProposal, authorizedWrite, manualTouchPaused, pauseObserved, pausedFieldsStable, timeToReadyMs, cancellationLatencyMs, copyReady, clipboardMatches, clipboardMode: 'ISOLATED_BROWSER_CAPTURE', osClipboardIntegration: 'NOT_TESTED',
    selectedTitle, selectedPromptId, modelCalls: replay ? 0 : inference.length, replayedProtocolResponses: replay ? inference.length : 0, inference, calls, timeline, network, screen, browserObservation: await page.evaluate(() => window.__novaPerf), browserPages: context.pages().length, externalRequests,
    resourcesBefore, resourcesAfter: await resourceSnapshot() };
  const diagnostics = await fetch(origin + '/api/v1/nova/live/' + runId + '/performance'); if (diagnostics.ok) record.performance = await diagnostics.json();
  const expectedRunIds = [runId, ...precedingRuns.map(run => run.runId)];
  record.modelCorrelation = { supported: inference.some(call => call.runId), unscoped: inference.filter(call => !call.runId).length, foreign: inference.filter(call => call.runId && !expectedRunIds.includes(call.runId)).length };
  record.inferenceCorrelationValid = record.modelCorrelation.supported && !record.modelCorrelation.unscoped && !record.modelCorrelation.foreign;
  record.latencyComparison = 'NOT_ESTABLISHED: run-ID correlation alone does not establish comparable fixtures, model state, resource conditions or success criteria across runs';
  record.faultInjected = scenario === 'fault' || scenario === 'persistentFault';
  record.injectedFailures = injectedFailures;
  record.faultMode = scenario === 'fault' ? 'ONE_TRANSIENT_HTTP_503' : scenario === 'persistentFault' ? 'PERSISTENT_HTTP_503' : null;
  if (referenceQuestionReview && controlAndFieldsCorrect && result.question) {
    await page.getByRole('button', { name: 'Ver última actividad de NOVA', exact: true }).click();
    const question = page.locator('.nova-live-popover').getByText(result.question, { exact: true }).first();
    await question.waitFor({ state: 'visible' }); await question.scrollIntoViewIfNeeded();
    record.referenceQuestionReview.userVisible = true;
    record.referenceQuestionReview.access = 'Opened the existing NOVA activity control, without selecting a source or changing the request';
  }
  results.push(record); fs.writeFileSync(path.join(out, 'measurements.json'), JSON.stringify(results, null, 2));
  await page.screenshot({ path: path.join(out, scenario + '-' + index + '.png'), animations: 'disabled', timeout: 10000 }).catch(() => { record.screenshotUnavailable = true; });
  fs.writeFileSync(path.join(out, 'measurements.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify({ scenario, index, elapsedMs, correct, status: record.status, error: record.errorCode, inferenceMode: record.inferenceMode, llmCalls: record.modelCalls, llmMs: replay ? null : inference.reduce((s, c) => s + c.durationMs, 0) }));
  if (!['COMPLETED', 'FAILED', 'BLOCKED', 'CANCELLED'].includes(result.run.status)) await api('/nova/live/' + runId + '/cancel', {});
  runId = null; await context.close(); context = null;
}
(async () => {
  const environment = { variant, inferenceMode: replay ? 'SYNTHETIC_REPLAY_UI_ONLY' : 'REAL_OLLAMA', syntheticDataOnly: true, modelContentLogged: contractDiagnostics, loggedContentScope: contractDiagnostics ? 'SYNTHETIC_CONTRACTS_AND_TEMPLATE_PROPOSALS_NO_THINKING' : 'NONE', measuredAt: new Date().toISOString(), count, snapshot: path.relative(root, temp), extraOptions: process.env.NOVA_PERF_OPTIONS ? JSON.parse(process.env.NOVA_PERF_OPTIONS) : [],
    ollama: await (await fetch('http://127.0.0.1:11434/api/version')).json(), before: await (await fetch('http://127.0.0.1:11434/api/ps')).json(),
    show: await (await fetch('http://127.0.0.1:11434/api/show', { method: 'POST', body: JSON.stringify({ model: 'qwen3:8b' }) })).json(),
    installedModel: (await (await fetch('http://127.0.0.1:11434/api/tags')).json()).models.find(model => model.name === 'qwen3:8b'),
    hashes: Object.fromEntries(['QuickPrompt.Api.dll', 'QuickPrompt.Application.dll', 'QuickPrompt.Infrastructure.dll', 'QuickPrompt.Domain.dll'].map(file => [file, digest(fs.readFileSync(path.join(temp, 'api', file)))])),
    webHashes: Object.fromEntries(['index.html', ...fs.readdirSync(path.join(temp, 'web/assets')).filter(file => /\.(js|css)$/.test(file)).map(file => 'assets/' + file)].map(file => [file, digest(fs.readFileSync(path.join(temp, 'web', file)))])) };
  // Model metadata is public, but the verbose license/template is not useful for measurement.
  delete environment.show.license; delete environment.show.template; delete environment.show.modelfile;
  fs.writeFileSync(path.join(out, 'environment.json'), JSON.stringify(environment, null, 2));
  if (process.env.NOVA_PERF_PREWARM === '1') {
    const packet = { model: 'qwen3:8b', messages: [], stream: false, options: { num_ctx: 12288 }, keep_alive: '5m' }, started = performance.now();
    const response = await fetch('http://127.0.0.1:11434/api/chat', { method: 'POST', body: JSON.stringify(packet) }); assert.ok(response.ok); const usage = await response.json();
    environment.prewarm = { packet, elapsedMs: Math.round(performance.now() - started), loadMs: typeof usage.load_duration === 'number' ? usage.load_duration / 1e6 : null, done: usage.done, doneReason: usage.done_reason, outputTokens: usage.eval_count ?? 0 };
    fs.writeFileSync(path.join(out, 'environment.json'), JSON.stringify(environment, null, 2));
  }
  proxy = http.createServer((request, response) => {
    if (replay && request.url === '/api/chat') {
      const bytes = []; request.on('data', chunk => bytes.push(chunk)); request.on('end', () => {
        try {
          const packet = JSON.parse(Buffer.concat(bytes)), properties = packet.format?.properties;
          assert.ok(!packet.stream && properties, 'Unexpected replay protocol');
          let content;
          if (properties.authoring || properties.requestedResults) content = recordedReply('syntheticContract');
          else if (properties.templateText) content = { description: '', reason: 'Recorded synthetic fixture; no model quality claim.', ...recordedReply('syntheticPromptProposal') };
          else if (properties.text) { const proposal = recordedReply('syntheticProposal'); content = { text: proposal.text, variablesPreserved: proposal.variablesPreserved, reasons: ['Recorded synthetic fixture'], changes: ['Recorded content change'], warnings: [] }; }
          else if (properties.appliesRequestedChange) content = recordedReply('syntheticFidelityReview');
          else throw Error('Unexpected replay contract');
          chats.push({ path: request.url, durationMs: 0, outcome: 'SYNTHETIC_REPLAY', runId: request.headers['x-nova-run-id'] ?? null });
          response.writeHead(200, { 'Content-Type': 'application/json' }); response.end(JSON.stringify({ done: true, model: packet.model, message: { content: JSON.stringify(content) } }));
        } catch { response.writeHead(500); response.end('UNEXPECTED_SYNTHETIC_REPLAY'); }
      }); return;
    }
    if (request.url === '/api/chat' && faultPending) { if (!faultPersistent) faultPending = false; request.resume(); chats.push({ path: request.url, durationMs: 0, outcome: 'INJECTED_HTTP_503', runId: request.headers['x-nova-run-id'] ?? null }); response.writeHead(503); response.end(); return; }
    const start = performance.now(), input = [], chunks = []; let firstByteMs = null;
    const metric = request.url === '/api/chat' ? { path: request.url, outcome: 'PENDING', startedAt: new Date().toISOString(), runId: request.headers['x-nova-run-id'] ?? null } : null; if (metric) chats.push(metric);
    request.on('data', chunk => { if (request.url === '/api/chat') input.push(chunk); });
    const upstream = http.request({ hostname: '127.0.0.1', port: 11434, path: request.url, method: request.method, headers: { ...request.headers, host: '127.0.0.1:11434' } }, remote => {
      response.writeHead(remote.statusCode || 502, remote.headers);
      remote.on('data', chunk => { firstByteMs ??= performance.now() - start; if (request.url === '/api/chat') chunks.push(chunk); }); remote.pipe(response);
      remote.on('end', () => {
        const timing = { path: request.url, durationMs: Math.round(performance.now() - start), status: remote.statusCode }; transport.push(timing);
        if (request.url !== '/api/chat') return;
        let usage = {}; try { const packet = JSON.parse(Buffer.concat(input)), raw = Buffer.concat(chunks).toString(), body = JSON.parse(packet.stream ? raw.trim().split(/\r?\n/).at(-1) : raw);
          usage = { model: packet.model, options: packet.options, thinking: packet.think, keepAlive: packet.keep_alive ?? null, streaming: packet.stream, firstByteMs: Math.round(firstByteMs), firstTokenMs: null, firstTokenUnavailableReason: packet.stream ? 'See correlated backend firstModelTokenMs' : 'non-streaming response',
            requestHash: digest(Buffer.concat(input)), inputBytes: Buffer.concat(input).length, promptTokens: body.prompt_eval_count, outputTokens: body.eval_count,
            loadMs: typeof body.load_duration === 'number' ? body.load_duration / 1e6 : null, promptEvaluationMs: typeof body.prompt_eval_duration === 'number' ? body.prompt_eval_duration / 1e6 : null, generationMs: typeof body.eval_duration === 'number' ? body.eval_duration / 1e6 : null, ollamaTotalMs: typeof body.total_duration === 'number' ? body.total_duration / 1e6 : null, doneReason: body.done_reason };
          if (contractDiagnostics && !packet.stream && packet.messages?.at(-1)?.content?.length < 32000) {
            // Explicitly owned synthetic traffic only. Observe the actual UI context
            // and proposed calls; never store thinking or relabel proposals as effects.
            let resource; try { resource = JSON.parse(packet.messages.at(-1).content); } catch {}
            if (resource?.memory && resource?.observation && resource?.capabilities) usage.syntheticUiResources = {
              memory: resource.memory, observation: resource.observation, capabilities: resource.capabilities,
              guideHash: digest(Buffer.from(packet.messages[0].content)),
              proposedCalls: body.message?.tool_calls ?? [],
              schemaSelection: !packet.tools && body.message?.content?.length < 16000 ? body.message.content : null
            };
          }
          if (contractDiagnostics && !packet.stream && body.message?.content?.length < 16000
              && (packet.format?.properties?.targets || packet.format?.properties?.authoring || packet.format?.properties?.authoringRequested || packet.format?.properties?.values
                  || packet.format?.properties?.requestType || packet.format?.anyOf?.some(branch => branch.properties?.requestType))) {
            usage.syntheticScopeContract = JSON.parse(body.message.content);
          }
          if (contractDiagnostics && !packet.stream && body.message?.content?.length < 16000
              && (packet.format?.properties?.requestType || packet.format?.anyOf?.some(branch => branch.properties?.requestType))) {
            usage.syntheticTextGoal = { packet: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content),
              guideHash: digest(Buffer.from(packet.messages[0].content)), schemaHash: digest(Buffer.from(JSON.stringify(packet.format))) };
          }
          if (contractDiagnostics && !packet.stream && (packet.format?.properties?.targetObject?.enum?.includes('INSTRUCTION_ARTIFACT') || packet.format?.properties?.requestedOperations)
              && body.message?.content?.length < 16000) usage.syntheticAuthoringScope = {
            packet: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content)
          };
          if (contractDiagnostics && packet.format?.properties?.authoring && !packet.stream && body.message?.content?.length < 16000) {
            usage.syntheticAuthoring = { packet: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && packet.format?.properties?.templateText && !packet.format.properties.decision
              && !packet.stream && body.message?.content?.length < 64000) {
            usage.syntheticDefinition = { packet: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && (packet.format?.properties?.effects || packet.format?.properties?.clauses || packet.format?.properties?.requestedResults) && !packet.stream && body.message?.content?.length < 64000) {
            const contract = JSON.parse(body.message.content), allowed = ['source', 'requestedResults', 'authoring', 'effects', 'forbidden', 'templateEdit', 'clauses', 'shortening', 'constraints', 'contentChange', 'targetReference', 'resolvedTargetId', 'sourceSearchText', 'clarificationQuestion', 'providerId', 'continuesPendingTask', 'explanation'];
            usage.syntheticContract = Object.fromEntries(allowed.filter(key => Object.hasOwn(contract, key)).map(key => [key, contract[key]]));
            try { const feedback = JSON.parse(packet.messages.at(-1).content).validationFeedback;
              if (typeof feedback === 'string' && feedback.length < 6000) usage.syntheticValidationFeedback = feedback;
            } catch {}
          }
          if (contractDiagnostics && packet.format?.anyOf?.some(branch => branch.properties?.kind) && !packet.stream && body.message?.content?.length < 16000) {
            usage.syntheticSourceReference = JSON.parse(body.message.content);
          }
          if (contractDiagnostics && packet.format?.properties?.access && packet.format?.properties?.save
              && !packet.stream && body.message?.content?.length < 16000) {
            usage.syntheticWorkflow = { request: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && !packet.stream && body.message?.content?.length < 16000
              && (packet.format?.properties?.promptIds || packet.format?.properties?.matches || packet.format?.properties?.sourceRole || packet.format?.properties?.searchText
                  || packet.format?.anyOf?.some(branch => branch.properties?.matchStatus))) {
            // Only this helper's explicitly owned synthetic traffic. Preserve the
            // original reference/selection packets, never model thinking.
            usage.syntheticReferenceResolution = { request: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && packet.format?.properties?.userProhibitions && !packet.stream && body.message?.content?.length < 16000) {
            // Diagnose literal-authority failures on this owned synthetic fixture;
            // keep structured evidence only, never the model's private reasoning.
            usage.syntheticConstraintExtraction = { request: JSON.parse(packet.messages.at(-1).content), response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && packet.format?.properties?.action && packet.format?.properties?.providerId && !packet.stream && body.message?.content?.length < 16000) {
            const destinationPacket = JSON.parse(packet.messages.at(-1).content);
            usage.syntheticDestination = { directive: destinationPacket.currentUserDirective,
              knowledge: destinationPacket.systemCapabilities ? { status: destinationPacket.systemCapabilities.status, catalogVersion: destinationPacket.systemCapabilities.catalogVersion,
                nodeIds: destinationPacket.systemCapabilities.nodes.map(node => node.id) } : null, response: JSON.parse(body.message.content) };
          }
          if (contractDiagnostics && packet.format?.properties?.templateText && !packet.stream && body.message?.content?.length < 64000) {
            const proposal = JSON.parse(body.message.content);
            usage.syntheticPromptProposal = { decision: proposal.decision, sourceId: proposal.sourceId, title: proposal.title, templateText: proposal.templateText, requiredInputs: proposal.requiredInputs, values: proposal.values, clarification: proposal.clarification };
          }
          if (contractDiagnostics && packet.format?.properties?.text && !packet.stream && body.message?.content?.length < 64000) {
            const proposal = JSON.parse(body.message.content);
            if (typeof proposal.text === 'string') usage.syntheticProposal = { text: proposal.text, characters: proposal.text.length, variablesPreserved: proposal.variablesPreserved };
            try { usage.syntheticGenerationBudget = JSON.parse(packet.messages.at(-1).content).generationBudget; } catch {}
          }
          if (contractDiagnostics && (packet.format?.properties?.appliesRequestedChange || packet.format?.properties?.verdict && packet.format?.properties?.instructionScope) && !packet.stream && body.message?.content?.length < 16000) {
            usage.syntheticFidelityReview = JSON.parse(body.message.content);
            usage.syntheticFidelityPacket = JSON.parse(packet.messages.at(-1).content);
          }
        } catch {} Object.assign(metric, { ...timing, ...usage, outcome: 'RESPONSE' });
      });
    }); upstream.on('error', () => { if (!response.headersSent) response.writeHead(502); response.end(); }); response.on('close', () => { if (metric?.outcome === 'PENDING') Object.assign(metric, { outcome: 'CANCELLED_OR_ERROR', durationMs: Math.round(performance.now() - start) }); upstream.destroy(); }); request.pipe(upstream);
  });
  await new Promise((resolve, reject) => { proxy.once('error', reject); proxy.listen(5237, '127.0.0.1', resolve); });
  let ready = false; server = spawn('dotnet', [path.join(temp, 'api/QuickPrompt.Api.dll'), '--QuickPrompt:Port=5236', '--QuickPrompt:Ollama=http://127.0.0.1:5237', '--QuickPrompt:DataPath=' + path.join(temp, 'synthetic.sqlite'), '--QuickPrompt:WebRoot=' + path.join(temp, 'web'), '--QuickPrompt:Preparation:ReuseInference=false', ...(debugPointsEnabled ? ['--QuickPrompt:Diagnostics:DebugPoints=true'] : []), ...(process.env.NOVA_PERF_OPTIONS ? JSON.parse(process.env.NOVA_PERF_OPTIONS) : [])], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  let debugBuffer = '';
  server.stdout.on('data', bytes => {
    if (String(bytes).includes('Now listening on: ' + origin)) ready = true;
    if (debugPointsEnabled) {
      debugBuffer += String(bytes); let end;
      while ((end = debugBuffer.indexOf('\n')) >= 0) {
        const line = debugBuffer.slice(0, end).trim(); debugBuffer = debugBuffer.slice(end + 1);
        if (line.startsWith('NOVA_DEBUG_POINT ') && debugPoints.length < 256) debugPoints.push(JSON.parse(line.slice(17)));
      }
    }
  }); server.stderr.on('data', () => {});
  await until(() => { if (server.exitCode !== null) throw Error('Owned API exited'); return ready; }, 20000);
  const selected = (process.env.NOVA_PERF_SCENARIOS || 'interview').split(','); assert.ok(selected.every(s => scenarios[s]));
  const existing = (await api('/prompts?size=100')).items;
  const sourceSeed = searchFixture?.source ?? { title: 'Redactar correos', description: 'Correspondencia profesional.', templateText: 'Redacta un correo profesional, breve y respetuoso. Usa únicamente los datos proporcionados. No inventes nombres, fechas ni información personal.' };
  const source = existing.find(p => p.title === sourceSeed.title) ?? await api('/prompts', sourceSeed);
  if (searchFixture) assert.equal(source.templateText, sourceSeed.templateText, 'Synthetic source must match the declared fixture');
  if (!existing.some(p => p.title === 'Proyecto técnico')) await api('/prompts', { title: 'Proyecto técnico', description: 'Análisis técnico de proyectos.', templateText: 'Revisa el proyecto <proyecto> y enumera tres mejoras. No inventes datos.' });
  if (selected.some(s => ['local', 'selectedLocal'].includes(s))) await api('/prompts', { title: 'Resultado breve', templateText: 'Responde exactamente LISTO, sin otros caracteres.' });
  if (selected.some(s => ['long', 'selectedLong'].includes(s))) await api('/prompts', { title: 'Revisión extensa', templateText: 'Evalúa el material proporcionado y presenta conclusiones verificables. ' + 'Explica los criterios, las alternativas consideradas y las consecuencias de cada decisión. '.repeat(12) + 'No inventes datos.' });
  if (selected.includes('readOnly')) await api('/prompts', { title: 'Reescribir anuncios', templateText: 'Resume las características proporcionadas. No inventes información.' });
  if (selected.includes('ambiguity')) for (const title of ['Anuncios A', 'Anuncios B']) if (!existing.some(prompt => prompt.title === title))
    await api('/prompts', { title, templateText: 'Redacta un anuncio con los datos proporcionados. No inventes datos.' });
  browser = await chromium.launch({ channel: 'msedge', headless: false });
  for (let index = 1; index <= count; index++) for (const scenario of selected) await measure(index, scenario, source);
  const expectedCases = Array.from({ length: count }, (_, index) => selected.map(scenario => ({ scenario, index: index + 1 }))).flat();
  const acceptance = campaignAcceptance(results, errors, expectedCases);
  fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify({ ...acceptance, inferenceMode: replay ? 'SYNTHETIC_REPLAY_UI_ONLY' : 'REAL_OLLAMA', variant, results: results.length, errors, after: await (await fetch('http://127.0.0.1:11434/api/ps')).json() }, null, 2));
  process.exitCode = acceptance.exitCode;
  console.log('EVIDENCE', out);
})().catch(async error => { console.error(error); fs.writeFileSync(path.join(out, 'failure.txt'), error.stack); process.exitCode = 1; }).finally(async () => {
  if (runId && server?.exitCode === null) await api('/nova/live/' + runId + '/cancel', {}).catch(() => {});
  await context?.close().catch(() => {}); await browser?.close().catch(() => {});
  if (server?.exitCode === null) { server.kill(); await new Promise(resolve => server.once('exit', resolve)); }
  if (debugPointsEnabled) fs.writeFileSync(path.join(out, 'debug-points.json'), JSON.stringify(debugPoints, null, 2));
  if (proxy) await new Promise(resolve => proxy.close(resolve));
  if (modelLease !== null) fs.closeSync(modelLease);
});
