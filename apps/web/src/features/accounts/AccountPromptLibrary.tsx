import { useMemo, useState } from 'react';
import type { AccountLibrary, AccountPrompt, AccountPromptDraft, AccountPromptAction, AccountService, SavedEditorContext } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';
import { PublishPromptDialog } from './AccountSharing';
import { AccountRememberedDrafts } from './AccountRememberedDrafts';
import { AccountExecutions, UseAccountPromptDialog } from './AccountExecutions';
import { AccountFactualDocuments } from './AccountFactualDocuments';
import { AccountCollectionDialog } from './AccountCollectionDialog';
import { PublishCollectionDialog } from './PublishCollectionDialog';
import type { Group } from '../../services/api';
import { LibraryToolbar } from '../prompts/LibraryToolbar';
import { AccountPromptCatalog } from './AccountPromptCatalog';
import { AccountPromptDeletion } from './AccountPromptDeletion';
import { useAccountCollections, useAccountLibraryCatalog, useAccountNovaCandidates } from './useAccountLibraryCatalog';
import { AccountPromptEditor } from './AccountPromptEditor';
import { AccountNovaBoundary, type AccountAgentBinding } from './AccountNovaBoundary';
import { AccountNovaActions } from './AccountNovaActions';
import { AccountNovaSourceControl } from './AccountNovaSourceControl';
import { AgentDock } from '../nova/AgentDock';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import { AccountPromptFocus } from './AccountPromptFocus';
import { commitAccountProposal } from '../../services/accountNovaCommit';

type LibraryProps = { library: AccountLibrary; service: AccountService; onExpired: () => void; agentAccountId?: string };

/** Private account storage has its own revision contract; parsing and permissions remain server-owned. */
export function AccountPromptLibrary(props: LibraryProps) {
  return props.agentAccountId ? <AccountNovaBoundary accountId={props.agentAccountId} libraryId={props.library.id} service={props.service} onExpired={props.onExpired}>
    {agent => <AccountLibraryContent key={props.library.id} {...props} agent={agent} />}
  </AccountNovaBoundary> : <AccountLibraryContent key={props.library.id} {...props} />;
}

