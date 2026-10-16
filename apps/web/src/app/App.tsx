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
import { CommandDialog } from '../features/nova/CommandDialog';
import { commandSchema, proposalSchema, type LocalCommand } from '../services/commands';
import { emptySchema, errorMessage, groupsSchema, post, promptSchema, request, type Group, type Prompt, type PromptDraft } from '../services/api';

const NovaAssist = lazy(() => import('../features/nova/NovaAssist').then(module => ({ default: module.NovaAssist })));
const ProviderPicker = lazy(() => import('../features/providers/ProviderPicker').then(module => ({ default: module.ProviderPicker })));
const GroupSelector = lazy(() => import('../features/groups/GroupSelector').then(module => ({ default: module.GroupSelector })));
const LibraryTools = lazy(() => import('../features/settings/LibraryTools').then(module => ({ default: module.LibraryTools })));
const LearningCenter = lazy(() => import('../features/help/LearningCenter').then(module => ({ default: module.LearningCenter })));

const blank: PromptDraft = { title: '', templateText: '', description: '', categoryId: 0 };
const noValues: Record<string, string> = {};
function initialTheme() { try { return localStorage.getItem('nova-theme') === 'light' ? 'light' : 'dark'; } catch { return 'dark'; } }
export default function App() {
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
  const [editingId, setEditingId] = useState<string | null>(null);
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
  const focusSearchAfterTransition = useRef(false);
  const opening = useRef<AbortController | null>(null);
  const editorDrafts = useRef(new Map<string, PromptDraft>());
  const [command, setCommand] = useState<LocalCommand | null>(null);
  const [commandBusy, setCommandBusy] = useState(false); const [commandError, setCommandError] = useState('');
  const commandController = useRef<AbortController | null>(null);
  const library = useLibrary(search, filter, groupId, page, version);
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
  useEffect(() => () => commandController.current?.abort(), []);
  async function executeCommand(resolution: LocalCommand, prompt: Prompt, instruction: string, signal: AbortSignal) {
    const fresh = await request(`/prompts/${prompt.id}`, promptSchema, { signal });
    if (signal.aborted) return;
    if (fresh.isArchived) throw new Error('Ese prompt está archivado. Recupéralo antes de editarlo.');
    if (resolution.kind === 'UPDATE' && !instruction.trim()) {
      setCommand({ ...resolution, candidates: [fresh], message: '¿Qué cambio concreto quieres hacer?' }); setCommandError(''); return;
    }
    let text = fresh.templateText;
    if (resolution.kind === 'UPDATE') {
      if (editorDrafts.current.has(fresh.id)) throw new Error('Tienes una edición sin guardar de este prompt. Guárdala antes de pedir otro cambio.');
      const proposal = await request('/nova/propose', proposalSchema, post({ intent: 'improve', context: instruction, promptId: fresh.id, revision: fresh.revision, model: 'qwen3:8b' }, signal));
      if (proposal.templateId !== fresh.id || proposal.baseRevision !== fresh.revision) throw new Error('La propuesta no corresponde a la versión seleccionada.');
      text = proposal.text;
    }
    if (signal.aborted) return;
    opening.current?.abort(); setCommand(null); setError(''); setNovaState('idle'); setSelected(fresh);
    if (resolution.kind === 'OPEN') setMode('focus');
    else {
      setEditingId(fresh.id);
      const next = editorDrafts.current.get(fresh.id) ?? { title: fresh.title, templateText: text, description: fresh.description, categoryId: fresh.categoryId, revision: fresh.revision };
      editorDrafts.current.set(fresh.id, next); setDraft(next); setMode('create');
      setToast(resolution.kind === 'UPDATE' ? 'Cambio preparado. Revisa el contenido y pulsa Guardar versión para aplicarlo.' : 'Editando el prompt seleccionado.');
    }
  }
  async function handleCommand(message: string, signal: AbortSignal) {
    const resolution = await request('/nova/command', commandSchema, post({ message }, signal));
    if (signal.aborted || resolution.kind === 'NONE') return false;
    if (['REFRESH', 'LIBRARY', 'FAVORITES', 'RECENT', 'ARCHIVE'].includes(resolution.kind)) {
      setSearch(''); setIntention(''); setPage(0); setGroupId(''); setFilter(resolution.kind === 'FAVORITES' ? 'favorites' : resolution.kind === 'RECENT' ? 'recent' : resolution.kind === 'ARCHIVE' ? 'archived' : 'all'); setVersion(value => value + 1); setNovaState('idle');
      setToast(resolution.message); return true;
    }
    if (resolution.candidates.length !== 1 || resolution.kind === 'UPDATE' && !resolution.instruction.trim()) { setCommand(resolution); setCommandError(''); }
    else await executeCommand(resolution, resolution.candidates[0], resolution.instruction, signal);
    return true;
  }
  function closeCommand() { commandController.current?.abort(); setCommand(null); setCommandBusy(false); }
  async function clarifyCommand(work: (signal: AbortSignal) => Promise<unknown>) {
    commandController.current?.abort(); const current = new AbortController(); commandController.current = current;
    setCommandBusy(true); setCommandError('');
    try { await work(current.signal); } catch (error) { if (!current.signal.aborted) setCommandError(errorMessage(error)); }
    finally { if (!current.signal.aborted) setCommandBusy(false); }
  }
  function discover() { opening.current?.abort(); setMode('discover'); setError(''); }
  function back() { if (mode === 'create') setDiscardOpen(true); else discover(); }
  async function open(prompt: Prompt) {
    opening.current?.abort(); const current = new AbortController(); opening.current = current;
    setError('');
    try { const fresh = await request(`/prompts/${prompt.id}`, promptSchema, { signal: current.signal }); if (!current.signal.aborted) { setSelected(fresh); setMode('focus'); } }
    catch (error) { if (!current.signal.aborted) setError(errorMessage(error)); }
  }
  function create() { opening.current?.abort(); setEditingId(null); setDraft(editorDrafts.current.get('new') ?? { ...blank }); setMode('create'); setError(''); }
  function changeDraft(next: PromptDraft) { editorDrafts.current.set(editingId ?? 'new', next); setDraft(next); }
  function edit() { if (!selected) return; setEditingId(selected.id); setDraft(editorDrafts.current.get(selected.id) ?? { title: selected.title, templateText: selected.templateText, description: selected.description, categoryId: selected.categoryId, revision: selected.revision }); setMode('create'); setError(''); }
  async function save() {
    if (saving) return;
    setSaving(true); setError('');
    try {
      const prompt = await request(editingId ? `/prompts/${editingId}` : '/prompts', promptSchema, { method: editingId ? 'PUT' : 'POST', body: JSON.stringify(draft) });
      editorDrafts.current.delete(editingId ?? 'new');
      setSelected(prompt); setDraft({ ...blank }); setEditingId(null); setMode('focus'); setVersion(v => v + 1); setToast('Prompt guardado en tu biblioteca local.');
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
    const nextDraft = { title: source?.title ?? '', templateText: text, description: source?.description ?? '', categoryId: source?.categoryId ?? 0, revision: source?.revision };
    setEditingId(source?.id ?? null);
    editorDrafts.current.set(source?.id ?? 'new', nextDraft); setDraft(nextDraft);
    setMode('create'); setError('');
  }
  return <MotionConfig reducedMotion="user"><LazyMotion features={domAnimation}><div className="app-shell">
    <a className="skip-link" href="#main">Ir al contenido</a>
    <header className="app-header"><button className="brand" onClick={() => mode === 'create' ? setDiscardOpen(true) : discover()} aria-label="QuickPrompt NOVA, biblioteca"><span className="brand-mark" aria-hidden="true">q<span>p</span></span><span>QuickPrompt <span className="brand-nova">NOVA</span></span></button>
      <div className="header-actions"><span className="local-indicator"><i />Local y privado</span><button className="icon-button" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')} aria-label={theme === 'dark' ? 'Activar tema claro' : 'Activar tema oscuro'}>{theme === 'dark' ? <Sun size={20} aria-hidden="true" /> : <Moon size={20} aria-hidden="true" />}</button><button className="icon-button" onClick={() => setDataOpen(true)} aria-label="Datos y respaldos"><Database size={19} aria-hidden="true" /></button><button className="icon-button" onClick={() => setHelpOpen(true)} aria-label="Ayuda"><CircleHelp size={20} aria-hidden="true" /></button></div>
    </header>
    <div className={`toast-region${mode === 'focus' || providerOpen || dataOpen || groupsOpen ? ' sr-only' : ''}`} role="status" aria-live="polite" aria-atomic="true">{toast && <div className="toast">{toast}</div>}</div>
    <main id="main" tabIndex={-1}>
      <AnimatePresence mode="wait" initial={false}><m.div key={mode} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -4 }} transition={{ duration: 0.2 }} onAnimationComplete={() => { if (mode === 'discover' && focusSearchAfterTransition.current) { searchRef.current?.focus(); focusSearchAfterTransition.current = false; } }}>
        {mode === 'discover' && <>
          <section className="discovery" aria-labelledby="discover-heading"><NovaCore state={novaState !== 'idle' ? novaState : library.loading ? 'searching' : search ? 'listening' : 'idle'} /><h1 id="discover-heading">Tus ideas.<br /><span>Listas para actuar.</span></h1><p className="hero-description">Elige un prompt, completa tus datos y úsalo con tu IA.<br />O cuéntale a NOVA qué instrucción necesitas.</p><button className="text-button learning-entry" onClick={() => setHelpOpen(true)}><CircleHelp size={18} aria-hidden="true" />Empieza aquí: guía y videos</button>
            <PreparationCanvas inputRef={searchRef} initialMessage={intention} onMessage={setIntention} onSearch={value => { setSearch(value); setPage(0); }} onState={setNovaState} onSaved={() => setVersion(value => value + 1)} onCommand={handleCommand} />
          </section>
          <section className="library-section" aria-labelledby="library-heading" aria-busy={library.loading}>
            <div className="section-heading"><h2 id="library-heading"><Library size={19} aria-hidden="true" />Tu biblioteca <span className="count">{library.total}</span></h2><button className="text-button accent" onClick={create}><Plus size={18} aria-hidden="true" />Nuevo prompt</button></div>
            <div className="library-filters"><div className="filter-tabs" role="group" aria-label="Filtrar biblioteca">{([['all', 'Todos'], ['favorites', 'Favoritos'], ['recent', 'Recientes'], ['archived', 'Archivo']] as const).map(([value, label]) => <button key={value} aria-pressed={filter === value} onClick={() => { setFilter(value); setPage(0); }}>{label}</button>)}</div><div className="collection-filter"><label className="group-filter"><span className="sr-only">Colección</span><select aria-label="Colección" value={groupId} onChange={e => { setGroupId(e.target.value); setPage(0); }}><option value="">Todas las colecciones</option>{groups.map(({ group }) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><button className="icon-button" aria-label="Gestionar colecciones" onClick={() => setGroupsOpen(true)}><FolderPlus size={18} aria-hidden="true" /></button></div></div>
            {library.error ? <div className="empty-state"><h3>No pudimos abrir la biblioteca.</h3><p role="alert" className="error">{library.error}</p><button className="secondary" onClick={() => setVersion(v => v + 1)}>Reintentar</button></div> : library.loading ? <p className="loading-state" role="status">Buscando tus prompts…</p> : library.items.length ? <PromptStream items={library.items} open={open} /> : <div className="empty-state"><h3>{search ? 'Aquí empieza una nueva idea.' : filter === 'all' && !groupId ? 'Tu primera instrucción te espera.' : 'Todavía no hay prompts aquí.'}</h3><p className="muted">{search ? 'No hay coincidencias en tu biblioteca. Prueba otras palabras o crea un prompt.' : 'Guarda una instrucción y reutilízala cuando la necesites.'}</p><button className="secondary" onClick={create}><Plus size={17} aria-hidden="true" />Crear prompt</button></div>}
            {library.total > 12 && <nav className="pagination" aria-label="Páginas"><button className="icon-button" disabled={page === 0 || library.loading} onClick={() => setPage(p => p - 1)} aria-label="Página anterior"><ChevronLeft size={20} aria-hidden="true" /></button><span>Página {page + 1} de {Math.ceil(library.total / 12)}</span><button className="icon-button" disabled={(page + 1) * 12 >= library.total || library.loading} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente"><ChevronRight size={20} aria-hidden="true" /></button></nav>}
          </section>
        </>}
        {mode === 'focus' && selected && <PromptFocus key={selected.id} prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onValues={values => setValuesByPrompt(previous => ({ ...previous, [selected.id]: values }))} onBack={discover} onEdit={edit} onAction={action} onDelete={() => setDeleteOpen(true)} onGroups={() => setGroupsOpen(true)} onUsed={() => setVersion(v => v + 1)} notify={setToast} feedback={toast} onUse={() => setProviderOpen(true)} assistance={<Suspense fallback={null}><NovaAssist message={selected.description} source={selected} onOpen={open} onDraft={aiDraft} onValues={values => setValuesByPrompt(previous => ({ ...previous, [selected.id]: { ...previous[selected.id], ...values } }))} /></Suspense>} />}
        {mode === 'create' && <InlinePromptEditor draft={draft} onChange={changeDraft} onSave={save} onBack={back} saving={saving} error={error} editing={!!editingId} />}
      </m.div></AnimatePresence>
      {error && mode !== 'create' && <p className="error global-error" role="alert">{error}</p>}
    </main>
    <footer className="app-footer"><span>Un espacio para pensar mejor.</span><span>HECHO PARA REUTILIZAR</span></footer>
    {providerOpen && selected && <Suspense fallback={<p className="loading-state" role="status">Preparando destinos…</p>}><ProviderPicker prompt={selected} values={valuesByPrompt[selected.id] ?? noValues} onClose={() => setProviderOpen(false)} notify={setToast} onUsed={() => setVersion(v => v + 1)} /></Suspense>}
    {groupsOpen && <Suspense fallback={null}><GroupSelector prompt={mode === 'focus' ? selected ?? undefined : undefined} onClose={() => setGroupsOpen(false)} onUpdated={() => { setVersion(v => v + 1); setGroupId(''); }} /></Suspense>}
    {dataOpen && <Suspense fallback={null}><LibraryTools onClose={() => setDataOpen(false)} onUpdated={() => { setVersion(v => v + 1); setSelected(null); setMode('discover'); setValuesByPrompt({}); }} notify={setToast} /></Suspense>}
    <Dialog open={deleteOpen} onOpenChange={open => !saving && setDeleteOpen(open)} title="¿Eliminar este prompt?" description="Se eliminarán la plantilla y sus versiones. El historial de textos utilizados se conserva. Puedes archivarlo si quieres recuperarlo después."><div className="dialog-actions"><button className="secondary" onClick={() => setDeleteOpen(false)}>Cancelar</button><button className="destructive" disabled={saving} onClick={remove}>{saving ? 'Eliminando…' : 'Eliminar definitivamente'}</button></div>{error && <p className="error" role="alert">{error}</p>}</Dialog>
    <Dialog open={discardOpen} onOpenChange={setDiscardOpen} title="¿Salir del editor?" description="Tu borrador se conservará durante esta sesión. Guarda una versión para conservarlo en SQLite."><div className="dialog-actions"><button className="secondary" onClick={() => setDiscardOpen(false)}>Seguir editando</button><button className="primary" onClick={() => { setDiscardOpen(false); setMode(selected && editingId ? 'focus' : 'discover'); }}>Salir del editor</button></div></Dialog>
    {helpOpen && <Suspense fallback={<p role="status">Abriendo la guía…</p>}><LearningCenter onClose={() => setHelpOpen(false)} onImported={() => setVersion(value => value + 1)} /></Suspense>}
    {command && <CommandDialog key={`${command.kind}-${command.candidates.map(prompt => prompt.id).join(',')}`} command={command} busy={commandBusy} error={commandError} onClose={closeCommand} onReturnFocus={() => searchRef.current?.focus()} onChoose={(prompt, instruction) => { void clarifyCommand(signal => executeCommand(command, prompt, instruction, signal)); }} onResolve={title => { void clarifyCommand(signal => handleCommand(`${command.kind === 'UPDATE' ? 'Actualiza' : command.kind === 'EDIT' ? 'Edita' : 'Abre'} el prompt «${title}»${command.instruction ? ': ' + command.instruction : ''}`, signal)); }} />}
  </div></LazyMotion></MotionConfig>;
}
