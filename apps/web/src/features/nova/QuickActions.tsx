import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useId, useRef, useState } from 'react';
import { ArrowUpRight, Copy, ListChecks, MoreHorizontal, Pencil, Play, Plus, Search, Trash2 } from 'lucide-react';
import { AnimatePresence, m, useReducedMotionConfig } from 'motion/react';
import { panelMotion, stillPanelMotion } from '../../design/motion';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, type Prompt } from '../../services/api';

import { useLibrary } from '../../hooks/useLibrary';
import type { LiveCommand } from '../../services/live';

const actions = [
  { id: 'create', label: 'Crear prompt', icon: Plus, question: '¿Qué prompt quieres crear?', hint: 'Describe para qué lo necesitas y qué resultado esperas.', submit: 'Crear con NOVA' },
  { id: 'update', label: 'Actualizar prompt', icon: Pencil, question: '¿Qué quieres cambiar?', hint: 'Por ejemplo: hacerlo más breve, cambiar el tono o añadir una sección.', submit: 'Preparar cambios' },
  { id: 'find', label: 'Buscar por significado', icon: Search, question: '¿Qué prompt necesitas?', hint: 'Describe su propósito con tus palabras. NOVA buscará entre los prompts guardados.', submit: 'Buscar con IA' },
  { id: 'open', label: 'Buscar y abrir', icon: Search, submit: 'Abrir con NOVA' },
  { id: 'delete', label: 'Eliminar prompt', icon: Trash2, submit: 'Revisar eliminación' },
  { id: 'fill', label: 'Completar variables', icon: ListChecks, submit: 'Completar con NOVA' },
  { id: 'prepare', label: 'Preparar para una IA', icon: ArrowUpRight, submit: 'Preparar con NOVA' },
  { id: 'execute', label: 'Ejecutar con IA local', icon: Play, submit: 'Ejecutar ahora' },
  { id: 'copy', label: 'Copiar con NOVA', icon: Copy, submit: 'Copiar ahora' },
  { id: 'adapt-copy', label: 'Adaptar y preparar copia', icon: Copy, question: '¿Qué quieres adaptar?', hint: 'Describe el cambio. Después elige plantilla con marcadores o texto rellenado.', submit: 'Adaptar y preparar' },
] as const;
type Action = typeof actions[number];
type Destination = { providerId: string; displayName: string };

// These instructions represent explicit button choices, never classification of free text.
function instruction(action: Action, details: string, values: Record<string, string>, destination?: Destination, copyContent: 'TEMPLATE' | 'RENDERED' = 'RENDERED') {
  // The typed command already owns the operation. Generated authoring prose
  // must not become apparent user authority for title/content review.
  if (action.id === 'create' || action.id === 'update' || action.id === 'find') return details.trim();
  // Identity/revision travel as typed source context, never as part of the task text.
  const subject = 'el prompt seleccionado';
  const inputs = Object.entries(values).filter(([, value]) => value.trim()).map(([name, value]) => `${name}: ${value}`).join('\n');
  switch (action.id) {
    case 'adapt-copy': return `Adapta ${subject} según estos cambios:\n${details.trim()}\nPrepara ${copyContent === 'TEMPLATE' ? 'la plantilla con sus marcadores' : 'el texto rellenado'} para copiar, sin copiar ahora, guardar ni ejecutar.\n${copyContent === 'RENDERED' ? inputs : ''}`;
    case 'open': return `Abre ${subject} para verlo, sin modificar su contenido ni ejecutar su tarea.`;
    case 'fill': return `En ${subject}, completa las variables con estos valores, sin reescribir su plantilla ni ejecutar su tarea:\n${inputs}`;
    case 'prepare': return `Prepara ${subject} para ${destination!.displayName}, sin ejecutar ni enviar.\n${inputs}`;
    case 'execute': return `Ejecuta ahora ${subject} con Qwen local, sin modificar su plantilla.\n${inputs}`;
    case 'copy': return copyContent === 'TEMPLATE' ? `Copia ahora al portapapeles la plantilla de ${subject} conservando sus marcadores, sin modificarla ni ejecutar su tarea.` : `Copia ahora al portapapeles ${subject} con estos valores, sin modificar ni ejecutar su plantilla.\n${inputs}`;
    default: throw new Error('Elige un prompt para revisar la eliminación.');
  }
}

