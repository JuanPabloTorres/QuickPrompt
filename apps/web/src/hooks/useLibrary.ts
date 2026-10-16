import { useEffect, useState } from 'react';
import { errorMessage, pageSchema, request, type Prompt } from '../services/api';

export type Filter = 'all' | 'favorites' | 'recent' | 'archived';
export function useLibrary(search: string, filter: Filter, groupId: string, page: number, version: number) {
  const [items, setItems] = useState<Prompt[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    const timer = setTimeout(() => {
      const query = new URLSearchParams({ search, page: String(page), size: '12', sort: filter === 'recent' ? 'recent' : 'updated' });
      if (filter !== 'all') query.set(filter, 'true');
      if (groupId) query.set('groupId', groupId);
      request(`/prompts?${query}`, pageSchema, { signal: controller.signal })
        .then(result => { if (!controller.signal.aborted) { setItems(result.items); setTotal(result.total); } })
        .catch(error => { if (!controller.signal.aborted) { setError(errorMessage(error)); setItems([]); } })
        .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    }, search ? 180 : 0);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [search, filter, groupId, page, version]);
  return { items, total, loading, error };
}
