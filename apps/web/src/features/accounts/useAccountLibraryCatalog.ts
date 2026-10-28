import { useEffect, useMemo, useState } from 'react';
import { ApiError, errorMessage, type Group } from '../../services/api';
import type { AccountCatalogQuery, AccountPrompt, AccountService } from '../../services/accounts';

const initialQuery: AccountCatalogQuery = { search: '', filter: 'all', page: 0, size: 12, sort: 'updated' };
type Catalog = Awaited<ReturnType<AccountService['catalog']>>;

/** Page selection is presentation state; the server owns matching, totals and permissions. */
export function useAccountLibraryCatalog(libraryId: string, service: AccountService, refresh: number, onExpired: () => void) {
  const scope = useMemo(() => ({ libraryId, service }), [libraryId, service]);
  const [selection, setSelection] = useState({ scope, query: initialQuery });
  const query = selection.scope === scope ? selection.query : initialQuery;
  const key = JSON.stringify([query, refresh]);
  const [snapshot, setSnapshot] = useState<{ scope: object; key: string; result?: Catalog; error?: string }>();
  const current = snapshot?.scope === scope && snapshot.key === key ? snapshot : undefined;
  useEffect(() => {
    const controller = new AbortController();
    void service.catalog(libraryId, query, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      if (query.page > 0 && result.items.length === 0) {
        setSelection({ scope, query: { ...query, page: Math.max(0, Math.ceil(result.total / query.size) - 1) } });
      } else setSnapshot({ scope, key, result });
    }).catch(failure => {
      if (controller.signal.aborted) return;
      if (failure instanceof ApiError && failure.status === 401) onExpired();
      else setSnapshot({ scope, key, error: errorMessage(failure) });
    });
    return () => controller.abort();
  }, [scope, libraryId, service, query, key, onExpired]);
  function select(patch: Partial<AccountCatalogQuery>) {
    setSelection(previous => ({ scope, query: { ...(previous.scope === scope ? previous.query : initialQuery), page: 0, ...patch } }));
  }
  return { query, select, result: current?.result, error: current?.error ?? '', loading: !current };
}

/** Agent source choices use the complete legacy port, never the current UI page. */
export function useAccountNovaCandidates(libraryId: string, service: AccountService, refresh: number, enabled: boolean, onExpired: () => void) {
  const scope = useMemo(() => ({ libraryId, service, enabled }), [libraryId, service, enabled]);
  const [snapshot, setSnapshot] = useState<{ scope: object; refresh: number; items?: AccountPrompt[]; error?: string }>();
  const current = snapshot?.scope === scope && snapshot.refresh === refresh ? snapshot : undefined;
  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    void service.prompts(libraryId, controller.signal).then(items => {
      if (controller.signal.aborted) return;
      if (items.some(item => item.libraryId !== libraryId) || new Set(items.map(item => item.id)).size !== items.length)
        throw new Error('Los candidatos no corresponden a una biblioteca e identidad únicas.');
      setSnapshot({ scope, refresh, items: items.filter(item => !item.isArchived) });
    }).catch(failure => {
      if (controller.signal.aborted) return;
      if (failure instanceof ApiError && failure.status === 401) onExpired();
      else setSnapshot({ scope, refresh, error: errorMessage(failure) });
    });
    return () => controller.abort();
  }, [scope, libraryId, service, refresh, enabled, onExpired]);
  return { items: current?.items, error: current?.error };
}

export function useAccountCollections(libraryId: string, service: AccountService, refresh: number, onExpired: () => void) {
  const scope = useMemo(() => ({ libraryId, service }), [libraryId, service]);
  const [snapshot, setSnapshot] = useState<{ scope: object; items: Group[]; error?: string }>();
  useEffect(() => {
    const controller = new AbortController();
    void service.collections(libraryId).list(controller.signal).then(items => {
      if (!controller.signal.aborted) setSnapshot({ scope, items });
    }).catch(failure => {
      if (controller.signal.aborted) return;
      if (failure instanceof ApiError && failure.status === 401) onExpired();
      else setSnapshot({ scope, items: [], error: errorMessage(failure) });
    });
    return () => controller.abort();
  }, [scope, libraryId, service, refresh, onExpired]);
  return snapshot?.scope === scope ? snapshot : { items: [], error: undefined };
}
