import { useApplicationServices } from '../components/ServiceContext';
import { useEffect, useState } from 'react';
import { errorMessage, type Prompt } from '../services/api';
import { type LibraryFilter, type PromptLibraryClient } from '../services/library';

export type Filter = LibraryFilter;
export function useLibrary(search: string, filter: Filter, groupId: string, page: number, version: number, libraryOverride?: Pick<PromptLibraryClient, 'list'>) {
  const { prompts } = useApplicationServices();
  const library = libraryOverride ?? prompts;
  const [items, setItems] = useState<Prompt[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const key = JSON.stringify([search, filter, groupId, page, version]);
  const [resolvedKey, setResolvedKey] = useState('');
  const [resolvedLibrary, setResolvedLibrary] = useState<Pick<PromptLibraryClient, 'list'> | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    const timer = setTimeout(() => {
      library.list({ search, filter, groupId, page }, controller.signal)
        .then(result => { if (!controller.signal.aborted) { setItems(result.items); setTotal(result.total); setResolvedKey(key); setResolvedLibrary(library); } })
        .catch(error => { if (!controller.signal.aborted) { setError(errorMessage(error)); setItems([]); setResolvedKey(key); setResolvedLibrary(library); } })
        .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    }, search ? 180 : 0);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [search, filter, groupId, page, version, key, library]);
  return { items, total, loading: loading || resolvedKey !== key || resolvedLibrary !== library, error };
}
