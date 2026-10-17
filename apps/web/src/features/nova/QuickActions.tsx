import { useEffect, useId, useRef, useState } from 'react';
import { ArrowUpRight, ListChecks, PencilLine, Play, Plus, Search, Trash2 } from 'lucide-react';
import { z } from 'zod';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, request, type Prompt } from '../../services/api';
import { useLibrary } from '../../hooks/useLibrary';

const actions = [
  { id: 'create', label: 'Crear prompt', icon: Plus, question: '¿Qué prompt quieres crear?', hint: 'Describe para qué lo necesitas y qué resultado esperas.', submit: 'Crear con NOVA' },
  { id: 'update', label: 'Actualizar prompt', icon: PencilLine, question: '¿Qué quieres cambiar?', hint: 'Por ejemplo: hacerlo más breve, cambiar el tono o añadir una sección.', submit: 'Preparar cambios' },
  { id: 'open', label: 'Buscar y abrir', icon: Search, submit: 'Abrir con NOVA' },
  { id: 'delete', label: 'Eliminar prompt', icon: Trash2, submit: 'Revisar eliminación' },
  { id: 'fill', label: 'Completar variables', icon: ListChecks, submit: 'Completar con NOVA' },
  { id: 'prepare', label: 'Preparar para una IA', icon: ArrowUpRight, submit: 'Preparar con NOVA' },
  { id: 'execute', label: 'Ejecutar con IA local', icon: Play, submit: 'Ejecutar ahora' },
] as const;
type Action = typeof actions[number];
type Destination = { providerId: string; displayName: string };
const providersSchema = z.array(z.object({ providerId: z.string(), displayName: z.string(), modesSupported: z.array(z.string()) }));

