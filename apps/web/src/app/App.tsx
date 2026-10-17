import { useEffect, useRef, useState } from 'react';
import { lazy, Suspense } from 'react';
import { AnimatePresence, domAnimation, LazyMotion, m, MotionConfig } from 'motion/react';
import { ChevronLeft, ChevronRight, CircleHelp, Database, FolderPlus, Library, Moon, Plus, Sun } from 'lucide-react';
import { Dialog } from '../components/ui/Dialog';
import { NovaCore, type NovaState } from '../features/nova/NovaCore';
import { InlinePromptEditor } from '../features/prompts/InlinePromptEditor';
import { PromptFocus } from '../features/prompts/PromptFocus';
import { PromptStream } from '../features/prompts/PromptStream';
import { useLibrary, type Filter } from '../hooks/useLibrary';
import { PreparationCanvas } from '../features/nova/PreparationCanvas';
import { SameTabAgent } from '../features/nova/SameTabAgent';
import { NovaLive } from '../features/nova/NovaLive';
import { QuickActions } from '../features/nova/QuickActions';
import { EditorPreparation } from '../features/prompts/EditorPreparation';
import { semanticSchema } from '../services/semantic';
import { preparedSchema, type PreparedDraft } from '../services/preparation';
import { liveRecordSchema, liveViewSchema, observeOperation, startVisible, type LiveView } from '../services/live';

import { emptySchema, errorMessage, groupsSchema, inspectSchema, post, promptSchema, request, type Group, type Prompt, type PromptDraft } from '../services/api';

const NovaAssist = lazy(() => import('../features/nova/NovaAssist').then(module => ({ default: module.NovaAssist })));
const ProviderPicker = lazy(() => import('../features/providers/ProviderPicker').then(module => ({ default: module.ProviderPicker })));
const GroupSelector = lazy(() => import('../features/groups/GroupSelector').then(module => ({ default: module.GroupSelector })));
const LibraryTools = lazy(() => import('../features/settings/LibraryTools').then(module => ({ default: module.LibraryTools })));
const LearningCenter = lazy(() => import('../features/help/LearningCenter').then(module => ({ default: module.LearningCenter })));
const PreparedUse = lazy(() => import('../features/nova/PreparedUse').then(module => ({ default: module.PreparedUse })));

