import { useApplicationServices } from '../components/ServiceContext';
import { WorkspaceSidebar } from '../components/WorkspaceSidebar';
import { WorkspaceFooter } from '../components/WorkspaceFooter';
import { EmptyState, ErrorState, PromptSkeleton } from '../components/ui/Feedback';
import { canvasMotion, stillCanvasMotion } from '../design/motion';
import { AppIcon } from '../components/ui/Icon';
import { useEffect, useMemo, useRef, useState } from 'react';
import { lazy, Suspense } from 'react';
import { AnimatePresence, domAnimation, LazyMotion, m, MotionConfig } from 'motion/react';
import { useMotionPreference } from '../hooks/useMotionPreference';
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { Dialog } from '../components/ui/Dialog';
import { InlinePromptEditor } from '../features/prompts/InlinePromptEditor';
import { PromptFocus } from '../features/prompts/PromptFocus';
import { PromptStream } from '../features/prompts/PromptStream';
import { LibraryToolbar } from '../features/prompts/LibraryToolbar';
import { usePromptDeletion } from '../features/prompts/usePromptDeletion';
import { createEditorDraftSession } from '../features/prompts/EditorDraftSession';
import { createEditorContextPreparer } from '../features/prompts/EditorContextPreparation';
import { useLibrary, type Filter } from '../hooks/useLibrary';
import { SameTabAgent } from '../features/nova/SameTabAgent';
import { NovaLive, type NovaRuntimeSnapshot } from '../features/nova/NovaLive';
import { AgentControl } from '../features/nova/AgentControl';
import { QuickActions } from '../features/nova/QuickActions';
import { AgentDock } from '../features/nova/AgentDock';
import { NovaWelcome } from '../features/nova/NovaWelcome';
import { AgentUiProvider } from '../features/nova/AgentUiContext';
import { InterfaceSettings } from '../features/settings/InterfaceSettings';
import { useInterfacePreferences } from '../hooks/useInterfacePreferences';
import { useInterfaceTheme } from '../hooks/useInterfaceTheme';
import { EditorPreparation } from '../features/prompts/EditorPreparation';
import { type PromptAction } from '../services/library';
import { type PreparedDraft } from '../services/preparation';
import { terminalLive, type LiveView, type LiveCommand, type AuthoringBriefReply } from '../services/live';

import { errorMessage, type Group, type Prompt, type PromptDraft } from '../services/api';

const ProviderPicker = lazy(() => import('../features/providers/ProviderPicker').then(module => ({ default: module.ProviderPicker })));
const GroupSelector = lazy(() => import('../features/groups/GroupSelector').then(module => ({ default: module.GroupSelector })));
const LibraryTools = lazy(() => import('../features/settings/LibraryTools').then(module => ({ default: module.LibraryTools })));
const LearningCenter = lazy(() => import('../features/help/LearningCenter').then(module => ({ default: module.LearningCenter })));
const PreparedUse = lazy(() => import('../features/nova/PreparedUse').then(module => ({ default: module.PreparedUse })));
const PromptVersions = lazy(() => import('../features/prompts/PromptVersions').then(module => ({ default: module.PromptVersions })));
const SharePrompt = lazy(() => import('../features/prompts/SharePrompt').then(module => ({ default: module.SharePrompt })));
const CompilationHistory = lazy(() => import('../features/prompts/CompilationHistory').then(module => ({ default: module.CompilationHistory })));

const blank: PromptDraft = { title: '', templateText: '', description: '', categoryId: 0 };
const noValues: Record<string, string> = {};
export default function App() {
  return <AgentUiProvider><ApplicationView /></AgentUiProvider>;
}

