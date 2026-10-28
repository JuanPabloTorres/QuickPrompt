import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useAccountEditorInspection } from '../src/features/accounts/useAccountEditorInspection';
import { createAccountService, type AccountEditorInspection, type InspectAccountEditor } from '../src/services/accounts';
import { ApiError, createApiClient } from '../src/services/api';

const a = '10000000-0000-4000-8000-000000000001';
const b = '10000000-0000-4000-8000-000000000002';
afterEach(cleanup);

it('uses only the authenticated inspection endpoint with CSRF and literal values', async () => {
  const send = vi.fn(async (path: string, options: RequestInit) => {
    if (path === '/account/csrf') return Response.json({ token: 'synthetic-token' });
    expect(path).toBe(`/api/accounts/v1/libraries/${a}/memory/editor/inspect`);
    expect(new Headers(options.headers).get('X-QP-CSRF')).toBe('synthetic-token');
    expect(options.credentials).toBe('same-origin');
    expect(JSON.parse(String(options.body))).toEqual({ promptId: b, revision: 4, templateText: 'Explica <tema>.', values: { tema: '<literal>\nñ' } });
    return Response.json({ variableNames: ['tema'], compiledText: 'Explica <literal>\nñ.', missingVariables: [] });
  });
  const service = createAccountService(createApiClient({ send }, ''));
  expect((await service.inspectEditor(a, { promptId: b, revision: 4, templateText: 'Explica <tema>.', values: { tema: '<literal>\nñ' } })).compiledText).toBe('Explica <literal>\nñ.');
  expect(send).toHaveBeenCalledTimes(2);
});

it('discards late definitions across templates and libraries while keeping fields mounted during value inspection', async () => {
  const calls: { libraryId: string; input: InspectAccountEditor; signal?: AbortSignal; resolve: (value: AccountEditorInspection) => void }[] = [];
  const service = { ...createAccountService(), inspectEditor: vi.fn((libraryId: string, input: InspectAccountEditor, signal?: AbortSignal) =>
    new Promise<AccountEditorInspection>(resolve => calls.push({ libraryId, input, signal, resolve }))) };
  const onExpired = vi.fn();
  const initial = { libraryId: a, text: '<vieja>', values: {} as Record<string, string> };
  const hook = renderHook(props => useAccountEditorInspection(props.libraryId, null, props.text, props.values, service, onExpired), { initialProps: initial });
  await waitFor(() => expect(calls).toHaveLength(1));
  hook.rerender({ ...initial, text: '<nueva>' });
  expect(calls[0].signal?.aborted).toBe(true);
  await act(async () => calls[0].resolve({ variableNames: ['vieja'], compiledText: null, missingVariables: ['vieja'] }));
  expect(hook.result.current.variableNames).toEqual([]);
  await waitFor(() => expect(calls).toHaveLength(2));
  await act(async () => calls[1].resolve({ variableNames: ['nueva'], compiledText: null, missingVariables: ['nueva'] }));
  expect(hook.result.current.variableNames).toEqual(['nueva']);
  hook.rerender({ libraryId: a, text: '<nueva>', values: { nueva: 'literal ñ' } });
  expect(hook.result.current.variableNames).toEqual(['nueva']);
  expect(hook.result.current.result).toBeUndefined();
  await waitFor(() => expect(calls).toHaveLength(3));
  hook.rerender({ libraryId: b, text: '<nueva>', values: {} });
  expect(hook.result.current.variableNames).toEqual([]);
  await act(async () => calls[2].resolve({ variableNames: ['nueva'], compiledText: 'literal ñ', missingVariables: [] }));
  expect(hook.result.current.result).toBeUndefined();
  await waitFor(() => expect(calls).toHaveLength(4));
  expect(calls[3].libraryId).toBe(b); expect(calls[3].input.values).toEqual({});
  hook.unmount(); expect(calls[3].signal?.aborted).toBe(true);
});

it('hands an expired cookie back to account login and retries an inspection without persisting context', async () => {
  const service = { ...createAccountService(), inspectEditor: vi.fn().mockRejectedValueOnce(new ApiError('Sesión vencida', 401))
    .mockResolvedValue({ variableNames: [], compiledText: '', missingVariables: [] }) };
  const expired = vi.fn(); const values = {};
  const hook = renderHook(() => useAccountEditorInspection(a, null, '', values, service, expired));
  await waitFor(() => expect(expired).toHaveBeenCalledOnce());
  act(() => hook.result.current.retry());
  await waitFor(() => expect(hook.result.current.result?.compiledText).toBe(''));
  expect(service.inspectEditor).toHaveBeenCalledTimes(2);
});
