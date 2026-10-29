import { act, cleanup, fireEvent, render, renderHook, screen, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AccountPage } from '../src/features/accounts/AccountPage';
import { AccountWorkspaceSidebar } from '../src/features/accounts/AccountWorkspaceSidebar';
import { WorkspaceFooter } from '../src/components/WorkspaceFooter';
import { useInterfaceTheme } from '../src/hooks/useInterfaceTheme';
import { createApiClient } from '../src/services/api';
import { createAccountService } from '../src/services/accounts';

const actor = { id: '10000000-0000-4000-8000-000000000001', email: 'shell@example.invalid' };

afterEach(() => {
  cleanup(); vi.restoreAllMocks(); localStorage.clear();
  delete document.documentElement.dataset.theme;
});

function accountFixture() {
  let current = () => Promise.resolve(Response.json(actor));
  const paths: string[] = [];
  const send = vi.fn(async (path: string) => {
    paths.push(path);
    if (path === '/account/me') return current();
    if (path === '/account/options') return Response.json({ email: true, google: false, delegation: true, mailDelivery: 'unconfigured' });
    if (path === '/account/csrf') return Response.json({ token: 'fixture-csrf' });
    if (path === '/api/accounts/v1/libraries/') return Response.json([]);
    if (path === '/account/preferences/') return Response.json({ language: null, format: null, providerId: null });
    if (path === '/account/logout') {
      current = async () => new Response(null, { status: 401 });
      return new Response(null, { status: 204 });
    }
    throw new Error(`Unexpected account shell request: ${path}`);
  });
  return { service: createAccountService(createApiClient({ send }, '')), paths,
    holdIdentity: () => {
      let resolve!: (response: Response) => void;
      const pending = new Promise<Response>(finish => { resolve = finish; });
      current = () => pending;
      return () => resolve(Response.json(actor));
    } };
}

it('uses the shared authenticated shell and account-owned navigation without local library traffic', async () => {
  const fixture = accountFixture();
  render(<AccountPage service={fixture.service} />);
  await screen.findByText(actor.email);
  // Identity is rendered before the library bootstrap effect finishes. Measure
  // theme-only traffic after that independent request has settled.
  await screen.findByRole('heading', { name: 'Tu primera biblioteca empieza aquí.' });
  const sidebar = screen.getByRole('complementary', { name: 'Barra lateral de QuickPrompt' });
  expect(sidebar.closest('.accounts-workspace.app-shell')).toBeTruthy();
  expect(within(sidebar).getByRole('button', { name: 'QuickPrompt NOVA, biblioteca' }).querySelector('img')?.getAttribute('src')).toBe('/branding/quickprompt_official.png');
  expect(screen.queryByRole('banner')).toBeNull();
  expect(screen.getByRole('contentinfo').textContent).toContain('Bibliotecas privadas de tu cuenta');
  expect(screen.getByRole('contentinfo').textContent).not.toContain('Datos en tu equipo');
  expect(within(sidebar).getByRole('button', { name: 'Mis bibliotecas' }).getAttribute('aria-current')).toBe('page');
  const requestCount = fixture.paths.length;
  fireEvent.click(within(sidebar).getByRole('button', { name: 'Activar tema claro' }));
  expect(document.documentElement.dataset.theme).toBe('light');
  expect(fixture.paths).toHaveLength(requestCount);
  fireEvent.click(within(sidebar).getByRole('button', { name: 'Mis preferencias' }));
  await screen.findByText('Preferencias de NOVA');
  expect(within(sidebar).getByRole('button', { name: 'Mis preferencias' }).getAttribute('aria-current')).toBe('page');
  fireEvent.click(within(sidebar).getByRole('button', { name: 'QuickPrompt NOVA, biblioteca' }));
  expect(await screen.findByRole('heading', { name: 'Mis bibliotecas' })).toBeTruthy();
  expect(fixture.paths.every(path => path.startsWith('/account/') || path.startsWith('/api/accounts/'))).toBe(true);
  expect(Array.from({ length: localStorage.length }, (_, index) => localStorage.key(index))).toEqual(['nova-theme']);
  fireEvent.click(within(sidebar).getByRole('button', { name: 'Cerrar sesión' }));
  await screen.findByRole('heading', { name: 'Bienvenido a tu espacio' });
  expect(screen.queryByRole('complementary', { name: 'Barra lateral de QuickPrompt' })).toBeNull();
  expect(screen.queryByText(actor.email)).toBeNull();
});

it('masks private navigation during identity verification and restores the same nodes after confirmation', async () => {
  const fixture = accountFixture();
  // Finish bootstrap effects before dispatching the foreground interaction.
  await act(async () => { render(<AccountPage service={fixture.service} />); });
  await screen.findByText(actor.email);
  const sidebar = screen.getByRole('complementary', { name: 'Barra lateral de QuickPrompt' });
  const finish = fixture.holdIdentity();
  await act(async () => { fireEvent.focus(window); });
  expect(fixture.paths.filter(path => path === '/account/me')).toHaveLength(2);
  expect(screen.queryByRole('complementary', { name: 'Barra lateral de QuickPrompt' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Cerrar sesión' })).toBeNull();
  expect(sidebar.isConnected).toBe(true);
  expect(screen.getByRole('button', { name: 'Volver a comprobar acceso' })).toBeTruthy();
  await act(async () => finish());
  expect(await screen.findByRole('complementary', { name: 'Barra lateral de QuickPrompt' })).toBe(sidebar);
  expect(fixture.paths.filter(path => path === '/api/accounts/v1/libraries/')).toHaveLength(1);
});

it('keeps sidebar capabilities explicit and busy logout disabled', () => {
  const onView = vi.fn(), onLogout = vi.fn();
  render(<AccountWorkspaceSidebar view="libraries" theme="dark" busy delegation={false} onHome={vi.fn()} onView={onView} onTheme={vi.fn()} onLogout={onLogout} />);
  expect(screen.queryByRole('button', { name: 'Conexiones locales' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Invitaciones' }));
  expect(onView).toHaveBeenCalledWith('invitations');
  const logout = screen.getByRole('button', { name: 'Cerrar sesión' });
  expect(logout).toHaveProperty('disabled', true);
  fireEvent.click(logout); expect(onLogout).not.toHaveBeenCalled();
});

it('preserves the local footer defaults and its existing guide callback', () => {
  const help = vi.fn();
  render(<WorkspaceFooter onHelp={help} />);
  expect(screen.getByRole('contentinfo').textContent).toContain('Datos en tu equipo');
  fireEvent.click(screen.getByRole('button', { name: 'Guía de QuickPrompt' }));
  expect(help).toHaveBeenCalledOnce();
});

it('uses the existing device theme preference and remains usable when storage is unavailable', () => {
  localStorage.setItem('nova-theme', 'light');
  const first = renderHook(useInterfaceTheme);
  expect(first.result.current[0]).toBe('light');
  expect(document.documentElement.dataset.theme).toBe('light');
  first.unmount();
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Storage unavailable'); });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Storage unavailable'); });
  const fallback = renderHook(useInterfaceTheme);
  expect(fallback.result.current[0]).toBe('dark');
  act(() => fallback.result.current[1]('light'));
  expect(document.documentElement.dataset.theme).toBe('light');
});
