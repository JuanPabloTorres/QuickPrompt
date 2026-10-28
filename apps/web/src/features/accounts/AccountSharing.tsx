import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Download, Mail, ShieldOff } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { AppIcon } from '../../components/ui/Icon';
import { InlineSpinner } from '../../components/ui/Feedback';
import { ApiError } from '../../services/api';
import type { AccountInvitation, AccountLibrary, AccountPrompt, AccountPublication, AccountService, SharedCollection, SharedPrompt } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';

/** Only owners reach this control; the server still checks owner and source revision on every write. */
export function PublishPromptDialog({ prompt, service, onExpired, onClose }: { prompt: AccountPrompt; service: AccountService; onExpired: () => void; onClose: () => void }) {
  const client = useMemo(() => ({
    list: (signal?: AbortSignal) => service.publications(prompt.libraryId, prompt.id, signal),
    publish: async (signal?: AbortSignal) => ({ ...await service.publish(prompt, signal), revokedAt: null }),
  }), [prompt, service]);
  return <PublicationManager title={prompt.title} revision={prompt.revision} preview={<pre>{prompt.template}</pre>} client={client} service={service} onExpired={onExpired} onClose={onClose} />;
}

type PublicationSummary = Pick<AccountPublication, 'id' | 'title' | 'sourceRevision' | 'createdAt' | 'revokedAt'>;
type PublicationClient = { list(signal?: AbortSignal): Promise<PublicationSummary[]>; publish(signal?: AbortSignal): Promise<PublicationSummary> };

