import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useEffect, useState } from 'react';
import { Check, Plus, Trash2 } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, type Group, type Prompt } from '../../services/api';


export function GroupSelector({ prompt, onClose, onUpdated }: { prompt?: Prompt; onClose: () => void; onUpdated: () => void }) {
  const { collections } = useApplicationServices();
  const [groups, setGroups] = useState<Group[]>([]);
  const [members, setMembers] = useState<string[]>([]);
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<Group | null>(null);
  async function reload() {
    const [nextGroups, nextMembers] = await Promise.all([collections.list(), prompt ? collections.memberships(prompt.id) : Promise.resolve([])]);
    setGroups(nextGroups); setMembers(nextMembers);
  }
  useEffect(() => { const controller = new AbortController();
    Promise.all([collections.list(controller.signal), prompt ? collections.memberships(prompt.id, controller.signal) : Promise.resolve([])])
      .then(([nextGroups, nextMembers]) => { if (!controller.signal.aborted) { setGroups(nextGroups); setMembers(nextMembers); } })
      .catch(error => { if (!controller.signal.aborted) setError(errorMessage(error)); });
    return () => controller.abort();
  }, [prompt?.id]);
  async function work(action: () => Promise<unknown>) {
    setBusy(true); setError('');
    try { await action(); await reload(); onUpdated(); }
    catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Tus colecciones" description={prompt ? `Organiza «${prompt.title}» sin duplicar su contenido.` : 'Crea colecciones cuando las necesites. Cada prompt puede pertenecer a varias.'}>
    <div className="collection-list">{groups.map(item => <form className="collection-row" key={item.group.id} onSubmit={e => { e.preventDefault(); const nextName = String(new FormData(e.currentTarget).get('group-name')).trim(); if (nextName !== item.group.name) work(() => collections.rename(item.group, nextName)); }}>
      {prompt && <label className="collection-check"><input type="checkbox" checked={members.includes(item.group.id)} disabled={busy} onChange={e => {
        const member = e.target.checked; const previous = members;
        setMembers(member ? [...members, item.group.id] : members.filter(id => id !== item.group.id));
        work(async () => { try { await collections.setMember(prompt, item.group.id, member); } catch (error) { setMembers(previous); throw error; } });
      }} /><span className="sr-only">Asignar {item.group.name}</span></label>}
      <input name="group-name" aria-label={`Nombre de colección ${item.group.name}`} defaultValue={item.group.name} key={item.group.id + item.group.revision} maxLength={80} disabled={busy} />
      <button className="icon-button" disabled={busy} aria-label={`Guardar nombre de ${item.group.name}`}><AppIcon glyph={Check} size={16} aria-hidden="true"  /></button>
      <button type="button" className="icon-button danger" disabled={busy} aria-label={`Eliminar colección ${item.group.name}`} onClick={() => setDeleting(item)}><AppIcon glyph={Trash2} size={16} aria-hidden="true"  /></button>
    </form>)}</div>
    {!groups.length && <p className="muted">Aún no tienes colecciones.</p>}
    <form className="collection-create" onSubmit={e => { e.preventDefault(); work(async () => { await collections.create(name.trim()); setName(''); }); }}><label htmlFor="collection-name">Nueva colección</label><div><input id="collection-name" value={name} maxLength={80} onChange={e => setName(e.target.value)} required placeholder="Por ejemplo, Escritura" /><button className="icon-button secondary" disabled={busy || !name.trim()} aria-label="Crear colección"><AppIcon glyph={Plus} size={20} aria-hidden="true"  /></button></div></form>
    {deleting && <div className="notice"><p>¿Eliminar «{deleting.group.name}»? Sus prompts se conservan.</p><div className="dialog-actions"><button className="text-button" onClick={() => setDeleting(null)}>Cancelar eliminación</button><button className="destructive" disabled={busy} onClick={() => work(async () => { await collections.remove(deleting.group, true); setDeleting(null); })}>Confirmar eliminación</button></div></div>}
    {error && <p className="error" role="alert">{error}</p>}
    <div className="dialog-actions"><button className="secondary" disabled={busy} onClick={onClose}>Listo</button></div>
  </Dialog>;
}
