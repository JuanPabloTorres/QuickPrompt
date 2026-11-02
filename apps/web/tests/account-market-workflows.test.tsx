import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { NovaPreferenceEditor } from '../src/features/settings/NovaPreferences';
import { AccountPromptVersions } from '../src/features/accounts/AccountPromptVersions';
import { AccountPromptFocus } from '../src/features/accounts/AccountPromptFocus';
import * as textExport from '../src/services/textExport';
import { createAccountService, type AccountPromptVersion } from '../src/services/accounts';
import { ApiError, createApiClient } from '../src/services/api';
import type { PreferenceClient, PreparationPreferences } from '../src/services/preferences';

const libraryId = '10000000-0000-4000-8000-000000000001';
const prompt = { libraryId, id: '10000000-0000-4000-8000-000000000002', title: 'Actual', template: 'Explica <tema>.', revision: 2 };
const blank: PreparationPreferences = { language: null, format: null, providerId: null };
afterEach(cleanup);

it('uses account preferences with fresh CSRF, explicit confirmation and no local endpoint fallback', async () => {
  const calls: { path: string; method: string }[] = [];
  const send = vi.fn(async (path: string, options: RequestInit) => {
    calls.push({ path, method: options.method ?? 'GET' });
    if (path === '/account/csrf') return Response.json({ token: 'qa-csrf' });
    expect(path).toBe('/account/preferences/');
    if (options.method === 'POST') {
      expect(new Headers(options.headers).get('X-QP-CSRF')).toBe('qa-csrf');
      expect(JSON.parse(String(options.body))).toEqual({ preferences: { ...blank, language: 'es' }, confirmed: true });
      return Response.json({ ...blank, language: 'es' });
    }
    if (options.method === 'DELETE') { expect(new Headers(options.headers).get('X-QP-CSRF')).toBe('qa-csrf'); return new Response(null, { status: 204 }); }
    return Response.json(blank);
  });
  const client = createAccountService(createApiClient({ send }, '')).preferences;
  render(<NovaPreferenceEditor client={client} savedMessage="Guardadas en mi cuenta" />);
  fireEvent.click(screen.getByText('Preferencias de NOVA'));
  await waitFor(() => expect(screen.getByLabelText('Idioma de respuesta')).toHaveProperty('disabled', false));
  fireEvent.change(screen.getByLabelText('Idioma de respuesta'), { target: { value: 'es' } });
  expect(calls).toHaveLength(1);
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar preferencias' }));
  await screen.findByText('Guardadas en mi cuenta');
  fireEvent.click(screen.getByRole('button', { name: 'Eliminar preferencias' }));
  await screen.findByText('Preferencias eliminadas.');
  expect(calls.filter(call => call.path === '/account/csrf')).toHaveLength(2);
});

it('cannot show a late preference read or complete a save after the account scope is retired', async () => {
  let loaded!: (value: PreparationPreferences) => void;
  let saved!: (value: PreparationPreferences) => void;
  const a: PreferenceClient = { get: vi.fn(() => new Promise<PreparationPreferences>(resolve => { loaded = resolve; })), save: vi.fn(() => new Promise<PreparationPreferences>(resolve => { saved = resolve; })), forget: vi.fn() };
  const b: PreferenceClient = { get: vi.fn(async () => blank), save: vi.fn(), forget: vi.fn() };
  const view = render(<NovaPreferenceEditor client={a} savedMessage="Cuenta A" />);
  view.rerender(<NovaPreferenceEditor client={b} savedMessage="Cuenta B" />);
  await act(async () => loaded({ language: 'en', format: 'JSON', providerId: 'chatgpt' }));
  expect(screen.getByLabelText('Idioma de respuesta')).toHaveProperty('value', '');
  view.rerender(<NovaPreferenceEditor client={{ ...a, get: async () => blank }} savedMessage="Cuenta A" />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar preferencias' })).toHaveProperty('disabled', false));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar preferencias' }));
  view.rerender(<NovaPreferenceEditor client={b} savedMessage="Cuenta B" />);
  await act(async () => saved({ language: 'en', format: null, providerId: null }));
  expect(screen.queryByText('Cuenta A')).toBeNull();
  expect(screen.getByLabelText('Idioma de respuesta')).toHaveProperty('value', '');
});