function PromptChoice({ onChoose, allowArchived }: { onChoose: (prompt: Prompt) => void; allowArchived: boolean }) {
  const [query, setQuery] = useState(''), [page, setPage] = useState(0), [archived, setArchived] = useState(false), [retry, setRetry] = useState(0);
  const library = useLibrary(query, archived ? 'archived' : 'all', '', page, retry);
  const id = useId();
  return <div className="quick-prompt-choice">
    <label htmlFor={id}>¿Qué prompt?</label>
    <input id={id} value={query} maxLength={200} placeholder="Busca por título o contenido…" onChange={event => { setQuery(event.target.value); setPage(0); }} />
    {allowArchived && <label className="remember-draft"><input type="checkbox" checked={archived} onChange={event => { setArchived(event.target.checked); setPage(0); }} />Buscar en el archivo</label>}
    {library.loading ? <p role="status">Buscando prompts…</p> : library.error ? <div><p role="alert" className="error">{library.error}</p><button type="button" className="secondary" onClick={() => setRetry(value => value + 1)}>Reintentar búsqueda</button></div> : <>
      <div className="quick-prompt-results" role="group" aria-label="Elegir prompt">{library.items.map(prompt => <button type="button" className="secondary candidate-choice" key={prompt.id} onClick={() => onChoose(prompt)}><strong>{prompt.title}</strong><span className="muted">{prompt.description || prompt.templateText.slice(0, 160)}</span></button>)}</div>
      {!library.items.length && <p role="status" className="muted">No hay coincidencias. Prueba con otras palabras.</p>}
      {library.total > 12 && <nav className="pagination" aria-label="Resultados de prompts"><button type="button" className="text-button" disabled={page === 0} onClick={() => setPage(value => value - 1)}>Anterior</button><span>{page + 1} / {Math.ceil(library.total / 12)}</span><button type="button" className="text-button" disabled={(page + 1) * 12 >= library.total} onClick={() => setPage(value => value + 1)}>Siguiente</button></nav>}
    </>}
  </div>;
}