function ApplicationView() {
  const { prompts, templates, collections, preparation, live, visibleSession } = useApplicationServices();
  const editorContext = useMemo(() => createEditorContextPreparer(templates, preparation), [templates, preparation]);
  const { observeOperation, startVisible } = visibleSession;
  const [workerRunId] = useState(() => { const id = new URLSearchParams(location.search).get('novaRun'); return id && /^[0-9a-f-]{36}$/i.test(id) ? id : null; });
  const [liveRunId, setLiveRunId] = useState<string | null>(() => { if (workerRunId) return workerRunId; try { const id = sessionStorage.getItem('nova-live-run'); return id && /^[0-9a-f-]{36}$/i.test(id) ? id : null; } catch { return null; } });
  const [inputRequest, setInputRequest] = useState<{ runId: string; message: string; source?: Prompt; command: LiveCommand } | null>(null);
  useEffect(() => { if (liveRunId) try { sessionStorage.setItem('nova-live-run', liveRunId); } catch { /* Only a run ID is retained; no request or values. */ } }, [liveRunId]);
  useEffect(() => { const uncertain = (event: Event) => { const id = (event as CustomEvent<string>).detail; if (/^[0-9a-f-]{36}$/i.test(id)) setLiveRunId(id); }; window.addEventListener('nova-uncertain-start', uncertain); return () => window.removeEventListener('nova-uncertain-start', uncertain); }, []);
  const [sameTabActive, setSameTabActive] = useState(false);
  const [runtimeSnapshot, setRuntimeSnapshot] = useState<NovaRuntimeSnapshot | null>(null);
  const runtime = runtimeSnapshot?.view.run.runId === liveRunId ? runtimeSnapshot : null;
  const controlling = sameTabActive || !!workerRunId && !!runtime && !terminalLive(runtime.view.run.status);
  const [approvedRunId, setApprovedRunId] = useState<string | null>(null);
  const [preferences, setPreferences] = useInterfacePreferences();
  const reducedMotion = useMotionPreference(preferences.decorativeMotion);
  const agentSpeed = preferences.agentSpeed;
  const [agentNotice, setAgentNotice] = useState('');
  const executionRunId = workerRunId ?? (sameTabActive ? liveRunId : null);
  const [editorUse, setEditorUse] = useState<PreparedDraft | null>(null);
  const [theme, setTheme] = useInterfaceTheme();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [groupId, setGroupId] = useState('');
  const [groups, setGroups] = useState<Group[]>([]);
  const [page, setPage] = useState(0);
  const [version, setVersion] = useState(0);
  const [selected, setSelected] = useState<Prompt | null>(null);
  const [mode, setMode] = useState<'discover' | 'focus' | 'create'>('discover');
  const [draft, setDraft] = useState<PromptDraft>(blank);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [newDraftValues, setNewDraftValues] = useState<Record<string, string>>({});
  const [valuesByPrompt, setValuesByPrompt] = useState<Record<string, Record<string, string>>>({});
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const { target: deleteTarget, review: deletionReview, ready: deletionReady, open: quickRemove, confirm: remove, cancel: cancelDeletion } = usePromptDeletion(prompts, live, {
    saving, setSaving, setError, ensureAvailable: ensureQuickActionAvailable,
    onDeleted: target => { if (selected?.id === target.id) setSelected(null); discover(); setVersion(v => v + 1); setToast('Prompt eliminado.'); },
  });
  const [helpOpen, setHelpOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [toast, setToastText] = useState('');
  const [toastSequence, setToastSequence] = useState(0);
  function setToast(text: string) { setToastText(text); setToastSequence(value => value + 1); }
  const [providerOpen, setProviderOpen] = useState(false);
  const [groupsOpen, setGroupsOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);
  const [dataOpen, setDataOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const sessionId = useRef(crypto.randomUUID());
  const opening = useRef<AbortController | null>(null);
  const quickLaunching = useRef(false);
  const [editorDrafts] = useState(createEditorDraftSession);
  const library = useLibrary(search, filter, groupId, page, version);
  const [showSkeleton, setShowSkeleton] = useState(false);
  useEffect(() => {
    setShowSkeleton(false);
    if (!library.loading) return;
    const timer = setTimeout(() => setShowSkeleton(true), 250);
    return () => clearTimeout(timer);
  }, [library.loading]);
  useEffect(() => { collections.list().then(setGroups).catch(() => {}); }, [version]);
  useEffect(() => {
    const listener = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key === 'k') { event.preventDefault(); window.dispatchEvent(new Event('nova-open-assistant')); }
    };
    document.addEventListener('keydown', listener); return () => document.removeEventListener('keydown', listener);
  }, []);
  useEffect(() => { if (toast) { const timer = setTimeout(() => setToastText(''), 5000); return () => clearTimeout(timer); } }, [toast, toastSequence]);
  useEffect(() => () => opening.current?.abort(), []);
  function discover() { opening.current?.abort(); setMode('discover'); setError(''); }
  function back() { if (mode === 'create') setDiscardOpen(true); else discover(); }
  async function open(prompt: Prompt) {
    opening.current?.abort(); const current = new AbortController(); opening.current = current;
    setError('');
    try { const fresh = await prompts.get(prompt.id, current.signal); if (!current.signal.aborted) { setSelected(fresh); setMode('focus'); } }
    catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
  }
  function create() { opening.current?.abort(); const cached = editorDrafts.read(null); if (!cached) setNewDraftValues({}); setEditingId(null); setDraft(cached ?? { ...blank }); setMode('create'); setError(''); }
  function changeDraft(next: PromptDraft) { editorDrafts.remember(editingId, next); setDraft(next); }
  function edit() { if (!selected) return; setEditingId(selected.id); setDraft(editorDrafts.read(selected) ?? { title: selected.title, templateText: selected.templateText, description: selected.description, categoryId: selected.categoryId, revision: selected.revision }); setMode('create'); setError(''); }
  async function save() {
    if (executionRunId && approvedRunId === executionRunId) { try { await commitLive(); } catch (error) { setError(errorMessage(error)); } return; }
    if (executionRunId) { try { await prepareLiveReview(); window.dispatchEvent(new Event('nova-review-approval')); } catch (error) { setError(errorMessage(error)); } return; }
    if (saving) return;
    setSaving(true); setError('');
    try {
      const prompt = await (editingId ? prompts.update(editingId, draft) : prompts.create(draft));
      let contextWarning = '';
      try { await syncEditorContext(prompt); }
      catch (error) { contextWarning = ` El contexto de NOVA no se pudo actualizar: ${errorMessage(error)}`; }
      editorDrafts.forget(editingId);
      if (!editingId) setValuesByPrompt(previous => ({ ...previous, [prompt.id]: newDraftValues }));
      setSelected(prompt); setDraft({ ...blank }); setEditingId(null); setMode('focus'); setVersion(v => v + 1); setToast('Prompt guardado en tu biblioteca local.' + contextWarning);
    } catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  async function action(action: PromptAction) {
    if (!selected || saving) return;
    setSaving(true); setError('');
    try {
      const prompt = await prompts.act(selected, action);
      setSelected(prompt); setVersion(v => v + 1); setToast(action === 'duplicate' ? 'Copia creada.' : 'Biblioteca actualizada.');
    } catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  async function libraryAction(prompt: Prompt, verb: 'favorite' | 'duplicate') {
    if (saving) return; setSaving(true); setError('');
    try {
      const result = await prompts.act(prompt, verb);
      if (selected?.id === prompt.id && verb === 'favorite') setSelected(result);
      setVersion(value => value + 1); setToast(verb === 'duplicate' ? 'Copia creada.' : result.isFavorite ? 'Añadido a favoritos.' : 'Quitado de favoritos.');
    } catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  async function editFromLibrary(prompt: Prompt) {
    try {
      await ensureQuickActionAvailable();
      const fresh = await prompts.get(prompt.id);
      const cached = editorDrafts.read(fresh);
      setSelected(fresh); setEditingId(fresh.id); setDraft(cached ?? { title: fresh.title, templateText: fresh.templateText, description: fresh.description, categoryId: fresh.categoryId, revision: fresh.revision }); setMode('create'); setError('');
    } catch (error) { setError(errorMessage(error)); }
  }
  function aiDraft(text: string, source?: Prompt) {
    if (!source) setNewDraftValues({});
    const nextDraft = { title: source?.title ?? '', templateText: text, description: source?.description ?? '', categoryId: source?.categoryId ?? 0, revision: source?.revision };
    setEditingId(source?.id ?? null);
    editorDrafts.remember(source?.id ?? null, nextDraft); setDraft(nextDraft);
    setMode('create'); setError('');
  }
  async function ensureQuickActionAvailable() {
    if (!liveRunId) return;
    const current = await live.get(liveRunId);
    if (!['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(current.run.status)) throw new Error('Termina o detén la tarea actual antes de elegir otra acción.');
  }
  async function quickRequest(message: string, source?: Prompt, values?: Record<string, string>, command?: LiveCommand) {
    if (quickLaunching.current) throw new Error('Espera a que termine de iniciarse la acción anterior.');
    quickLaunching.current = true;
    try {
      await ensureQuickActionAvailable();
      await syncEditorContext();
      const fresh = source ? await prompts.get(source.id) : undefined;
      if (fresh?.revision !== source?.revision) throw new Error('El prompt cambió. Elígelo de nuevo antes de continuar.');
      if (fresh?.isArchived) throw new Error('Restaura este prompt desde el archivo antes de utilizarlo con NOVA.');
      const supplied = fresh ? Object.fromEntries(Object.entries(values ?? {}).filter(([name]) => fresh.variableNames.includes(name))) : undefined;
      const run = await startVisible(message, sessionId.current, fresh, supplied, { bindSource: true, command });
      setInputRequest(command && (command.operation === 'CREATE_DRAFT' || command.operation === 'PROPOSE_ADAPTATION') ? { runId: run.runId, message, source: fresh, command } : null);
      setLiveRunId(run.runId);
    } finally { quickLaunching.current = false; }
  }
  async function continueAuthoring(answer: string, sequence: number, brief?: AuthoringBriefReply) {
    const original = inputRequest;
    if (!original || original.runId !== liveRunId || !visibleSession.clarifyVisible || quickLaunching.current)
      throw new Error('Esta pestaña ya no conserva el encargo de esa pregunta.');
    quickLaunching.current = true;
    try {
      const run = await visibleSession.clarifyVisible(original.runId, sequence, answer, brief);
      // The workspace owns both the activity and SameTab driver. A transport
      // acknowledgement alone must not leave the new run without its UI worker.
      setInputRequest({ ...original, runId: run.runId });
      setLiveRunId(run.runId);
    } finally { quickLaunching.current = false; }
  }
  async function useWorkingDraft() {
    const source = mode === 'create' ? editingId === selected?.id ? selected : null : selected;
    if (!source && mode !== 'create') return;
    const text = mode === 'create' ? draft.templateText : source!.templateText;
    try {
      const result = await editorContext.prepare({ source, title: mode === 'create' ? draft.title : source!.title, templateText: text,
        defaults: mode === 'create' ? draft.defaults ?? source?.defaults : source?.defaults,
        values: (source ? valuesByPrompt[source.id] : newDraftValues) ?? noValues, sessionId: sessionId.current }); setEditorUse(result);
    }
    catch (error) { setError(errorMessage(error)); }
  }
  async function syncEditorContext(savedSource?: Prompt) {
    if (mode !== 'create' || !draft.templateText.trim()) return;
    const source = savedSource ?? (editingId === selected?.id ? selected : null);
    const templateText = savedSource?.templateText ?? draft.templateText;
    await editorContext.prepare({ source, title: savedSource?.title ?? (draft.title.trim() || 'Borrador del editor'), templateText,
      defaults: savedSource ? savedSource.defaults : draft.defaults ?? source?.defaults,
      values: (editingId && source ? valuesByPrompt[source.id] : newDraftValues) ?? noValues, sessionId: sessionId.current });
  }
  async function leaveEditor() {
    try { await syncEditorContext(); }
    catch (error) { setToast(`Tu borrador sigue en este editor. NOVA no pudo actualizar su contexto: ${errorMessage(error)}`); return; }
    setDiscardOpen(false); setMode(selected && editingId ? 'focus' : 'discover');
  }
  async function commitLive() {
    if (!executionRunId || mode !== 'create') throw new Error('El editor aprobado ya no está abierto.');
    const current = await live.get(executionRunId);
    if (current.run.approval !== 'APPROVED' || !current.proposal) throw new Error('Esta propuesta ya no tiene una aprobación vigente.');
    // A copy has a new revision even while the editor still displays its source.
    // Keep the actual form content; the server compares it to the approved hash.
    const prompt = await live.commit(executionRunId, { ...draft, revision: current.proposal.revision ?? 0 });
    editorDrafts.forget(editingId); if (!editingId) setValuesByPrompt(previous => ({ ...previous, [prompt.id]: newDraftValues })); setSelected(prompt); setEditingId(null); setDraft({ ...blank }); setMode('focus'); setVersion(v => v + 1);
  }
  async function prepareLiveReview() {
    if (!executionRunId || mode !== 'create') throw new Error('Abre el borrador actual antes de revisarlo.');
    await live.propose(executionRunId, draft);
  }
  async function chooseSearchSource(source: { id: string; revision: number; title: string }) {
    const fresh = await prompts.get(source.id);
    if (fresh.revision !== source.revision || fresh.isArchived) throw new Error('El candidato cambió. Repite la búsqueda antes de abrirlo.');
    await quickRequest(`Abre el prompt elegido: ${fresh.title}.`, fresh, {}, { operation: 'OPEN_PROMPT', content: '' });
  }
  async function reviewLive(view: LiveView) {
    const observed = view.observation; if (!observed?.promptId) return;
    try { const source = await prompts.get(observed.promptId); setSelected(source); setValuesByPrompt(previous => ({ ...previous, [source.id]: observed.values }));
      if (observed.draftChanged && observed.templateText && !view.run.effectCommitted && source.revision === observed.revision) aiDraft(observed.templateText, source);
      else setMode('focus');
    } catch (error) { setError(errorMessage(error)); }
  }
  const activePrompt = mode === 'focus' || mode === 'create' && editingId ? selected : null;
  return <MotionConfig reducedMotion={reducedMotion ? 'always' : 'never'}><LazyMotion features={domAnimation}><div className="app-shell">
    <a className="skip-link" href="#main">Ir al contenido</a>
    <WorkspaceSidebar mode={mode} theme={theme} busy={!!executionRunId} onHome={() => mode === 'create' ? setDiscardOpen(true) : discover()} onCreate={create} onHistory={() => setHistoryOpen(true)} onTheme={() => setTheme(theme === 'dark' ? 'light' : 'dark')} onSettings={() => setDataOpen(true)} onHelp={() => setHelpOpen(true)} />
    <div className={`toast-region${mode === 'focus' || providerOpen || dataOpen || groupsOpen ? ' sr-only' : ''}`} role="status" aria-live="polite" aria-atomic="true">{toast && <div className="toast">{toast}</div>}</div>
    <main id="main" tabIndex={-1}>
      <SameTabAgent runId={workerRunId ? null : liveRunId} onActive={setSameTabActive} speed={agentSpeed} onNotice={setAgentNotice} />
      <NovaLive runId={liveRunId} worker={!!executionRunId} notice={agentNotice} centralControl={controlling} onRuntimeChange={setRuntimeSnapshot} onApprovalChange={setApprovedRunId} onPrepareReview={prepareLiveReview} onChooseSource={chooseSearchSource} onReview={view => { void reviewLive(view); }}
        contentClarification={inputRequest?.runId === liveRunId ? { identity: visibleSession, confirm: continueAuthoring } : undefined}
        inputDefinition={inputRequest?.runId === liveRunId ? { updating: inputRequest.command.operation === 'PROPOSE_ADAPTATION', confirm: names => quickRequest(inputRequest.message, inputRequest.source, undefined, { ...inputRequest.command, inputNames: names }) } : undefined} />
      <AgentControl runId={controlling ? liveRunId : null} runtime={runtime} />
      {mode === 'discover' && <NovaWelcome runtime={runtime} home controlling={controlling} onOpen={() => window.dispatchEvent(new Event('nova-open-assistant'))} onHelp={() => setHelpOpen(true)} />}
      {!workerRunId && <AgentDock suspended={controlling} hasActivity={!!liveRunId} context={mode === 'create' ? draft.title || 'Borrador actual' : activePrompt?.title ?? 'Tu biblioteca'} actions={<QuickActions compact source={activePrompt ?? undefined} sourceValues={activePrompt ? valuesByPrompt[activePrompt.id] : undefined} disabled={!!executionRunId} onRequest={quickRequest} onRemove={quickRemove} />}>
        <p className="search-hint" data-testid="nova-action-workbench">Elige la acción; NOVA te ayuda con el contenido, los datos y la búsqueda.</p>
      </AgentDock>}
      <AnimatePresence mode="wait" initial={false}><m.div key={mode} variants={reducedMotion ? stillCanvasMotion : canvasMotion} initial="hidden" animate="visible" exit="exit">
        {mode === 'discover' && <>
          <section data-nova-surface="library" className="library-section" aria-labelledby="library-heading" aria-busy={library.loading}>
            <LibraryToolbar total={library.total} search={search} filter={filter} groupId={groupId} groups={groups} view={preferences.view} onSearch={value => { setSearch(value); setPage(0); }} onFilter={value => { setFilter(value); setPage(0); }} onGroup={value => { setGroupId(value); setPage(0); }} onView={view => setPreferences(value => ({ ...value, view }))} onCreate={create} onGroups={() => setGroupsOpen(true)} />
            {!library.loading && <span data-testid="library-ready" className="sr-only">Biblioteca actualizada</span>}
            {library.error ? <ErrorState title="No pudimos abrir la biblioteca." message={library.error}><button className="secondary" onClick={() => setVersion(v => v + 1)}>Reintentar</button></ErrorState> : library.loading ? <div className="loading-state"><p role="status">Buscando tus prompts…</p>{showSkeleton && <PromptSkeleton />}</div> : library.items.length ? <PromptStream items={library.items} open={open} view={preferences.view} busy={saving || !!executionRunId} onFavorite={prompt => { void libraryAction(prompt, 'favorite'); }} onEdit={prompt => { void editFromLibrary(prompt); }} onDuplicate={prompt => { void libraryAction(prompt, 'duplicate'); }} onDelete={prompt => { void quickRemove(prompt).catch(error => setError(errorMessage(error))); }} /> : <EmptyState title={search ? 'Aquí empieza una nueva idea.' : filter === 'all' && !groupId ? 'Tu primera instrucción te espera.' : 'Todavía no hay prompts aquí.'}><p className="muted">{search ? 'No hay coincidencias en tu biblioteca. Prueba otras palabras o crea un prompt.' : 'Guarda una instrucción y reutilízala cuando la necesites.'}</p><button className="secondary" onClick={create}><AppIcon glyph={Plus} size={20} aria-hidden="true"  />Crear prompt</button></EmptyState>}
            {library.total > 12 && <nav className="pagination" aria-label="Páginas"><button className="icon-button" disabled={page === 0 || library.loading} onClick={() => setPage(p => p - 1)} aria-label="Página anterior"><AppIcon glyph={ChevronLeft} size={20} aria-hidden="true"  /></button><span>Página {page + 1} de {Math.ceil(library.total / 12)}</span><button className="icon-button" disabled={(page + 1) * 12 >= library.total || library.loading} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente"><AppIcon glyph={ChevronRight} size={20} aria-hidden="true"  /></button></nav>}
          </section>
        </>}
        {mode === 'focus' && selected && <PromptFocus key={selected.id} prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onValues={values => setValuesByPrompt(previous => ({ ...previous, [selected.id]: values }))} onBack={discover} onEdit={edit} onAction={action} onDelete={() => { void quickRemove(selected).catch(error => setError(errorMessage(error))); }} onGroups={() => setGroupsOpen(true)} onVersions={() => setVersionsOpen(true)} onShare={() => setShareOpen(true)} onUsed={() => setVersion(v => v + 1)} notify={setToast} feedback={toast} onUse={() => executionRunId ? void useWorkingDraft() : setProviderOpen(true)} />}
        {mode === 'create' && <InlinePromptEditor approvedRunId={executionRunId === approvedRunId ? approvedRunId : null} sourceDefaults={editingId === selected?.id ? selected.defaults : undefined} metadataLocked={!!executionRunId} promptId={editingId} preparation={<EditorPreparation onObserve={async (message, source, signal) => { if (executionRunId) return executionRunId; const observed = await observeOperation(message, sessionId.current, source, signal); setLiveRunId(observed.runId); return observed.runId; }} source={editingId ? selected : null} draft={draft} onChange={changeDraft} values={editingId && selected ? valuesByPrompt[selected.id] ?? noValues : newDraftValues} onValues={values => { if (editingId && selected) setValuesByPrompt(previous => ({ ...previous, [selected.id]: values })); else setNewDraftValues(values); }} onUse={() => void useWorkingDraft()} />} draft={draft} onChange={changeDraft} onSave={save} onBack={back} saving={saving} error={error} editing={!!editingId} />}
      </m.div></AnimatePresence>
      {error && mode !== 'create' && <p className="error global-error" role="alert">{error}</p>}
      {editorUse && <Suspense fallback={<p role="status">Preparando destinos…</p>}><PreparedUse draft={editorUse} onClose={() => setEditorUse(null)} /></Suspense>}
    </main>
    <WorkspaceFooter onHelp={() => setHelpOpen(true)} />
    {providerOpen && selected && <Suspense fallback={<p className="loading-state" role="status">Preparando destinos…</p>}><ProviderPicker prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onClose={() => setProviderOpen(false)} notify={setToast} onUsed={() => setVersion(v => v + 1)} /></Suspense>}
    {groupsOpen && <Suspense fallback={null}><GroupSelector prompt={mode === 'focus' ? selected ?? undefined : undefined} onClose={() => setGroupsOpen(false)} onUpdated={() => { setVersion(v => v + 1); setGroupId(''); }} /></Suspense>}
    {versionsOpen && mode === 'focus' && selected && <Suspense fallback={<p role="status">Abriendo versiones…</p>}><PromptVersions key={selected.id} prompt={selected} onClose={() => setVersionsOpen(false)} onCurrentChanged={prompt => { setSelected(current => current?.id === prompt.id ? prompt : current); setVersion(value => value + 1); }} onRestored={prompt => { setSelected(current => current?.id === prompt.id ? prompt : current); setVersion(value => value + 1); setVersionsOpen(false); setToast('Versión restaurada. El historial anterior se conserva.'); }} /></Suspense>}
    {shareOpen && mode === 'focus' && selected && <Suspense fallback={<p role="status">Abriendo plantilla para compartir…</p>}><SharePrompt key={selected.id} prompt={selected} onClose={() => setShareOpen(false)} onReload={() => { setShareOpen(false); void open(selected); }} /></Suspense>}
    {historyOpen && <Suspense fallback={null}><CompilationHistory onClose={() => setHistoryOpen(false)} /></Suspense>}
    {dataOpen && <Suspense fallback={null}><LibraryTools onClose={() => setDataOpen(false)} onUpdated={() => { setVersion(v => v + 1); setSelected(null); setMode('discover'); setValuesByPrompt({}); }} notify={setToast}><InterfaceSettings preferences={preferences} onChange={patch => setPreferences(value => ({ ...value, ...patch }))} theme={theme} onTheme={setTheme} /></LibraryTools></Suspense>}
    <Dialog open={!!deleteTarget} onOpenChange={open => { if (!open) void cancelDeletion(); }} title="¿Eliminar este prompt?" description={`Se eliminará «${deleteTarget?.title ?? ''}» y sus versiones. El historial de textos utilizados se conserva. Puedes archivarlo si quieres recuperarlo después.`}><div data-testid="prompt-delete-review" data-delete-prompt-id={deleteTarget?.id} data-delete-revision={deleteTarget?.revision} data-delete-run-id={deletionReview?.runId} data-delete-ready={!deletionReview || deletionReady}><div className="dialog-actions"><button className="secondary" disabled={saving} onClick={() => void cancelDeletion()}>Cancelar</button><button className="destructive" disabled={saving || !!deletionReview && !deletionReady} onClick={remove}>{saving ? 'Eliminando…' : 'Eliminar definitivamente'}</button></div></div>{error && <p className="error" role="alert">{error}</p>}</Dialog>
    <Dialog open={discardOpen} onOpenChange={setDiscardOpen} title="¿Salir del editor?" description="Tu borrador se conservará durante esta sesión. Guarda una versión para conservarlo en SQLite."><div className="dialog-actions"><button className="secondary" onClick={() => setDiscardOpen(false)}>Seguir editando</button><button className="primary" onClick={() => { void leaveEditor(); }}>Salir del editor</button></div></Dialog>
    {helpOpen && <Suspense fallback={<p role="status">Abriendo la guía…</p>}><LearningCenter onClose={() => setHelpOpen(false)} onImported={() => setVersion(value => value + 1)} /></Suspense>}
  </div></LazyMotion></MotionConfig>;
}
