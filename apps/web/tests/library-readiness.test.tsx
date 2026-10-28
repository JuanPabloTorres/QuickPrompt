import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useLibrary } from '../src/hooks/useLibrary';
import * as api from '../src/services/api';
import type { PromptLibraryClient } from '../src/services/library';

afterEach(() => vi.restoreAllMocks());
it('never reports old results as ready while a new query is pending', async () => {
  let resolveSearch: (value: unknown) => void = () => {};
  vi.spyOn(api, 'request').mockImplementation(async (path: string) => {
    if (path.includes('search=nueva')) return await new Promise<unknown>(resolve => { resolveSearch = resolve; }) as never;
    return { items: [], total: 0 } as never;
  });
  const { result, rerender } = renderHook(({ search }) => useLibrary(search, 'all', '', 0, 0), { initialProps: { search: '' } });
  await waitFor(() => expect(result.current.loading).toBe(false));
  rerender({ search: 'nueva' }); expect(result.current.loading).toBe(true);
  await new Promise(resolve => setTimeout(resolve, 210)); expect(result.current.loading).toBe(true);
  await act(async () => resolveSearch({ items: [], total: 0 }));
  await waitFor(() => expect(result.current.loading).toBe(false));
});

it('does not treat one implementation results as ready after substituting its catalog port', async () => {
  let finish: (value: { items: never[]; total: number }) => void = () => {};
  const original = { list: vi.fn(async (_query: unknown, _signal?: AbortSignal) => ({ items: [], total: 1 })) };
  const replacement = { list: vi.fn(() => new Promise<{ items: never[]; total: number }>(resolve => { finish = resolve; })) };
  const observations: { library: Pick<PromptLibraryClient, 'list'>; loading: boolean }[] = [];
  const { result, rerender } = renderHook(({ library }: { library: Pick<PromptLibraryClient, 'list'> }) => {
    const result = useLibrary('', 'all', '', 0, 0, library);
    observations.push({ library, loading: result.loading });
    return result;
  }, { initialProps: { library: original } });
  await waitFor(() => expect(result.current.loading).toBe(false));
  rerender({ library: replacement });
  expect(result.current.loading).toBe(true);
  await waitFor(() => expect(replacement.list).toHaveBeenCalled());
  expect(observations.filter(observation => observation.library === replacement).every(observation => observation.loading)).toBe(true);
  expect(original.list.mock.calls[0][1]?.aborted).toBe(true);
  await act(async () => finish({ items: [], total: 2 }));
  await waitFor(() => expect(result.current.loading).toBe(false));
  expect(result.current.total).toBe(2);
});
