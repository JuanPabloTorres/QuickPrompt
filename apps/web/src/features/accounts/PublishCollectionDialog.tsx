import { useEffect, useMemo, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { Group } from '../../services/api';
import type { AccountPrompt, AccountService } from '../../services/accounts';
import { PublicationManager } from './AccountSharing';
import { useAccountOperation } from './useAccountOperation';

/** Review current member revisions; publication never infers or expands the selected collection. */
export function PublishCollectionDialog({ libraryId, collection, service, onExpired, onClose }: { libraryId: string; collection: Group['group']; service: AccountService; onExpired: () => void; onClose: () => void }) {
  const [prompts, setPrompts] = useState<AccountPrompt[]>();
  const scope = useMemo(() => ({ libraryId, collection, service }), [libraryId, collection, service]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController();
    service.prompts(libraryId, controller.signal, { collectionId: collection.id }).then(items => { if (!controller.signal.aborted) setPrompts(items); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [libraryId, collection.id, service, operation.fail]);
  const client = useMemo(() => ({
    list: (signal?: AbortSignal) => service.collectionPublications(libraryId, collection.id, signal),
    publish: async (signal?: AbortSignal) => {
      if (!prompts?.length) throw new Error('Añade prompts antes de publicar esta colección.');
      const revisions = Object.fromEntries(prompts.map(prompt => [prompt.id, prompt.revision]));
      return { ...await service.publishCollection(libraryId, collection.id, collection.revision, revisions, signal), revokedAt: null };
    },
  }), [libraryId, collection, prompts, service]);
  if (!prompts?.length) return <Dialog open onOpenChange={open => { if (!open) onClose(); }} title={`Compartir ${collection.name}`} description="Revisa los prompts que formarán esta versión fija de la colección.">
    {operation.error ? <p role="alert" className="error">{operation.error} Cierra y vuelve a abrir para revisar la versión actual.</p> : prompts ? <p>Añade al menos un prompt para compartir esta colección.</p> : <p role="status">Cargando los prompts de la colección…</p>}
    <button className="secondary" onClick={onClose}>Cerrar</button>
  </Dialog>;
  return <PublicationManager title={collection.name} revision={collection.revision} client={client} service={service} onExpired={onExpired} onClose={onClose}
    preview={<><p>{prompts.length} prompts. Se compartirán sus textos y variables, sin valores, memoria ni resultados privados.</p>{prompts.map(prompt => <section key={prompt.id}><h3>{prompt.title} · versión {prompt.revision}</h3><pre>{prompt.template}</pre></section>)}</>} />;
}