function ActionForm({ action, initialPrompt, onRequest, onRemove, onClose }: {
  action: Action; initialPrompt?: Prompt; onRequest: (message: string, source?: Prompt, values?: Record<string, string>, command?: LiveCommand) => Promise<void>;
  onRemove: (prompt: Prompt) => Promise<void>; onClose: () => void;
}) {
  const { providers: providerClient } = useApplicationServices();
  const [prompt, setPrompt] = useState<Prompt | null>(action.id === 'create' || action.id === 'find' ? null : initialPrompt ?? null);
  const [details, setDetails] = useState(''), [values, setValues] = useState<Record<string, string>>({});
  const [copyContent, setCopyContent] = useState<'TEMPLATE' | 'RENDERED'>(action.id === 'adapt-copy' ? 'TEMPLATE' : 'RENDERED');
  const [providers, setProviders] = useState<Destination[]>([]), [providerId, setProviderId] = useState('');
  const [providerError, setProviderError] = useState(''), [providerRetry, setProviderRetry] = useState(0);
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  const submitting = useRef(false), id = useId();
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const needsValues = action.id === 'fill' || action.id === 'prepare' || action.id === 'execute' || (action.id === 'copy' || action.id === 'adapt-copy') && copyContent === 'RENDERED';
  useEffect(() => {
    if (action.id !== 'prepare') return;
    const controller = new AbortController(); setProviderError('');
    providerClient.list(controller.signal).then(items => {
      if (!controller.signal.aborted) setProviders(items.filter(item => item.modesSupported.includes('COPY_AND_OPEN') || item.modesSupported.includes('LOCAL_EXECUTION')));
    }).catch(error => { if (!controller.signal.aborted) setProviderError(errorMessage(error)); });
    return () => controller.abort();
  }, [action.id, providerRetry]);
  const destination = providers.find(item => item.providerId === providerId);
  const needsDetails = 'question' in action;
  const detailLimit = action.id === 'update' ? Math.max(0, 4000 - instruction(action, '', {}).length) : 8000;
  const independent = action.id === 'create' || action.id === 'find';
  const ready = (independent || !!prompt) && (!needsDetails || !!details.trim())
    && (action.id !== 'prepare' || !!destination)
    && (action.id !== 'fill' || !!prompt?.variableNames.some(name => values[name]?.trim()));
  async function submit() {
    if (!ready || submitting.current) return;
    submitting.current = true; setBusy(true); setError('');
    try {
      if (action.id === 'delete') await onRemove(prompt!);
      else {
        const message = instruction(action, details, values, destination, copyContent);
        if (message.length > 16000) throw new Error('La solicitud es demasiado larga. Reduce los detalles o los valores e inténtalo de nuevo.');
        const supplied = (action.id === 'copy' || action.id === 'adapt-copy') && copyContent === 'TEMPLATE' ? {} : Object.fromEntries(Object.entries(values).filter(([name, value]) => prompt?.variableNames.includes(name) && value.trim()));
        const operations = { create: 'CREATE_DRAFT', find: 'SEARCH_PROMPT', update: 'PROPOSE_ADAPTATION', 'adapt-copy': 'PROPOSE_ADAPTATION', open: 'OPEN_PROMPT', fill: 'FILL_VARIABLES', prepare: 'PREPARE_PROVIDER', execute: 'EXECUTE_LOCAL', copy: 'COPY_TO_CLIPBOARD' } as const;
        await onRequest(message, prompt ?? undefined, supplied, { operation: operations[action.id], content: details.trim(),
          ...(action.id === 'execute' ? { providerId: 'ollama' } : destination ? { providerId: destination.providerId } : {}),
          ...(action.id === 'copy' || action.id === 'adapt-copy' ? { copyContent } : {}),
          ...(action.id === 'adapt-copy' ? { requestedOutcomes: ['PROPOSE_ADAPTATION', 'COPY_PROMPT'] } : {}) });
      }
      if (mounted.current) onClose();
    } catch (error) { setError(errorMessage(error)); }
    finally { submitting.current = false; setBusy(false); }
  }
  return <form className="quick-action-form" data-nova-command="true" onSubmit={event => { event.preventDefault(); void submit(); }}>
    {!independent && (!prompt ? <PromptChoice allowArchived={action.id === 'delete'} onChoose={selected => { setPrompt(selected); setValues({}); setError(''); }} /> : <div className="quick-selected"><div><span className="muted">Prompt elegido</span><strong>{prompt.title}</strong><p className="muted">{prompt.description || prompt.templateText.slice(0, 160)}</p></div><button type="button" className="text-button" disabled={busy} onClick={() => { setPrompt(null); setValues({}); }}>Elegir otro</button></div>)}
    {(independent || prompt) && <>
      {needsDetails && <div className="prepared-field"><label htmlFor={`${id}-details`}>{action.question}</label><textarea id={`${id}-details`} rows={4} maxLength={detailLimit} required disabled={busy} value={details} onChange={event => setDetails(event.target.value)} aria-describedby={`${id}-hint`} /><p className="muted" id={`${id}-hint`}>{action.hint}</p></div>}
      {(action.id === 'copy' || action.id === 'adapt-copy') && <fieldset disabled={busy}><legend>¿Qué quieres copiar?</legend>
        <label><input type="radio" name={`${id}-copy-content`} checked={copyContent === 'TEMPLATE'} onChange={() => setCopyContent('TEMPLATE')} />Plantilla con marcadores</label>
        <label><input type="radio" name={`${id}-copy-content`} checked={copyContent === 'RENDERED'} onChange={() => setCopyContent('RENDERED')} />Texto rellenado</label>
      </fieldset>}
      {needsValues && prompt && <fieldset className="quick-values" disabled={busy}><legend>{action.id === 'fill' ? '¿Qué datos quieres completar?' : 'Datos para usar el prompt'}</legend>{prompt.variableNames.length ? prompt.variableNames.map((name, index) => <div className="prepared-field" key={name}><label htmlFor={`${id}-value-${index}`}>{name}</label><textarea id={`${id}-value-${index}`} rows={2} maxLength={8000} value={values[name] ?? ''} onChange={event => setValues(previous => ({ ...previous, [name]: event.target.value }))} /></div>) : <p className="muted">Este prompt no tiene variables.</p>}<p className="muted">{action.id === 'fill' ? 'Completa al menos un campo. Los demás conservan su valor.' : 'Si falta algún dato necesario, NOVA te lo preguntará.'}</p></fieldset>}
      {action.id === 'prepare' && <div className="prepared-field"><label htmlFor={`${id}-provider`}>¿En qué IA quieres usarlo?</label><select id={`${id}-provider`} value={providerId} disabled={busy} required onChange={event => setProviderId(event.target.value)}><option value="">Elige un destino</option>{providers.map(provider => <option key={provider.providerId} value={provider.providerId}>{provider.displayName}</option>)}</select>{providerError && <div><p role="alert" className="error">{providerError}</p><button type="button" className="text-button" onClick={() => setProviderRetry(value => value + 1)}>Reintentar destinos</button></div>}<p className="muted">Prepara el texto; tú decides cuándo enviarlo.</p></div>}
      {action.id === 'execute' && <p className="notice">Al continuar, se enviará el prompt completo al modelo local para generar una respuesta.</p>}
      {action.id === 'delete' && <p className="notice">Todavía no se eliminará. Primero verás la confirmación del prompt elegido.</p>}
    </>}
    {error && <p className="error" role="alert">{error}</p>}
    <div className="dialog-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancelar</button><button type="submit" className="primary" disabled={!ready || busy}>{busy ? 'Preparando…' : action.submit}</button></div>
  </form>;
}

