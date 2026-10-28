import { useEffect, useMemo, useState } from 'react';
import { FileText, Pencil, Plus, Share2 } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import type { AccountLibrary, AccountPrompt, AccountPromptDraft, AccountService, SavedEditorContext } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';
import { PublishPromptDialog } from './AccountSharing';
import { AccountRememberedDrafts } from './AccountRememberedDrafts';
import { AccountExecutions, UseAccountPromptDialog } from './AccountExecutions';
import { AccountCollectionDialog } from './AccountCollectionDialog';
import { PublishCollectionDialog } from './PublishCollectionDialog';
import type { Group } from '../../services/api';
import { AccountPromptEditor } from './AccountPromptEditor';
import { AccountNovaBoundary, type AccountAgentBinding } from './AccountNovaBoundary';
import { AccountNovaActions } from './AccountNovaActions';
import { AccountPromptFocus } from './AccountPromptFocus';
import { commitAccountProposal } from '../../services/accountNovaCommit';

type LibraryProps = { library: AccountLibrary; service: AccountService; onExpired: () => void; agentAccountId?: string };

/** Private account storage has its own revision contract; parsing and permissions remain server-owned. */
export function AccountPromptLibrary(props: LibraryProps) {
  return props.agentAccountId ? <AccountNovaBoundary accountId={props.agentAccountId} libraryId={props.library.id} service={props.service} onExpired={props.onExpired}>
    {agent => <AccountLibraryContent {...props} agent={agent} />}
  </AccountNovaBoundary> : <AccountLibraryContent {...props} />;
}

