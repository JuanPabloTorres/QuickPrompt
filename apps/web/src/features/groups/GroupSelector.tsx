import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Check, Plus, Trash2 } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, type Group, type Prompt } from '../../services/api';
import type { CollectionClient } from '../../services/library';


export function GroupSelector({ prompt, onClose, onUpdated }: { prompt?: Prompt; onClose: () => void; onUpdated: () => void }) {
  const { collections } = useApplicationServices();
  return <CollectionManager collections={collections} prompt={prompt} onClose={onClose} onUpdated={onUpdated} />;
}

/** Same interaction for local and authenticated storage. The injected client owns transport and scope. */
export function CollectionManager({ collections, prompt, onClose, onUpdated, onFailure }: { collections: CollectionClient; prompt?: Pick<Prompt, 'id' | 'revision' | 'title'>; onClose: () => void; onUpdated: () => void; onFailure?: (failure: unknown) => void }) {
  const [groups, setGroups] = useState<Group[]>([]);
  const [members, setMembers] = useState<string[]>([]);
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [catalogReady, setCatalogReady] = useState(false);
  const [membersReady, setMembersReady] = useState(!prompt);
  const loadingRequest = useRef<AbortController | null>(null);
  const [deleting, setDeleting] = useState<Group | null>(null);
  const promptId = prompt?.id;
  const reload = useCallback(async () => {
    loadingRequest.current?.abort();
    const controller = new AbortController(); loadingRequest.current = controller;
    setLoading(true); setCatalogReady(false); setMembersReady(!promptId); setError('');
    // Catalog and membership are separate reads. One failed read must not turn a
    // populated catalog into an apparent empty library or authorize a blind toggle.
    const [catalog, membership] = await Promise.allSettled([collections.list(controller.signal), promptId ? collections.memberships(promptId, controller.signal) : Promise.resolve([])]);
    if (controller.signal.aborted) return;
    const failures: string[] = [];
    if (catalog.status === 'fulfilled') { setGroups(catalog.value); setCatalogReady(true); }
    else { failures.push(`No se pudieron cargar las colecciones: ${errorMessage(catalog.reason)}`); onFailure?.(catalog.reason); }
    if (membership.status === 'fulfilled') { setMembers(membership.value); setMembersReady(true); }
    else { failures.push(`No se pudieron cargar las asignaciones del prompt: ${errorMessage(membership.reason)}`); onFailure?.(membership.reason); }
    setError(failures.join(' ')); setLoading(false);
  }, [collections, promptId, onFailure]);
  useEffect(() => { void reload(); return () => loadingRequest.current?.abort(); }, [reload]);
  async function work(action: () => Promise<unknown>) {
    if (busy || loading) return;
    setBusy(true); setError('');
    try { await action(); await reload(); onUpdated(); }
    catch (error) { setError(errorMessage(error)); onFailure?.(error); } finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Tus colecciones" description={prompt ? `Organiza «${prompt.title}» sin duplicar su contenido.` : 'Crea colecciones cuando las necesites. Cada prompt puede pertenecer a varias.'}>
    {loading && <p className="muted" role="status">Cargando colecciones…</p>}
    {error && <div role="alert"><p className="error">{error}</p><button type="button" className="secondary" disabled={busy || loading} onClick={() => void reload()}>Reintentar carga</button></div>}
    <div className="collection-list" aria-busy={loading}>{groups.map(item => <form className="collection-row" key={item.group.id} onSubmit={e => { e.preventDefault(); const nextName = String(new FormData(e.currentTarget).get('group-name')).trim(); if (nextName !== item.group.name) work(() => collections.rename(item.group, nextName)); }}>
      {prompt && <label className="collection-check"><input type="checkbox" checked={members.includes(item.group.id)} disabled={busy || loading || !membersReady || !catalogReady} onChange={e => {
        if (busy || loading || !membersReady || !catalogReady) return;
        const member = e.target.checked; const previous = members;
        setMembers(member ? [...members, item.group.id] : members.filter(id => id !== item.group.id));
        work(async () => { try { await collections.setMember(prompt, item.group.id, member); } catch (error) { setMembers(previous); throw error; } });
      }} /><span className="sr-only">Asignar {item.group.name}</span></label>}
      <input name="group-name" aria-label={`Nombre de colección ${item.group.name}`} defaultValue={item.group.name} key={item.group.id + item.group.revision} maxLength={80} disabled={busy || loading || !catalogReady} />
      <button className="icon-button" disabled={busy || loading || !catalogReady} aria-label={`Guardar nombre de ${item.group.name}`}><AppIcon glyph={Check} size={16} aria-hidden="true"  /></button>
      <button type="button" className="icon-button danger" disabled={busy || loading || !catalogReady} aria-label={`Eliminar colección ${item.group.name}`} onClick={() => setDeleting(item)}><AppIcon glyph={Trash2} size={16} aria-hidden="true"  /></button>
    </form>)}</div>
    {!loading && catalogReady && !groups.length && <p className="muted">Aún no tienes colecciones.</p>}
    <form className="collection-create" onSubmit={e => { e.preventDefault(); work(async () => { await collections.create(name.trim()); setName(''); }); }}><label htmlFor="collection-name">Nueva colección</label><div><input id="collection-name" value={name} maxLength={80} onChange={e => setName(e.target.value)} disabled={busy || loading || !catalogReady} required placeholder="Por ejemplo, Escritura" /><button className="icon-button secondary" disabled={busy || loading || !catalogReady || !name.trim()} aria-label="Crear colección"><AppIcon glyph={Plus} size={20} aria-hidden="true"  /></button></div></form>
    {deleting && <div className="notice"><p>¿Eliminar «{deleting.group.name}»? Sus prompts se conservan.</p><div className="dialog-actions"><button className="text-button" onClick={() => setDeleting(null)}>Cancelar eliminación</button><button className="destructive" disabled={busy} onClick={() => work(async () => { await collections.remove(deleting.group, true); setDeleting(null); })}>Confirmar eliminación</button></div></div>}
    <div className="dialog-actions"><button className="secondary" disabled={busy} onClick={onClose}>Listo</button></div>
  </Dialog>;
}