export function QuickActions({ source, disabled = false, onRequest, onRemove, compact = false }: {
  source?: Prompt; disabled?: boolean; onRequest: (message: string, source?: Prompt, values?: Record<string, string>, command?: LiveCommand) => Promise<void>; onRemove: (prompt: Prompt) => Promise<void>; compact?: boolean;
}) {
  const [action, setAction] = useState<Action | null>(null);
  const reduced = useReducedMotionConfig();
  const [moreOpen, setMoreOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const moreTrigger = useRef<HTMLButtonElement>(null), root = useRef<HTMLElement>(null), menuId = useId();
  useEffect(() => {
    if (!moreOpen) return;
    const outside = (event: PointerEvent) => { if (event.target instanceof Node && !root.current?.contains(event.target)) setMoreOpen(false); };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setMoreOpen(false); moreTrigger.current?.focus(); } };
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [moreOpen]);
  function button(item: Action) {
    const Icon = item.icon;
    return <button type="button" className="secondary quick-action" key={item.id} disabled={disabled} aria-label={item.label} title={item.label} aria-haspopup="dialog" onClick={event => { trigger.current = event.currentTarget; setMoreOpen(false); setAction(item); }}><AppIcon glyph={Icon} size={20} aria-hidden="true"  /><span>{item.label}</span></button>;
  }
  return <section ref={root} className={`nova-quick-actions${compact ? ' quick-actions-compact' : ''}`} aria-label="Acciones rápidas de NOVA">
    {!compact && <p className="quick-actions-caption">Elige una acción. NOVA te pide solo los detalles.</p>}
    <div className="quick-action-buttons">{actions.slice(0, 4).map(button)}{compact && <button ref={moreTrigger} type="button" className="icon-button quick-more-trigger" aria-label="Más acciones de NOVA" aria-expanded={moreOpen} aria-controls={moreOpen ? menuId : undefined} onClick={() => setMoreOpen(value => !value)}><AppIcon glyph={MoreHorizontal} size={20} aria-hidden="true"  /><span>Más acciones</span></button>}</div>
    {compact ? <AnimatePresence>{moreOpen && <m.div id={menuId} className="quick-actions-menu" aria-label="Más acciones de NOVA" role="group" variants={reduced ? stillPanelMotion : panelMotion} initial="closed" animate="open" exit="closed">{actions.slice(4).map(button)}</m.div>}</AnimatePresence> : <details className="quick-more"><summary>Más acciones</summary><div className="quick-action-buttons">{actions.slice(4).map(button)}</div></details>}
    <Dialog open={!!action} onOpenChange={open => { if (!open) setAction(null); }} title={action?.label ?? 'Acción rápida'} description="Elige los detalles y NOVA realizará la acción en esta misma pestaña." onCloseAutoFocus={event => { event.preventDefault(); if (trigger.current?.isConnected) trigger.current.focus(); else moreTrigger.current?.focus(); }}>
      {action && <ActionForm key={action.id} action={action} initialPrompt={source} onRequest={onRequest} onRemove={onRemove} onClose={() => setAction(null)} />}
    </Dialog>
  </section>;
}