function AccountLibraryContent({ library, service, onExpired, agent }: LibraryProps & { agent?: AccountAgentBinding }) {
  const [focused, setFocused] = useState<AccountPrompt>();
  const [editor, setEditor] = useState<{ source: AccountPrompt | null; restored?: SavedEditorContext }>();
  const [sharing, setSharing] = useState<AccountPrompt>();
  const [using, setUsing] = useState<{ prompt: AccountPrompt; values?: Record<string, string> }>();
  const [requestedExecution, setRequestedExecution] = useState<string>();
  const [organizing, setOrganizing] = useState<{ prompt?: AccountPrompt }>();
  const [sharingCollection, setSharingCollection] = useState<Group['group']>();
  const [view, setView] = useState<'cards' | 'rows'>('cards');
  const [deleting, setDeleting] = useState<AccountPrompt>();
  const [novaSource, setNovaSource] = useState<AccountPrompt>();
  const portalScope = useUiPortalScope();
  const [executionRefresh, setExecutionRefresh] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const [memoryRefresh, setMemoryRefresh] = useState(0);
  const scope = useMemo(() => ({ libraryId: library.id, service, agentServices: agent?.services, runId: agent?.runId }), [library.id, service, agent?.services, agent?.runId]);
  const operation = useAccountOperation(scope, onExpired);
  const catalog = useAccountLibraryCatalog(library.id, service, refresh, onExpired);
  const groups = useAccountCollections(library.id, service, refresh, onExpired);
  const candidates = useAccountNovaCandidates(library.id, service, refresh, !!agent?.services, onExpired);
  const prompts = catalog.result?.items;
  const collectionId = catalog.query.ungrouped ? 'ungrouped' : catalog.query.collectionId ?? '';
  const activeCollection = groups.items.find(item => item.group.id === collectionId)?.group;
  const unavailable = operation.busy || !!agent?.busy;
  function reload() { setRefresh(value => value + 1); }
  function open(prompt: AccountPrompt) {
    void operation.run(signal => service.prompt(library.id, prompt.id, signal), fresh => {
      if (fresh.libraryId !== library.id || fresh.id !== prompt.id) throw new Error('El prompt no corresponde a esta biblioteca.');
      setFocused(fresh);
    });
  }
  function act(prompt: AccountPrompt, action: AccountPromptAction) {
    if (unavailable) return;
    void operation.run(signal => service.actPrompt(prompt, action, signal), updated => {
      if (focused?.id === prompt.id) setFocused(updated);
      reload();
    }, action === 'duplicate' ? 'Copia creada en tu biblioteca.' : 'Prompt actualizado en tu biblioteca.');
  }
  function save(draft: AccountPromptDraft, asNew = false) {
    if (!editor) return;
    const proposal = agent?.runtime?.view.proposal;
    const approved = !asNew && !!agent?.controlled && agent.runtime?.view.run.approval === 'APPROVED'
      && proposal?.promptId === (editor.source?.id ?? null) && proposal?.revision === (editor.source?.revision ?? null);
    if (agent?.controlled && !approved) return;
    void operation.run(async signal => {
      if (!approved || !agent?.services || !agent.runId || !proposal) return service.savePrompt(library.id, asNew ? null : editor.source, draft, signal);
      return commitAccountProposal(agent.services, service, library.id, agent.runId, { ...proposal.draft, title: draft.title, templateText: draft.template }, signal);
    }, prompt => {
      reload();
      if (approved || focused) setFocused(prompt);
      setEditor(undefined);
    }, 'Prompt guardado en tu biblioteca.');
  }
  const proposal = agent?.runtime?.view.proposal;
  const approvedRunId = agent?.controlled && agent.runtime?.view.run.approval === 'APPROVED' && proposal?.promptId === (editor?.source?.id ?? null)
    && proposal?.revision === (editor?.source?.revision ?? null) ? agent.runId ?? undefined : undefined;
  return <section aria-label={`Prompts de ${library.name}`} className="account-prompt-library" data-nova-surface="library">
    {agent && <>
      <AgentDock context={focused?.title ?? library.name} hasActivity={!!agent.runId} suspended={agent.controlled || portalScope?.active === false} actions={
        <>{agent.configuration}<AccountNovaActions agent={agent} prompts={candidates.items ?? []} source={focused} onSourceChange={setNovaSource} unavailable={operation.busy || !!editor || !!using || !!sharing || !!organizing || !!sharingCollection || !!deleting || !candidates.items} /></>}>
        {candidates.error && <p role="alert">No se pudieron consultar los orígenes de NOVA: {candidates.error}<button className="text-button" onClick={reload}>Reintentar orígenes</button></p>}
      </AgentDock>
      <AccountNovaSourceControl source={novaSource} visibleIds={prompts?.map(prompt => prompt.id) ?? []} focused={!!focused} editing={!!editor} onOpen={open} />
    </>}
    {focused && !editor && <AccountPromptFocus key={focused.id} prompt={focused} service={service} onExpired={onExpired} busy={operation.busy}
      onUse={values => setUsing({ prompt: focused, values })} onAction={action => act(focused, action)} onCollections={() => setOrganizing({ prompt: focused })} onShare={() => setSharing(focused)} onDelete={() => setDeleting(focused)}
      onEdit={() => { operation.clear(); setEditor({ source: focused }); }} onClose={() => setFocused(undefined)} onSaved={saved => { setFocused(saved); setRefresh(value => value + 1); }} />}
    {focused && !editor && operation.error && <p className="error" role="alert">{operation.error} Tus datos permanecen en el detalle. Vuelve a la biblioteca para revisar la versión actual antes de reintentar.</p>}
    {focused && !editor && operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    <div hidden={!!focused || !!editor}>
    <button type="button" className="text-button" data-nova-control="library.home" onClick={() => { setFocused(undefined); catalog.select({ search: '', filter: 'all', collectionId: undefined, ungrouped: false }); }}>Ver todos los prompts</button>
    <LibraryToolbar title={library.name} createLabel="Crear prompt" collectionLabel="Filtrar por colección" includeUngrouped total={catalog.result?.total ?? 0}
      search={catalog.query.search} filter={catalog.query.filter} groupId={collectionId} groups={groups.items} view={view} busy={operation.busy}
      onSearch={search => catalog.select({ search })} onFilter={filter => catalog.select({ filter, ...(filter === 'recent' ? { sort: 'recent' } : {}) })}
      onGroup={id => catalog.select({ collectionId: id && id !== 'ungrouped' ? id : undefined, ungrouped: id === 'ungrouped' })} onView={setView}
      onCreate={() => { operation.clear(); setEditor({ source: null }); }} onGroups={() => setOrganizing({})} />
    <div className="account-catalog-options"><label htmlFor="account-catalog-sort">Ordenar prompts</label><select id="account-catalog-sort" value={catalog.query.sort} onChange={event => catalog.select({ sort: event.target.value as typeof catalog.query.sort })}><option value="updated">Última actualización</option><option value="title">Título</option><option value="recent">Último uso</option></select>
      {activeCollection && <button className="secondary" onClick={() => setSharingCollection(activeCollection)}>Compartir colección</button>}</div>
    {groups.error && <p role="alert">No se pudieron consultar las colecciones: {groups.error}<button className="text-button" onClick={reload}>Recargar colecciones</button></p>}
    {!editor && (operation.error || catalog.error) && <p className="error" role="alert">{operation.error || catalog.error}<button className="text-button" onClick={reload}>Recargar prompts</button></p>}
    {operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    {catalog.loading ? <p role="status">Cargando prompts…</p> : prompts?.length ? <AccountPromptCatalog items={prompts} view={view} busy={unavailable}
      onOpen={open} onUse={prompt => setUsing({ prompt })} onEdit={prompt => { operation.clear(); setEditor({ source: prompt }); }} onCollections={prompt => setOrganizing({ prompt })} onShare={setSharing} onAction={act} onDelete={setDeleting} />
      : !catalog.error && <div className="empty-state"><h3>{catalog.query.search || catalog.query.filter !== 'all' || collectionId ? 'No hay prompts en esta selección.' : 'Guarda tu primera instrucción.'}</h3><p>Puedes cambiar los filtros o crear una instrucción con variables.</p></div>}
    {catalog.result && catalog.result.total > catalog.query.size && <nav className="pagination" aria-label="Páginas de prompts"><button className="secondary" disabled={catalog.query.page === 0 || unavailable} onClick={() => catalog.select({ page: catalog.query.page - 1 })}>Página anterior</button><span>Página {catalog.query.page + 1} de {Math.ceil(catalog.result.total / catalog.query.size)}</span><button className="secondary" disabled={(catalog.query.page + 1) * catalog.query.size >= catalog.result.total || unavailable} onClick={() => catalog.select({ page: catalog.query.page + 1 })}>Página siguiente</button></nav>}
    <AccountRememberedDrafts key={`${library.id}:${memoryRefresh}`} libraryId={library.id} service={service} onExpired={onExpired} onRestore={(restored, source) => { operation.clear(); setEditor({ source, restored }); }} />
    {prompts && !operation.busy && <span className="sr-only" data-testid="library-ready">Biblioteca comprobada</span>}
    </div>
    <AccountExecutions key={library.id} libraryId={library.id} service={service} refresh={executionRefresh} onExpired={onExpired} onCatalogChanged={reload}
      requestedId={editor || using || sharing || organizing || sharingCollection || deleting || agent?.controlled ? undefined : requestedExecution} onRequestedShown={() => setRequestedExecution(undefined)} />
    <AccountFactualDocuments key={`documents:${library.id}`} libraryId={library.id} service={service} onExpired={onExpired} />
    {deleting && <AccountPromptDeletion key={`${library.id}:${deleting.id}:${deleting.revision}`} prompt={deleting} service={service} onExpired={onExpired} onClose={() => setDeleting(undefined)} onReload={() => { setDeleting(undefined); reload(); }} onDeleted={() => { if (focused?.id === deleting.id) setFocused(undefined); setDeleting(undefined); reload(); }} />}
    {using && <UseAccountPromptDialog key={`${using.prompt.id}:${using.prompt.revision}`} prompt={using.prompt} initialValues={using.values} service={service} onExpired={onExpired} onClose={() => setUsing(undefined)} onSubmitted={job => { setUsing(undefined); setRequestedExecution(job.id); setExecutionRefresh(value => value + 1); }} />}
    {editor && <AccountPromptEditor key={`${editor.restored?.id ?? editor.source?.id ?? 'new'}:${editor.source?.revision ?? 0}`} source={editor.source} restored={editor.restored} libraryId={library.id} service={service} onExpired={onExpired} onRemembered={() => setMemoryRefresh(value => value + 1)} saving={operation.busy} error={operation.error} onSave={save} controlled={agent?.controlled} approvedRunId={approvedRunId} onClose={() => { if (!operation.busy && !agent?.controlled) setEditor(undefined); }} />}
    {sharing && <PublishPromptDialog key={`${sharing.id}:${sharing.revision}`} prompt={sharing} service={service} onExpired={onExpired} onClose={() => setSharing(undefined)} />}
    {organizing && <AccountCollectionDialog key={`${library.id}:${organizing.prompt?.id ?? 'library'}`} libraryId={library.id} prompt={organizing.prompt} service={service} onExpired={onExpired} onUpdated={() => setRefresh(value => value + 1)} onClose={() => setOrganizing(undefined)} />}
    {sharingCollection && <PublishCollectionDialog libraryId={library.id} collection={sharingCollection} service={service} onExpired={onExpired} onClose={() => setSharingCollection(undefined)} />}
  </section>;
}