const blank: PromptDraft = { title: '', templateText: '', description: '', categoryId: 0 };
const noValues: Record<string, string> = {};
function initialTheme() { try { return localStorage.getItem('nova-theme') === 'light' ? 'light' : 'dark'; } catch { return 'dark'; } }
export default function App() {
  const [workerRunId] = useState(() => { const id = new URLSearchParams(location.search).get('novaRun'); return id && /^[0-9a-f-]{36}$/i.test(id) ? id : null; });
  const [liveRunId, setLiveRunId] = useState<string | null>(() => { if (workerRunId) return workerRunId; try { const id = sessionStorage.getItem('nova-live-run'); return id && /^[0-9a-f-]{36}$/i.test(id) ? id : null; } catch { return null; } });
  useEffect(() => { if (liveRunId) try { sessionStorage.setItem('nova-live-run', liveRunId); } catch { /* Only a run ID is retained; no request or values. */ } }, [liveRunId]);
  useEffect(() => { const uncertain = (event: Event) => { const id = (event as CustomEvent<string>).detail; if (/^[0-9a-f-]{36}$/i.test(id)) setLiveRunId(id); }; window.addEventListener('nova-uncertain-start', uncertain); return () => window.removeEventListener('nova-uncertain-start', uncertain); }, []);
  const [sameTabActive, setSameTabActive] = useState(false);
  const [agentSpeed, setAgentSpeed] = useState<'normal' | 'fast'>('normal');
  const [agentNotice, setAgentNotice] = useState('');
  const executionRunId = workerRunId ?? (sameTabActive ? liveRunId : null);
  const [editorUse, setEditorUse] = useState<PreparedDraft | null>(null);
  const [liveInstruction, setLiveInstruction] = useState('');
  const [theme, setTheme] = useState(initialTheme);
  const [novaState, setNovaState] = useState<NovaState>('idle');
  const [search, setSearch] = useState('');
  const [intention, setIntention] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [groupId, setGroupId] = useState('');
  const [groups, setGroups] = useState<Group[]>([]);
  const [page, setPage] = useState(0);
  const [version, setVersion] = useState(0);
  const [selected, setSelected] = useState<Prompt | null>(null);
  const [mode, setMode] = useState<'discover' | 'focus' | 'create'>('discover');
  const [draft, setDraft] = useState<PromptDraft>(blank);
  const [presentedPreparation, setPresentedPreparation] = useState<PreparedDraft | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [newDraftValues, setNewDraftValues] = useState<Record<string, string>>({});
  const [valuesByPrompt, setValuesByPrompt] = useState<Record<string, Record<string, string>>>({});
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [toast, setToastText] = useState('');
  const [toastSequence, setToastSequence] = useState(0);
  function setToast(text: string) { setToastText(text); setToastSequence(value => value + 1); }
  const [providerOpen, setProviderOpen] = useState(false);
  const [groupsOpen, setGroupsOpen] = useState(false);
  const [dataOpen, setDataOpen] = useState(false);
  const searchRef = useRef<HTMLTextAreaElement>(null);
  const sessionId = useRef(crypto.randomUUID());
  const focusSearchAfterTransition = useRef(false);
  const opening = useRef<AbortController | null>(null);
  const quickLaunching = useRef(false);
  const editorDrafts = useRef(new Map<string, PromptDraft>());
  const library = useLibrary(search, filter, groupId, page, version);
  useEffect(() => {
    const prepared = presentedPreparation;
    if (!prepared || prepared.sourceId !== (mode === 'create' ? editingId : selected?.id ?? null) || mode !== 'create' && mode !== 'focus') return;
    const root = document.querySelector(mode === 'create' ? '[data-testid="prompt-editor"]' : '[data-testid="prompt-focus"]');
    const rendered = mode === 'create' ? root?.querySelector<HTMLTextAreaElement>('#prompt-content')?.value : root?.querySelector('[data-testid="compiled-preview"]')?.textContent;
    if (rendered !== prepared.templateText && rendered !== prepared.compiledText) return;
    const current = new AbortController();
    void request(`/nova/live/${prepared.id}/ui-observed`, emptySchema, post({ preparedId: prepared.id, sourceId: prepared.sourceId, sourceRevision: prepared.sourceRevision, renderedText: rendered }, current.signal))
      .then(() => setPresentedPreparation(value => value?.id === prepared.id ? null : value)).catch(() => {});
    return () => current.abort();
  }, [presentedPreparation, selected?.id, editingId, mode, draft.templateText]);
  useEffect(() => { document.documentElement.dataset.theme = theme; try { localStorage.setItem('nova-theme', theme); } catch { /* Theme remains usable without storage. */ } }, [theme]);
  useEffect(() => { request('/groups', groupsSchema).then(setGroups).catch(() => {}); }, [version]);
  useEffect(() => {
    const listener = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key === 'k') { event.preventDefault(); if (mode === 'discover') searchRef.current?.focus(); else if (mode !== 'create') { focusSearchAfterTransition.current = true; setMode('discover'); } }
    };
    document.addEventListener('keydown', listener); return () => document.removeEventListener('keydown', listener);
  }, [mode]);
  useEffect(() => { if (toast) { const timer = setTimeout(() => setToastText(''), 5000); return () => clearTimeout(timer); } }, [toast, toastSequence]);
  useEffect(() => () => opening.current?.abort(), []);
  function discover() { opening.current?.abort(); setMode('discover'); setError(''); }
  function back() { if (mode === 'create') setDiscardOpen(true); else discover(); }
  async function open(prompt: Prompt) {
    opening.current?.abort(); const current = new AbortController(); opening.current = current;
    setError('');
    try { const fresh = await request(`/prompts/${prompt.id}`, promptSchema, { signal: current.signal }); if (!current.signal.aborted) { setSelected(fresh); setMode('focus'); } }
    catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
  }
  function create() { opening.current?.abort(); const cached = editorDrafts.current.get('new'); if (!cached) setNewDraftValues({}); setEditingId(null); setDraft(cached ?? { ...blank }); setMode('create'); setError(''); }
  function changeDraft(next: PromptDraft) { editorDrafts.current.set(editingId ?? 'new', next); setDraft(next); }
  function edit() { if (!selected) return; setEditingId(selected.id); setDraft(editorDrafts.current.get(selected.id) ?? { title: selected.title, templateText: selected.templateText, description: selected.description, categoryId: selected.categoryId, revision: selected.revision }); setMode('create'); setError(''); }
  async function save() {
    if (executionRunId) { try { await request(`/nova/live/${executionRunId}/proposal`, liveViewSchema, post(draft)); window.dispatchEvent(new Event('nova-review-approval')); } catch (error) { setError(errorMessage(error)); } return; }
    if (saving) return;
    setSaving(true); setError('');
    try {
      const prompt = await request(editingId ? `/prompts/${editingId}` : '/prompts', promptSchema, { method: editingId ? 'PUT' : 'POST', body: JSON.stringify(draft) });
      let contextWarning = '';
      try { await syncEditorContext(prompt); }
      catch (error) { contextWarning = ` El contexto de NOVA no se pudo actualizar: ${errorMessage(error)}`; }
      editorDrafts.current.delete(editingId ?? 'new');
      if (!editingId) setValuesByPrompt(previous => ({ ...previous, [prompt.id]: newDraftValues }));
      setSelected(prompt); setDraft({ ...blank }); setEditingId(null); setMode('focus'); setVersion(v => v + 1); setToast('Prompt guardado en tu biblioteca local.' + contextWarning);
    } catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  async function action(action: string) {
    if (!selected || saving) return;
    setSaving(true); setError('');
    try {
      const prompt = await request(`/prompts/${selected.id}/${action}`, promptSchema, post({ revision: selected.revision }));
      setSelected(prompt); setVersion(v => v + 1); setToast(action === 'duplicate' ? 'Copia creada.' : 'Biblioteca actualizada.');
    } catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  async function remove() {
    if (!selected || saving) return;
    setSaving(true); setError('');
    try { await request(`/prompts/${selected.id}`, emptySchema, { method: 'DELETE', body: JSON.stringify({ revision: selected.revision, confirmed: true }) }); setDeleteOpen(false); setSelected(null); discover(); setVersion(v => v + 1); setToast('Prompt eliminado.'); }
    catch (error) { setError(errorMessage(error)); } finally { setSaving(false); }
  }
  function aiDraft(text: string, source?: Prompt) {
    if (!source) setNewDraftValues({});
    const nextDraft = { title: source?.title ?? '', templateText: text, description: source?.description ?? '', categoryId: source?.categoryId ?? 0, revision: source?.revision };
    setEditingId(source?.id ?? null);
    editorDrafts.current.set(source?.id ?? 'new', nextDraft); setDraft(nextDraft);
    setMode('create'); setError('');
  }
  async function liveCommand(text: string) {
    if (!liveRunId) return false;
    const current = await request('/nova/live/' + liveRunId, liveViewSchema);
    if (['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(current.run.status)) return false;
    const interpretation = await request('/nova/semantic/interpret', semanticSchema, post({ message: text, sessionId: sessionId.current }));
    const action = interpretation.intentions.includes('CANCEL') ? 'cancel' : interpretation.intentions.includes('PAUSE') ? 'pause' : interpretation.intentions.includes('RESUME') ? 'resume' : interpretation.intentions.includes('REQUEST_SAVE_VERSION') ? 'review' : null;
    if (!action) throw new Error('Termina o detén la ejecución activa antes de iniciar otra petición.');
    if (action === 'review') { if (!current.proposal) throw new Error('Espera a que NOVA prepare la propuesta antes de guardar.'); window.dispatchEvent(new Event('nova-review-approval')); }
    else { if (action !== 'resume') window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: action })); const controlled = await request('/nova/live/' + liveRunId + '/' + action, liveRecordSchema, post({})); if (action === 'resume' && !controlled.pauseRequested && !['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(controlled.status)) window.dispatchEvent(new CustomEvent('nova-ui-control', { detail: 'resume' })); }
    return true;
  }
  async function ensureQuickActionAvailable() {
    if (!liveRunId) return;
    const current = await request('/nova/live/' + liveRunId, liveViewSchema);
    if (!['COMPLETED', 'FAILED', 'CANCELLED', 'BLOCKED'].includes(current.run.status)) throw new Error('Termina o detén la tarea actual antes de elegir otra acción.');
  }
  async function quickRequest(message: string, source?: Prompt, values?: Record<string, string>) {
    if (quickLaunching.current) throw new Error('Espera a que termine de iniciarse la acción anterior.');
    quickLaunching.current = true;
    try {
      await ensureQuickActionAvailable();
      const fresh = source ? await request(`/prompts/${source.id}`, promptSchema) : undefined;
      if (fresh?.revision !== source?.revision) throw new Error('El prompt cambió. Elígelo de nuevo antes de continuar.');
      if (fresh?.isArchived) throw new Error('Restaura este prompt desde el archivo antes de utilizarlo con NOVA.');
      const run = await startVisible(message, sessionId.current, fresh, fresh ? { ...valuesByPrompt[fresh.id], ...values } : undefined, { bindSource: true });
      setLiveRunId(run.runId);
    } finally { quickLaunching.current = false; }
  }
  async function quickRemove(source: Prompt) {
    await ensureQuickActionAvailable();
    const fresh = await request(`/prompts/${source.id}`, promptSchema);
    setSelected(fresh); setMode('focus'); setError(''); setDeleteOpen(true);
  }
  async function useWorkingDraft() {
    const source = mode === 'create' ? editingId === selected?.id ? selected : null : selected;
    if (!source && mode !== 'create') return;
    const text = mode === 'create' ? draft.templateText : source!.templateText;
    try {
      const inspected = await request('/templates/inspect', inspectSchema, post({ templateText: text }));
      const available = { ...source?.defaults, ...(source ? valuesByPrompt[source.id] : newDraftValues) };
      const values = Object.fromEntries(inspected.variableNames.filter(name => available[name]?.trim()).map(name => [name, available[name]]));
      const result = await request('/nova/editor-drafts', preparedSchema, post({ promptId: source?.id ?? null, revision: source?.revision ?? null, title: mode === 'create' ? draft.title : source!.title, templateText: text, values, sessionId: sessionId.current })); setEditorUse(result);
    }
    catch (error) { setError(errorMessage(error)); }
  }
  async function syncEditorContext(savedSource?: Prompt) {
    if (mode !== 'create' || !draft.templateText.trim()) return;
    const source = savedSource ?? (editingId === selected?.id ? selected : null);
    const templateText = savedSource?.templateText ?? draft.templateText;
    const inspected = await request('/templates/inspect', inspectSchema, post({ templateText }));
    const available = { ...source?.defaults, ...(editingId && source ? valuesByPrompt[source.id] : newDraftValues) };
    const values = Object.fromEntries(inspected.variableNames.filter(name => available[name]?.trim()).map(name => [name, available[name]]));
    await request('/nova/editor-drafts', preparedSchema, post({ promptId: source?.id ?? null, revision: source?.revision ?? null,
      title: savedSource?.title ?? (draft.title.trim() || 'Borrador del editor'), templateText, values, sessionId: sessionId.current }));
  }
  async function leaveEditor() {
    try { await syncEditorContext(); }
    catch (error) { setToast(`Tu borrador sigue en este editor. NOVA no pudo actualizar su contexto: ${errorMessage(error)}`); return; }
    setDiscardOpen(false); setMode(selected && editingId ? 'focus' : 'discover');
  }
  async function commitLive() {
    if (!executionRunId || mode !== 'create') throw new Error('El editor aprobado ya no está abierto.');
    const prompt = await request(`/nova/live/${executionRunId}/commit`, promptSchema, post(draft));
    editorDrafts.current.delete(editingId ?? 'new'); if (!editingId) setValuesByPrompt(previous => ({ ...previous, [prompt.id]: newDraftValues })); setSelected(prompt); setEditingId(null); setDraft({ ...blank }); setMode('focus'); setVersion(v => v + 1);
  }
  async function reviewLive(view: LiveView) {
    const observed = view.observation; if (!observed?.promptId) return;
    try { const source = await request(`/prompts/${observed.promptId}`, promptSchema); setSelected(source); setValuesByPrompt(previous => ({ ...previous, [source.id]: observed.values }));
      if (observed.draftChanged && observed.templateText && !view.run.effectCommitted && source.revision === observed.revision) aiDraft(observed.templateText, source);
      else setMode('focus');
    } catch (error) { setError(errorMessage(error)); }
  }
  return <MotionConfig reducedMotion="user"><LazyMotion features={domAnimation}><div className="app-shell">
    <a className="skip-link" href="#main">Ir al contenido</a>
    <header className="app-header"><button className="brand" onClick={() => mode === 'create' ? setDiscardOpen(true) : discover()} aria-label="QuickPrompt NOVA, biblioteca"><span className="brand-mark" aria-hidden="true">q<span>p</span></span><span>QuickPrompt <span className="brand-nova">NOVA</span></span></button>
      <div className="header-actions"><span className="local-indicator"><i />Local y privado</span><button className="icon-button" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')} aria-label={theme === 'dark' ? 'Activar tema claro' : 'Activar tema oscuro'}>{theme === 'dark' ? <Sun size={20} aria-hidden="true" /> : <Moon size={20} aria-hidden="true" />}</button><button className="icon-button" onClick={() => setDataOpen(true)} aria-label="Datos y respaldos"><Database size={19} aria-hidden="true" /></button><button className="icon-button" onClick={() => setHelpOpen(true)} aria-label="Ayuda"><CircleHelp size={20} aria-hidden="true" /></button></div>
    </header>
    <div className={`toast-region${mode === 'focus' || providerOpen || dataOpen || groupsOpen ? ' sr-only' : ''}`} role="status" aria-live="polite" aria-atomic="true">{toast && <div className="toast">{toast}</div>}</div>
    <main id="main" tabIndex={-1}>
      <SameTabAgent runId={workerRunId ? null : liveRunId} onActive={setSameTabActive} speed={agentSpeed} onNotice={setAgentNotice} />
      <NovaLive runId={liveRunId} worker={!!executionRunId} speed={agentSpeed} onSpeedChange={setAgentSpeed} notice={agentNotice} onCommit={commitLive} onReview={view => { void reviewLive(view); }} />
      {!workerRunId && mode === 'focus' && <form className="live-followup" data-nova-command="true" onSubmit={event => { event.preventDefault(); void (async () => { if (await liveCommand(liveInstruction)) return; const run = await startVisible(liveInstruction, sessionId.current, selected ?? undefined, selected ? valuesByPrompt[selected.id] : undefined); setLiveRunId(run.runId); })().catch(error => setError(errorMessage(error))); }}><label htmlFor="live-followup">Instrucción para NOVA</label><textarea id="live-followup" rows={2} maxLength={16000} value={liveInstruction} onChange={event => setLiveInstruction(event.target.value)} placeholder="Actualiza este prompt para que sea más corto…" /><button className="secondary" disabled={!liveInstruction.trim()}>Ejecutar en esta pestaña</button></form>}
      <AnimatePresence mode="wait" initial={false}><m.div key={mode} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -4 }} transition={{ duration: 0.2 }} onAnimationComplete={() => { if (mode === 'discover' && focusSearchAfterTransition.current) { searchRef.current?.focus(); focusSearchAfterTransition.current = false; } }}>
      {!workerRunId && mode === 'focus' && <QuickActions source={selected ?? undefined} disabled={!!executionRunId || novaState === 'thinking'} onRequest={quickRequest} onRemove={quickRemove} />}
        {mode === 'discover' && <>
          <section className="discovery" aria-labelledby="discover-heading"><NovaCore state={novaState !== 'idle' ? novaState : library.loading ? 'searching' : search ? 'listening' : 'idle'} /><h1 id="discover-heading">Tus ideas.<br /><span>Listas para actuar.</span></h1><p className="hero-description">Elige un prompt, completa tus datos y úsalo con tu IA.<br />O cuéntale a NOVA qué instrucción necesitas.</p><button className="text-button learning-entry" onClick={() => setHelpOpen(true)}><CircleHelp size={18} aria-hidden="true" />Empieza aquí: guía y videos</button>
            <QuickActions disabled={!!executionRunId || novaState === 'thinking'} onRequest={quickRequest} onRemove={quickRemove} />
            <PreparationCanvas contextKey={draft.templateText ? JSON.stringify(draft) : undefined} onContext={syncEditorContext} onLiveCommand={liveCommand} onLiveRun={setLiveRunId} inputRef={searchRef} sessionId={sessionId.current} retrievalFilter={{ groupId: groupId || undefined, favorites: filter === 'favorites' }} contextPrompt={selected} contextValues={selected ? Object.fromEntries(Object.entries(valuesByPrompt[selected.id] ?? {}).filter(([name]) => selected.variableNames.includes(name))) : undefined} initialMessage={intention} onMessage={setIntention} onState={setNovaState} onSaved={prompt => { if (selected?.id === prompt.id) setSelected(prompt); setVersion(value => value + 1); }} onPrepared={async result => {
              const semantic = result.semantic;
              if (!semantic || result.decision === 'ASK_CLARIFICATION') return false;
              if (!result.sourceId && semantic.intentions.includes('PROPOSE_ADAPTATION') && !semantic.intentions.includes('EXECUTE_LOCAL')) {
                const next = { title: result.title, templateText: result.templateText, description: result.description, categoryId: result.categoryId };
                setEditingId(null); setNewDraftValues(Object.fromEntries(result.values.map(value => [value.name, value.value])));
                editorDrafts.current.set('new', next); setDraft(next); setPresentedPreparation(result); setMode('create'); setError(''); return true;
              }
              if (!result.sourceId) return false;
              const source = await request('/prompts/' + result.sourceId, promptSchema);
              if (source.revision !== result.sourceRevision) throw new Error('El origen cambió. Revisa el prompt antes de continuar.');
              setValuesByPrompt(previous => ({ ...previous, [source.id]: Object.fromEntries(result.values.map(value => [value.name, value.value])) }));
              setPresentedPreparation(result);
              if (semantic.desiredOutcome === 'READ' && semantic.intentions.includes('OPEN_PROMPT')) {
                if (result.templateText !== source.templateText) { setSelected(source); aiDraft(result.templateText, source); }
                else await open(source);
                return true;
              }
              if (semantic.intentions.includes('PROPOSE_ADAPTATION') && !semantic.intentions.includes('EXECUTE_LOCAL') && result.decision === 'ADAPT_EXISTING') { setSelected(source); aiDraft(result.templateText, source); return true; }
              return false;
            }} />
          </section>
          <section className="library-section" aria-labelledby="library-heading" aria-busy={library.loading}>
            <div className="section-heading"><h2 id="library-heading"><Library size={19} aria-hidden="true" />Tu biblioteca <span className="count">{library.total}</span></h2><button className="text-button accent" onClick={create}><Plus size={18} aria-hidden="true" />Nuevo prompt</button></div>
            <label className="library-search">Buscar en la biblioteca<input value={search} maxLength={200} onChange={event => { setSearch(event.target.value); setPage(0); }} /></label>{!library.loading && <span data-testid="library-ready" className="sr-only">Biblioteca actualizada</span>}<div className="library-filters"><div className="filter-tabs" role="group" aria-label="Filtrar biblioteca">{([['all', 'Todos'], ['favorites', 'Favoritos'], ['recent', 'Recientes'], ['archived', 'Archivo']] as const).map(([value, label]) => <button key={value} aria-pressed={filter === value} onClick={() => { setFilter(value); setPage(0); }}>{label}</button>)}</div><div className="collection-filter"><label className="group-filter"><span className="sr-only">Colección</span><select aria-label="Colección" value={groupId} onChange={e => { setGroupId(e.target.value); setPage(0); }}><option value="">Todas las colecciones</option>{groups.map(({ group }) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><button className="icon-button" aria-label="Gestionar colecciones" onClick={() => setGroupsOpen(true)}><FolderPlus size={18} aria-hidden="true" /></button></div></div>
            {library.error ? <div className="empty-state"><h3>No pudimos abrir la biblioteca.</h3><p role="alert" className="error">{library.error}</p><button className="secondary" onClick={() => setVersion(v => v + 1)}>Reintentar</button></div> : library.loading ? <p className="loading-state" role="status">Buscando tus prompts…</p> : library.items.length ? <PromptStream items={library.items} open={open} /> : <div className="empty-state"><h3>{search ? 'Aquí empieza una nueva idea.' : filter === 'all' && !groupId ? 'Tu primera instrucción te espera.' : 'Todavía no hay prompts aquí.'}</h3><p className="muted">{search ? 'No hay coincidencias en tu biblioteca. Prueba otras palabras o crea un prompt.' : 'Guarda una instrucción y reutilízala cuando la necesites.'}</p><button className="secondary" onClick={create}><Plus size={17} aria-hidden="true" />Crear prompt</button></div>}
            {library.total > 12 && <nav className="pagination" aria-label="Páginas"><button className="icon-button" disabled={page === 0 || library.loading} onClick={() => setPage(p => p - 1)} aria-label="Página anterior"><ChevronLeft size={20} aria-hidden="true" /></button><span>Página {page + 1} de {Math.ceil(library.total / 12)}</span><button className="icon-button" disabled={(page + 1) * 12 >= library.total || library.loading} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente"><ChevronRight size={20} aria-hidden="true" /></button></nav>}
          </section>
        </>}
        {mode === 'focus' && selected && <PromptFocus key={selected.id} prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onValues={values => setValuesByPrompt(previous => ({ ...previous, [selected.id]: values }))} onBack={discover} onEdit={edit} onAction={action} onDelete={() => setDeleteOpen(true)} onGroups={() => setGroupsOpen(true)} onUsed={() => setVersion(v => v + 1)} notify={setToast} feedback={toast} onUse={() => executionRunId ? void useWorkingDraft() : setProviderOpen(true)} assistance={<Suspense fallback={null}><NovaAssist onLiveRun={workerRunId ? undefined : setLiveRunId} sessionId={sessionId.current} message={selected.description} source={selected} onOpen={open} onDraft={aiDraft} onValues={values => setValuesByPrompt(previous => ({ ...previous, [selected.id]: { ...previous[selected.id], ...values } }))} /></Suspense>} />}
        {mode === 'create' && <InlinePromptEditor promptId={editingId} preparation={<EditorPreparation onObserve={async (message, source, signal) => { if (executionRunId) return executionRunId; const observed = await observeOperation(message, sessionId.current, source, signal); setLiveRunId(observed.runId); return observed.runId; }} source={editingId ? selected : null} draft={draft} onChange={changeDraft} values={editingId && selected ? valuesByPrompt[selected.id] ?? noValues : newDraftValues} onValues={values => { if (editingId && selected) setValuesByPrompt(previous => ({ ...previous, [selected.id]: values })); else setNewDraftValues(values); }} onUse={() => void useWorkingDraft()} />} draft={draft} onChange={changeDraft} onSave={save} onBack={back} saving={saving} error={error} editing={!!editingId} />}
      </m.div></AnimatePresence>
      {error && mode !== 'create' && <p className="error global-error" role="alert">{error}</p>}
      {editorUse && <Suspense fallback={<p role="status">Preparando destinos…</p>}><PreparedUse draft={editorUse} onClose={() => setEditorUse(null)} /></Suspense>}
    </main>
    <footer className="app-footer"><span>Un espacio para pensar mejor.</span><span>HECHO PARA REUTILIZAR</span></footer>
    {providerOpen && selected && <Suspense fallback={<p className="loading-state" role="status">Preparando destinos…</p>}><ProviderPicker prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onClose={() => setProviderOpen(false)} notify={setToast} onUsed={() => setVersion(v => v + 1)} /></Suspense>}
    {groupsOpen && <Suspense fallback={null}><GroupSelector prompt={mode === 'focus' ? selected ?? undefined : undefined} onClose={() => setGroupsOpen(false)} onUpdated={() => { setVersion(v => v + 1); setGroupId(''); }} /></Suspense>}
    {dataOpen && <Suspense fallback={null}><LibraryTools onClose={() => setDataOpen(false)} onUpdated={() => { setVersion(v => v + 1); setSelected(null); setMode('discover'); setValuesByPrompt({}); }} notify={setToast} /></Suspense>}
    <Dialog open={deleteOpen} onOpenChange={open => !saving && setDeleteOpen(open)} title="¿Eliminar este prompt?" description={`Se eliminará «${selected?.title ?? ''}» y sus versiones. El historial de textos utilizados se conserva. Puedes archivarlo si quieres recuperarlo después.`}><div className="dialog-actions"><button className="secondary" onClick={() => setDeleteOpen(false)}>Cancelar</button><button className="destructive" disabled={saving} onClick={remove}>{saving ? 'Eliminando…' : 'Eliminar definitivamente'}</button></div>{error && <p className="error" role="alert">{error}</p>}</Dialog>
    <Dialog open={discardOpen} onOpenChange={setDiscardOpen} title="¿Salir del editor?" description="Tu borrador se conservará durante esta sesión. Guarda una versión para conservarlo en SQLite."><div className="dialog-actions"><button className="secondary" onClick={() => setDiscardOpen(false)}>Seguir editando</button><button className="primary" onClick={() => { void leaveEditor(); }}>Salir del editor</button></div></Dialog>
    {helpOpen && <Suspense fallback={<p role="status">Abriendo la guía…</p>}><LearningCenter onClose={() => setHelpOpen(false)} onImported={() => setVersion(value => value + 1)} /></Suspense>}
  </div></LazyMotion></MotionConfig>;
}