it('returns an expired private session to its owner and does not substitute defaults as saved preferences', async () => {
  const expired = vi.fn();
  const client: PreferenceClient = { get: vi.fn(async () => { throw new ApiError('Expired', 401); }), save: vi.fn(), forget: vi.fn() };
  render(<NovaPreferenceEditor client={client} savedMessage="Saved" onExpired={expired} />);
  await waitFor(() => expect(expired).toHaveBeenCalledTimes(1));
  expect(screen.getByRole('button', { name: 'Confirmar preferencias' })).toHaveProperty('disabled', true);
  expect(client.save).not.toHaveBeenCalled();
});

function versions(): AccountPromptVersion[] {
  return [2, 1].map(revision => ({ libraryId, promptId: prompt.id, revision, title: revision === 1 ? 'Anterior' : 'Actual', template: revision === 1 ? '<img src=x onerror=alert(1)>' : prompt.template, createdAt: '2026-10-27T00:00:00Z' }));
}

it('reviews private versions literally and creates a new version only after an explicit content confirmation', async () => {
  const service = { ...createAccountService(), versions: vi.fn(async () => versions()), savePrompt: vi.fn(async () => ({ ...prompt, revision: 3 })) };
  const onSaved = vi.fn();
  render(<AccountPromptVersions prompt={prompt} service={service} onExpired={vi.fn()} onSaved={onSaved} onClose={vi.fn()} />);
  fireEvent.change(await screen.findByLabelText('Versión guardada'), { target: { value: '1' } });
  await screen.findByText('<img src=x onerror=alert(1)>'); expect(document.querySelector('img')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Recuperar título y contenido' }));
  expect(service.savePrompt).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Conservar versión actual' }));
  expect(service.savePrompt).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Recuperar título y contenido' }));
  fireEvent.click(screen.getByRole('button', { name: 'Crear nueva versión con este contenido' }));
  await waitFor(() => expect(onSaved).toHaveBeenCalledWith({ ...prompt, revision: 3 }));
  expect(service.savePrompt).toHaveBeenCalledWith(libraryId, prompt, { title: 'Anterior', template: '<img src=x onerror=alert(1)>' }, expect.any(AbortSignal));
});

it('keeps a stale version conflict visible and never retries the write or claims restoration', async () => {
  const service = { ...createAccountService(), versions: vi.fn(async () => versions()), savePrompt: vi.fn(async () => { throw new ApiError('La revisión cambió', 409); }) };
  const onSaved = vi.fn();
  render(<AccountPromptVersions prompt={prompt} service={service} onExpired={vi.fn()} onSaved={onSaved} onClose={vi.fn()} />);
  fireEvent.change(await screen.findByLabelText('Versión guardada'), { target: { value: '1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Recuperar título y contenido' }));
  fireEvent.click(screen.getByRole('button', { name: 'Crear nueva versión con este contenido' }));
  await screen.findByRole('alert'); expect(service.savePrompt).toHaveBeenCalledTimes(1); expect(onSaved).not.toHaveBeenCalled();
});

it('exports only a fresh server-approved portable packet and refuses an obsolete revision', async () => {
  const download = vi.spyOn(textExport, 'downloadPreparedText').mockImplementation(() => {});
  const packet = { format: 'quickprompt-template' as const, formatVersion: 1 as const, prompt: { title: prompt.title, template: prompt.template, description: '', category: 0 } };
  const service = { ...createAccountService(), exportPrompt: vi.fn(async () => packet),
    inspectEditor: vi.fn(async () => ({ variableNames: [], compiledText: prompt.template, missingVariables: [] })) };
  render(<AccountPromptFocus prompt={prompt} service={service} onExpired={vi.fn()} onEdit={vi.fn()} onClose={vi.fn()} onSaved={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Exportar plantilla JSON' }));
  await waitFor(() => expect(download).toHaveBeenCalledWith(JSON.stringify(packet, null, 2), expect.stringContaining('-v2.json')));
  service.exportPrompt.mockRejectedValueOnce(new ApiError('La revisión cambió', 409));
  fireEvent.click(screen.getByRole('button', { name: 'Exportar plantilla JSON' }));
  await screen.findByRole('alert'); expect(download).toHaveBeenCalledTimes(1);
  download.mockRestore();
});