// These instructions represent explicit button choices, never classification of free text.
function instruction(action: Action, details: string, values: Record<string, string>, destination?: Destination) {
  if (action.id === 'create') return `Crea un nuevo prompt reutilizable desde cero para esta necesidad:\n${details.trim()}`;
  // Identity/revision travel as typed source context, never as part of the task text.
  const subject = 'el prompt seleccionado';
  const inputs = Object.entries(values).filter(([, value]) => value.trim()).map(([name, value]) => `${name}: ${value}`).join('\n');
  switch (action.id) {
    case 'update': return `Actualiza ${subject} según estos cambios:\n${details.trim()}`;
    case 'open': return `Abre ${subject} para verlo, sin modificar su contenido ni ejecutar su tarea.`;
    case 'fill': return `En ${subject}, completa las variables con estos valores, sin reescribir su plantilla ni ejecutar su tarea:\n${inputs}`;
    case 'prepare': return `Prepara ${subject} para ${destination!.displayName}, sin ejecutar ni enviar.\n${inputs}`;
    case 'execute': return `Ejecuta ahora ${subject} con Qwen local, sin modificar su plantilla.\n${inputs}`;
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
  action: Action; initialPrompt?: Prompt; onRequest: (message: string, source?: Prompt, values?: Record<string, string>) => Promise<void>;
  onRemove: (prompt: Prompt) => Promise<void>; onClose: () => void;
}) {
  const [prompt, setPrompt] = useState<Prompt | null>(action.id === 'create' ? null : initialPrompt ?? null);
  const [details, setDetails] = useState(''), [values, setValues] = useState<Record<string, string>>({});
  const [providers, setProviders] = useState<Destination[]>([]), [providerId, setProviderId] = useState('');
  const [providerError, setProviderError] = useState(''), [providerRetry, setProviderRetry] = useState(0);
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  const submitting = useRef(false), id = useId();
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const needsValues = action.id === 'fill' || action.id === 'prepare' || action.id === 'execute';
  useEffect(() => {
    if (action.id !== 'prepare') return;
    const controller = new AbortController(); setProviderError('');
    request('/providers', providersSchema, { signal: controller.signal }).then(items => {
      if (!controller.signal.aborted) setProviders(items.filter(item => item.modesSupported.includes('COPY_AND_OPEN') || item.modesSupported.includes('LOCAL_EXECUTION')));
    }).catch(error => { if (!controller.signal.aborted) setProviderError(errorMessage(error)); });
    return () => controller.abort();
  }, [action.id, providerRetry]);
  const destination = providers.find(item => item.providerId === providerId);
  const needsDetails = 'question' in action;
  const detailLimit = action.id === 'update' ? Math.max(0, 4000 - instruction(action, '', {}).length) : 8000;
  const ready = (action.id === 'create' || !!prompt) && (!needsDetails || !!details.trim())
    && (action.id !== 'prepare' || !!destination)
    && (action.id !== 'fill' || !!prompt?.variableNames.some(name => values[name]?.trim()));
  async function submit() {
    if (!ready || submitting.current) return;
    submitting.current = true; setBusy(true); setError('');
    try {
      if (action.id === 'delete') await onRemove(prompt!);
      else {
        const message = instruction(action, details, values, destination);
        if (message.length > 16000) throw new Error('La solicitud es demasiado larga. Reduce los detalles o los valores e inténtalo de nuevo.');
        const supplied = Object.fromEntries(Object.entries(values).filter(([name, value]) => prompt?.variableNames.includes(name) && value.trim()));
        if (Object.keys(supplied).length) await onRequest(message, prompt ?? undefined, supplied);
        else await onRequest(message, prompt ?? undefined);
      }
      if (mounted.current) onClose();
    } catch (error) { setError(errorMessage(error)); }
    finally { submitting.current = false; setBusy(false); }
  }
  return <form className="quick-action-form" data-nova-command="true" onSubmit={event => { event.preventDefault(); void submit(); }}>
    {action.id !== 'create' && (!prompt ? <PromptChoice allowArchived={action.id === 'delete'} onChoose={selected => { setPrompt(selected); setValues({}); setError(''); }} /> : <div className="quick-selected"><div><span className="muted">Prompt elegido</span><strong>{prompt.title}</strong><p className="muted">{prompt.description || prompt.templateText.slice(0, 160)}</p></div><button type="button" className="text-button" disabled={busy} onClick={() => { setPrompt(null); setValues({}); }}>Elegir otro</button></div>)}
    {(action.id === 'create' || prompt) && <>
      {needsDetails && <div className="prepared-field"><label htmlFor={`${id}-details`}>{action.question}</label><textarea id={`${id}-details`} rows={4} maxLength={detailLimit} required disabled={busy} value={details} onChange={event => setDetails(event.target.value)} aria-describedby={`${id}-hint`} /><p className="muted" id={`${id}-hint`}>{action.hint}</p></div>}
      {needsValues && prompt && <fieldset className="quick-values" disabled={busy}><legend>{action.id === 'fill' ? '¿Qué datos quieres completar?' : 'Datos para usar el prompt'}</legend>{prompt.variableNames.length ? prompt.variableNames.map((name, index) => <div className="prepared-field" key={name}><label htmlFor={`${id}-value-${index}`}>{name}</label><textarea id={`${id}-value-${index}`} rows={2} maxLength={8000} value={values[name] ?? ''} onChange={event => setValues(previous => ({ ...previous, [name]: event.target.value }))} /></div>) : <p className="muted">Este prompt no tiene variables.</p>}<p className="muted">{action.id === 'fill' ? 'Completa al menos un campo. Los demás conservan su valor.' : 'Si falta algún dato necesario, NOVA te lo preguntará.'}</p></fieldset>}
      {action.id === 'prepare' && <div className="prepared-field"><label htmlFor={`${id}-provider`}>¿En qué IA quieres usarlo?</label><select id={`${id}-provider`} value={providerId} disabled={busy} required onChange={event => setProviderId(event.target.value)}><option value="">Elige un destino</option>{providers.map(provider => <option key={provider.providerId} value={provider.providerId}>{provider.displayName}</option>)}</select>{providerError && <div><p role="alert" className="error">{providerError}</p><button type="button" className="text-button" onClick={() => setProviderRetry(value => value + 1)}>Reintentar destinos</button></div>}<p className="muted">Prepara el texto; tú decides cuándo enviarlo.</p></div>}
      {action.id === 'execute' && <p className="notice">Al continuar, se enviará el prompt completo al modelo local para generar una respuesta.</p>}
      {action.id === 'delete' && <p className="notice">Todavía no se eliminará. Primero verás la confirmación del prompt elegido.</p>}
    </>}
    {error && <p className="error" role="alert">{error}</p>}
    <div className="dialog-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancelar</button><button type="submit" className="primary" disabled={!ready || busy}>{busy ? 'Preparando…' : action.submit}</button></div>
  </form>;
}

export function QuickActions({ source, disabled = false, onRequest, onRemove }: {
  source?: Prompt; disabled?: boolean; onRequest: (message: string, source?: Prompt, values?: Record<string, string>) => Promise<void>; onRemove: (prompt: Prompt) => Promise<void>;
}) {
  const [action, setAction] = useState<Action | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  function button(item: Action) {
    const Icon = item.icon;
    return <button type="button" className="secondary quick-action" key={item.id} disabled={disabled} aria-haspopup="dialog" onClick={event => { trigger.current = event.currentTarget; setAction(item); }}><Icon size={17} aria-hidden="true" />{item.label}</button>;
  }
  return <section className="nova-quick-actions" aria-label="Acciones rápidas de NOVA">
    <p className="quick-actions-caption">Elige una acción. NOVA te pide solo los detalles.</p>
    <div className="quick-action-buttons">{actions.slice(0, 4).map(button)}</div>
    <details className="quick-more"><summary>Más acciones</summary><div className="quick-action-buttons">{actions.slice(4).map(button)}</div></details>
    <Dialog open={!!action} onOpenChange={open => { if (!open) setAction(null); }} title={action?.label ?? 'Acción rápida'} description="Elige los detalles y NOVA realizará la acción en esta misma pestaña." onCloseAutoFocus={event => { event.preventDefault(); if (trigger.current?.isConnected) trigger.current.focus(); }}>
      {action && <ActionForm key={action.id} action={action} initialPrompt={source} onRequest={onRequest} onRemove={onRemove} onClose={() => setAction(null)} />}
    </Dialog>
  </section>;
}