function AccountLibraryContent({ library, service, onExpired, agent }: LibraryProps & { agent?: AccountAgentBinding }) {
  const [prompts, setPrompts] = useState<AccountPrompt[]>();
  const [focused, setFocused] = useState<AccountPrompt>();
  const [editor, setEditor] = useState<{ source: AccountPrompt | null; restored?: SavedEditorContext }>();
  const [sharing, setSharing] = useState<AccountPrompt>();
  const [using, setUsing] = useState<AccountPrompt>();
  const [collections, setCollections] = useState<Group[]>([]);
  const [organizing, setOrganizing] = useState<{ prompt?: AccountPrompt }>();
  const [sharingCollection, setSharingCollection] = useState<Group['group']>();
  const [filter, setFilter] = useState('');
  const [executionRefresh, setExecutionRefresh] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const [memoryRefresh, setMemoryRefresh] = useState(0);
  const scope = useMemo(() => ({ libraryId: library.id, service, agentServices: agent?.services, runId: agent?.runId }), [library.id, service, agent?.services, agent?.runId]);
  const operation = useAccountOperation(scope, onExpired);
  useEffect(() => {
    const controller = new AbortController();
    operation.clear(); setPrompts(undefined);
    service.prompts(library.id, controller.signal, filter ? filter === 'ungrouped' ? { ungrouped: true } : { collectionId: filter } : undefined).then(items => { if (!controller.signal.aborted) setPrompts(items); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    service.collections(library.id).list(controller.signal).then(items => { if (!controller.signal.aborted) setCollections(items); })
      .catch(error => { if (!controller.signal.aborted) operation.fail(error); });
    return () => controller.abort();
  }, [library.id, service, operation.fail, operation.clear, refresh, filter]);
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
      if (filter) setRefresh(value => value + 1);
      else setPrompts(items => [prompt, ...(items ?? []).filter(item => item.id !== prompt.id)]);
      if (approved || focused) setFocused(prompt);
      setEditor(undefined);
    }, 'Prompt guardado en tu biblioteca.');
  }
  const proposal = agent?.runtime?.view.proposal;
  const approvedRunId = agent?.controlled && agent.runtime?.view.run.approval === 'APPROVED' && proposal?.promptId === (editor?.source?.id ?? null)
    && proposal?.revision === (editor?.source?.revision ?? null) ? agent.runId ?? undefined : undefined;
  return <section aria-label={`Prompts de ${library.name}`} className="account-prompt-library" data-nova-surface="library">
    {agent && <AccountNovaActions agent={agent} prompts={prompts ?? []} source={focused} unavailable={operation.busy || !!editor || !!using || !!sharing || !!organizing || !!sharingCollection || !prompts} />}
    {focused && !editor && <AccountPromptFocus key={`${focused.id}:${focused.revision}`} prompt={focused} service={service} onExpired={onExpired}
      onEdit={() => { operation.clear(); setEditor({ source: focused }); }} onClose={() => setFocused(undefined)} onSaved={saved => { setFocused(saved); setRefresh(value => value + 1); }} />}
    <div hidden={!!focused || !!editor}>
    <div className="account-section-heading"><div><span className="eyebrow">BIBLIOTECA PRIVADA</span><h2>{library.name}</h2></div><div className="account-actions"><button type="button" className="text-button" data-nova-control="library.home" onClick={() => { setFocused(undefined); setFilter(''); }}>Ver todos los prompts</button><button className="secondary" onClick={() => setOrganizing({})}>Colecciones</button><button className="primary" data-nova-control="prompt.new" disabled={operation.busy} onClick={() => { operation.clear(); setEditor({ source: null }); }}><AppIcon glyph={Plus} />Crear prompt</button></div></div>
    <div className="account-new-library"><label htmlFor="account-collection-filter">Filtrar por colección</label><select id="account-collection-filter" value={filter} onChange={event => setFilter(event.target.value)}>
      <option value="">Todos los prompts</option><option value="ungrouped">Sin colección</option>
      {filter && filter !== 'ungrouped' && !collections.some(item => item.group.id === filter) && <option value={filter} disabled>Colección no disponible</option>}
      {collections.map(item => <option key={item.group.id} value={item.group.id}>{item.group.name} · {item.templates}</option>)}
    </select>{collections.find(item => item.group.id === filter) && <button className="secondary" onClick={() => setSharingCollection(collections.find(item => item.group.id === filter)!.group)}>Compartir colección</button>}</div>
    {!editor && operation.error && <p className="error" role="alert">{operation.error}<button className="text-button" onClick={() => setRefresh(value => value + 1)}>Recargar prompts</button></p>}
    {operation.notice && <p role="status" className="account-notice">{operation.notice}</p>}
    {!prompts ? !operation.error && <p role="status">Cargando prompts…</p> : prompts.length ? <ul className="account-prompt-list">{prompts.map(prompt => <li key={prompt.id}>
      <div className="account-prompt-heading"><AppIcon glyph={FileText} size={20} /><div><h3><button type="button" className="text-button" data-testid="library-prompt" data-prompt-id={prompt.id} data-revision={prompt.revision} aria-label={prompt.title} onClick={() => {
        void operation.run(signal => service.prompt(library.id, prompt.id, signal), setFocused);
      }}>{prompt.title}</button></h3><span>Versión {prompt.revision} · Sólo tú</span></div></div>
      <pre>{prompt.template}</pre>
      <div className="account-actions"><button className="primary" onClick={() => setUsing(prompt)}>Usar {prompt.title}</button><button className="secondary" onClick={() => { operation.clear(); setEditor({ source: prompt }); }}><AppIcon glyph={Pencil} />Editar {prompt.title}</button><button className="secondary" onClick={() => setOrganizing({ prompt })}>Colecciones de {prompt.title}</button><button className="secondary" onClick={() => setSharing(prompt)}><AppIcon glyph={Share2} />Compartir {prompt.title}</button></div>
    </li>)}</ul> : <div className="empty-state"><h3>{filter ? 'No hay prompts en esta selección.' : 'Guarda tu primera instrucción.'}</h3><p>{filter ? 'Organiza tus instrucciones desde el botón Colecciones de cada prompt.' : <>Puedes usar marcadores como {'<tema>'} para reutilizarla.</>}</p></div>}
    <AccountRememberedDrafts key={`${library.id}:${memoryRefresh}`} libraryId={library.id} service={service} onExpired={onExpired} onRestore={(restored, source) => { operation.clear(); setEditor({ source, restored }); }} />
    <AccountExecutions key={library.id} libraryId={library.id} service={service} refresh={executionRefresh} onExpired={onExpired} />
    {prompts && !operation.busy && <span className="sr-only" data-testid="library-ready">Biblioteca comprobada</span>}
    </div>
    {using && <UseAccountPromptDialog key={`${using.id}:${using.revision}`} prompt={using} service={service} onExpired={onExpired} onClose={() => setUsing(undefined)} onSubmitted={() => { setUsing(undefined); setExecutionRefresh(value => value + 1); }} />}
    {editor && <AccountPromptEditor key={`${editor.restored?.id ?? editor.source?.id ?? 'new'}:${editor.source?.revision ?? 0}`} source={editor.source} restored={editor.restored} libraryId={library.id} service={service} onExpired={onExpired} onRemembered={() => setMemoryRefresh(value => value + 1)} saving={operation.busy} error={operation.error} onSave={save} controlled={agent?.controlled} approvedRunId={approvedRunId} onClose={() => { if (!operation.busy && !agent?.controlled) setEditor(undefined); }} />}
    {sharing && <PublishPromptDialog key={`${sharing.id}:${sharing.revision}`} prompt={sharing} service={service} onExpired={onExpired} onClose={() => setSharing(undefined)} />}
    {organizing && <AccountCollectionDialog key={`${library.id}:${organizing.prompt?.id ?? 'library'}`} libraryId={library.id} prompt={organizing.prompt} service={service} onExpired={onExpired} onUpdated={() => setRefresh(value => value + 1)} onClose={() => setOrganizing(undefined)} />}
    {sharingCollection && <PublishCollectionDialog libraryId={library.id} collection={sharingCollection} service={service} onExpired={onExpired} onClose={() => setSharingCollection(undefined)} />}
  </section>;
}