/** Shared publication, invitation and revocation interaction. Source-specific review remains in the owning wrapper. */
export function PublicationManager({ title, revision, preview, client, service, onExpired, onClose }: { title: string; revision: number; preview: ReactNode; client: PublicationClient; service: AccountService; onExpired: () => void; onClose: () => void }) {
  const [publications, setPublications] = useState<PublicationSummary[]>();
  const [selected, setSelected] = useState('');
  const [email, setEmail] = useState('');
  const [confirmRevoke, setConfirmRevoke] = useState(false);
  const scope = useMemo(() => ({ client, service }), [client, service]);
  const operation = useAccountOperation(scope, onExpired);
  const current = publications?.find(item => item.id === selected);
  const active = current && current.revokedAt === null;
  useEffect(() => {
    const controller = new AbortController();
    client.list(controller.signal).then(items => { if (!controller.signal.aborted) setPublications(items); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [client, operation.fail]);
  function publish() {
    void operation.run(signal => client.publish(signal), publication => {
      setPublications(items => [publication, ...(items ?? [])]); setSelected(publication.id);
    }, `Versión ${revision} publicada. Todavía no tiene destinatarios nuevos.`);
  }
  function invite() {
    if (!active) return;
    void operation.run(signal => service.invite(current.id, email, signal), () => { setEmail(''); }, 'Solicitud procesada: si la cuenta está confirmada, verá la invitación en QuickPrompt.');
  }
  function revoke() {
    if (!active) return;
    void operation.run(async signal => { await service.revokePublication(current.id, signal); return client.list(signal); }, items => {
      setPublications(items); setConfirmRevoke(false);
    }, 'Acceso revocado. Las copias ya importadas permanecen en sus bibliotecas.');
  }
  return <Dialog open onOpenChange={open => { if (!open && !operation.busy) onClose(); }} title={`Compartir ${title}`} description="Publica una versión fija e invita a una persona. Sus permisos permiten leer e importar; no editar tu original.">
    <div className="account-share-content">
      <div className="account-publication-source"><span className="eyebrow">VERSIÓN PRIVADA ACTUAL · {revision}</span>{preview}<button className="secondary" disabled={operation.busy} onClick={publish}>Publicar versión {revision}</button></div>
      {!publications ? !operation.error && <p role="status">Cargando publicaciones…</p> : publications.length ? <>
        <div><label htmlFor="account-publication">Versión publicada que vas a compartir</label><select id="account-publication" value={selected} disabled={operation.busy} onChange={event => { setSelected(event.target.value); setConfirmRevoke(false); }}><option value="">Seleccionar publicación</option>{publications.map(item => <option key={item.id} value={item.id}>Versión {item.sourceRevision} · {new Date(item.createdAt).toLocaleString()} {item.revokedAt ? '· Revocada' : ''}</option>)}</select></div>
        {current && <p className="muted">Compartes una copia fija de la versión {current.sourceRevision}. Editar el origen privado no actualiza esa publicación.</p>}
        {active && <form className="account-invite-form" onSubmit={event => { event.preventDefault(); invite(); }}><label htmlFor="account-invite-email">Correo de la persona invitada</label><input id="account-invite-email" type="email" required maxLength={256} autoComplete="email" disabled={operation.busy} value={email} onChange={event => setEmail(event.target.value)} /><button className="primary" disabled={operation.busy || !email.trim()}><AppIcon glyph={Mail} />Enviar invitación</button></form>}
        {current?.revokedAt && <p role="status">Esta publicación está revocada y no admite nuevas invitaciones.</p>}
        {active && !confirmRevoke && <button className="text-button" disabled={operation.busy} onClick={() => setConfirmRevoke(true)}><AppIcon glyph={ShieldOff} />Revocar esta publicación</button>}
        {active && confirmRevoke && <div className="account-revoke-confirm"><p>Se bloquearán nuevas lecturas e importaciones de esta publicación para todas las personas invitadas. Las copias ya importadas se conservan.</p><div className="account-actions"><button className="secondary" disabled={operation.busy} onClick={() => setConfirmRevoke(false)}>Conservar acceso</button><button className="destructive" disabled={operation.busy} onClick={revoke}>Confirmar revocación</button></div></div>}
      </> : <p className="muted">No hay versiones publicadas de este recurso.</p>}
      {operation.busy && <p role="status"><InlineSpinner /> Procesando…</p>}{operation.error && <p role="alert" className="error">{operation.error}</p>}{operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    </div>
  </Dialog>;
}

/** One inbox/outbox view; a pending invite never carries body content. */
export function AccountInvitations({ libraries, service, onExpired, onImported }: { libraries: AccountLibrary[]; service: AccountService; onExpired: () => void; onImported: (libraryId: string) => void }) {
  const [invitations, setInvitations] = useState<AccountInvitation[]>();
  const [selected, setSelected] = useState<AccountInvitation>();
  const [revoking, setRevoking] = useState<AccountInvitation>();
  const [refresh, setRefresh] = useState(0);
  const scope = useMemo(() => ({ service }), [service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController();
    operation.clear(); setInvitations(undefined);
    service.invitations(controller.signal).then(items => { if (!controller.signal.aborted) setInvitations(items); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [service, operation.fail, operation.clear, refresh]);
  function decide(invitation: AccountInvitation, state: 1 | 2 | 3) {
    void operation.run(async signal => { await service.decideInvitation(invitation.id, state, signal); return service.invitations(signal); }, items => {
      setInvitations(items); setRevoking(undefined);
    }, state === 1 ? 'Invitación aceptada. Ya puedes ver la versión compartida.' : state === 2 ? 'Invitación rechazada.' : 'Permiso revocado.');
  }
  return <section className="account-invitations" aria-label="Invitaciones a prompts">
    <div className="account-section-heading"><div><span className="eyebrow">COMPARTIR CON PERMISO</span><h2>Invitaciones</h2></div><button className="secondary" disabled={operation.busy} onClick={() => setRefresh(value => value + 1)}>Actualizar invitaciones</button></div>
    <p className="muted">Aceptar permite leer una versión fija e importar una copia propia. Nunca concede editar la biblioteca de quien comparte.</p>
    {operation.error && <p role="alert" className="error">{operation.error}</p>}{operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    {!invitations ? !operation.error && <p role="status">Cargando invitaciones…</p> : invitations.length ? <ul className="account-prompt-list">{invitations.map(item => <li key={item.id}>
      <div className="account-section-heading"><div><h3>{item.title}</h3><p className="muted">{item.isCollection ? 'Colección de prompts' : 'Prompt individual'} · {item.sentByMe ? `Enviada a ${item.recipientEmail ?? 'la cuenta invitada'}` : 'Recibida'} · {item.state === 0 ? 'Pendiente' : item.state === 1 ? item.available ? 'Aceptada' : 'Acceso no disponible' : item.state === 2 ? 'Rechazada' : 'Revocada'}</p></div></div>
      <div className="account-actions">
        {!item.sentByMe && item.state === 0 && <><button className="primary" disabled={operation.busy} onClick={() => decide(item, 1)}>Aceptar invitación a {item.title}</button><button className="secondary" disabled={operation.busy} onClick={() => decide(item, 2)}>Rechazar invitación a {item.title}</button></>}
        {!item.sentByMe && item.state === 1 && item.available && <button className="primary" onClick={() => setSelected(item)}>Ver versión compartida de {item.title}</button>}
        {item.sentByMe && (item.state === 0 || item.state === 1) && <button className="secondary" disabled={operation.busy} onClick={() => setRevoking(item)}>Revocar invitación a {item.recipientEmail}</button>}
      </div>
    </li>)}</ul> : <div className="empty-state"><h3>No tienes invitaciones.</h3><p>Las instrucciones compartidas contigo aparecerán aquí.</p></div>}
    {selected && <SharedPromptDialog key={selected.publicationId} invitation={selected} libraries={libraries} service={service} onExpired={onExpired} onClose={() => setSelected(undefined)} onImported={onImported} />}
    {revoking && <Dialog open onOpenChange={open => { if (!open && !operation.busy) setRevoking(undefined); }} title="Revocar invitación" description={`Se bloqueará el acceso futuro de ${revoking.recipientEmail ?? 'esta persona'} a ${revoking.title}. Las copias ya importadas se conservan.`}>
      <div className="dialog-actions"><button className="secondary" disabled={operation.busy} onClick={() => setRevoking(undefined)}>Conservar acceso</button><button className="destructive" disabled={operation.busy} onClick={() => decide(revoking, 3)}>Confirmar revocación</button></div>{operation.error && <p className="error" role="alert">{operation.error}</p>}
    </Dialog>}
  </section>;
}

function SharedPromptDialog({ invitation, libraries, service, onExpired, onClose, onImported }: { invitation: AccountInvitation; libraries: AccountLibrary[]; service: AccountService; onExpired: () => void; onClose: () => void; onImported: (libraryId: string) => void }) {
  const [shared, setShared] = useState<SharedPrompt | SharedCollection>();
  const [destination, setDestination] = useState('');
  const [imported, setImported] = useState(false);
  const receipt = useRef<{ destination: string; id: string } | undefined>(undefined);
  const scope = useMemo(() => ({ id: invitation.publicationId, service }), [invitation.publicationId, service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController();
    const pending = invitation.isCollection ? service.readCollectionPublication(invitation.publicationId, controller.signal) : service.readPublication(invitation.publicationId, controller.signal);
    pending.then(result => { if (!controller.signal.aborted) setShared(result); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [invitation.publicationId, invitation.isCollection, service, operation.fail]);
  function importCopy() {
    if (!shared || !destination) return;
    if (!receipt.current || receipt.current.destination !== destination) receipt.current = { destination, id: crypto.randomUUID() };
    const current = receipt.current;
    void operation.run(async signal => {
      const result = invitation.isCollection ? await service.importCollectionPublication(invitation.publicationId, destination, current.id, signal) : await service.importPublication(invitation.publicationId, destination, current.id, signal);
      if (result.libraryId !== destination) throw new Error('La copia no corresponde a la biblioteca elegida.');
    }, () => setImported(true), 'Copia importada en tu biblioteca.', error => {
      if (error instanceof ApiError && (error.status === 403 || error.status === 404)) setShared(undefined);
    });
  }
  return <Dialog open onOpenChange={open => { if (!open && !operation.busy) onClose(); }} title={invitation.title} description="Estás viendo una versión publicada. Tu copia importada será independiente del original.">
    <div className="account-share-content">
      {shared ? <><p className="muted">Versión publicada {shared.sourceRevision}{'items' in shared && ` · ${shared.items.length} prompts`}</p>
        {'items' in shared ? shared.items.map((item, index) => <section key={index}><h3>{item.title} · versión {item.sourceRevision}</h3><pre className="account-shared-body">{item.template}</pre></section>) : <pre className="account-shared-body">{shared.template}</pre>}
        {libraries.length ? <div><label htmlFor="account-import-library">Importar en mi biblioteca</label><select id="account-import-library" value={destination} disabled={operation.busy || imported} onChange={event => setDestination(event.target.value)}><option value="">Elegir biblioteca propia</option>{libraries.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div> : <p className="muted">Crea primero una biblioteca propia para importar una copia.</p>}
        <div className="account-actions">{imported ? <button className="primary" onClick={() => onImported(destination)}>Abrir mi copia</button> : <button className="primary" disabled={operation.busy || !destination} onClick={importCopy}><AppIcon glyph={Download} />Importar copia a mi biblioteca</button>}</div>
      </> : !operation.error && <p role="status">Comprobando permiso de lectura…</p>}
      {operation.busy && <p role="status"><InlineSpinner /> Importando…</p>}{operation.error && <p role="alert" className="error">{operation.error} Si el acceso fue revocado, no podrás realizar nuevas lecturas ni importaciones.</p>}{operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    </div>
  </Dialog>;
}
