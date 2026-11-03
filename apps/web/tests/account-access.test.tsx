import { StrictMode } from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AccountForm } from '../src/features/accounts/AccountForm';
import { createAccountService } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';

const options = { email: true, google: true, delegation: false, mailDelivery: 'configured' };
const userId = '10000000-0000-4000-8000-000000000001';
const password = 'Synthetic-Account!79';
afterEach(() => { cleanup(); window.history.replaceState(null, '', '/'); });

function setup(path = '/', emailAvailable = true) {
  window.history.replaceState(null, '', path);
  const service = createAccountService();
  const login = vi.spyOn(service, 'login').mockResolvedValue(undefined);
  const register = vi.spyOn(service, 'register').mockResolvedValue({ message: 'Si corresponde, recibirás los pasos para confirmar tu cuenta.' });
  const recover = vi.spyOn(service, 'recover').mockResolvedValue({ message: 'Si corresponde, recibirás los pasos de recuperación.' });
  const resend = vi.spyOn(service, 'resend').mockResolvedValue({ message: 'Si corresponde, recibirás los pasos para confirmar tu cuenta.' });
  const reset = vi.spyOn(service, 'reset').mockResolvedValue(undefined);
  const confirm = vi.spyOn(service, 'confirm').mockResolvedValue(undefined);
  const onLogin = vi.fn();
  render(<StrictMode><AccountForm service={service} options={{ ...options, email: emailAvailable }} csrf="synthetic" onLogin={onLogin} /></StrictMode>);
  return { login, register, recover, resend, reset, confirm, onLogin };
}

function email() { fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'qa@example.invalid' } }); }
function newPassword(confirmation = password) {
  fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: password } });
  fireEvent.change(screen.getByLabelText('Repite la contraseña'), { target: { value: confirmation } });
}

it.each([
  ['login', 'Inicia sesión'], ['register', 'Crea tu cuenta'],
  ['recover', 'Recupera tu acceso'], ['resend', 'Reenvía tu confirmación'],
])('opens the direct %s route', (route, title) => {
  setup(`/account/${route}`);
  expect(screen.getByRole('heading', { name: title })).toBeTruthy();
});

it('registers once after matching passwords without logging in or claiming delivery', async () => {
  const service = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Crear una cuenta' }));
  expect(window.location.pathname).toBe('/account/register');
  email(); newPassword('Different-Synthetic!80');
  fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Las contraseñas no coinciden.');
  expect(service.register).not.toHaveBeenCalled();
  newPassword();
  fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect(await screen.findByRole('status')).toHaveProperty('textContent', 'Si corresponde, recibirás los pasos para confirmar tu cuenta.');
  expect(service.register).toHaveBeenCalledExactlyOnceWith('qa@example.invalid', password, expect.any(AbortSignal));
  expect(service.onLogin).not.toHaveBeenCalled();
  expect(screen.getByLabelText('Contraseña')).toHaveProperty('value', '');
  expect(screen.getByLabelText('Repite la contraseña')).toHaveProperty('value', '');
});

it.each([
  ['recover', 'Enviar recuperación'], ['resend', 'Reenviar confirmación'],
] as const)('submits %s once and preserves the generic server response', async (mode, label) => {
  const service = setup(`/account/${mode}`); email();
  fireEvent.click(screen.getByRole('button', { name: label }));
  await screen.findByRole('status');
  expect(service[mode]).toHaveBeenCalledExactlyOnceWith('qa@example.invalid', expect.any(AbortSignal));
  expect(service.onLogin).not.toHaveBeenCalled();
});

it.each([
  ['register', 'Crear cuenta'], ['recover', 'Enviar recuperación'], ['resend', 'Reenviar confirmación'],
] as const)('explains unavailable mail and prevents %s even for synthetic form submission', async (mode, label) => {
  const service = setup(`/account/${mode}`, false);
  expect(screen.getByText(/El registro y la recuperación por correo no están disponibles/)).toBeTruthy();
  const button = screen.getByRole('button', { name: label });
  expect(button).toHaveProperty('disabled', true);
  email(); if (mode === 'register') newPassword();
  await act(async () => fireEvent.submit(button.closest('form')!));
  expect(service[mode]).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Volver a iniciar sesión' }));
  expect(screen.getByRole('button', { name: 'Entrar' })).toHaveProperty('disabled', false);
});

it('retains a reset token only in memory and returns to the login route after success', async () => {
  const service = setup(`/account/reset?userId=${userId}&code=synthetic-token`);
  expect(window.location.search).toBe(''); newPassword();
  fireEvent.click(screen.getByRole('button', { name: 'Guardar contraseña' }));
  await screen.findByText('Contraseña actualizada. Ya puedes entrar.');
  expect(service.reset).toHaveBeenCalledExactlyOnceWith({ userId, code: 'synthetic-token' }, password, expect.any(AbortSignal));
  expect(window.location.pathname).toBe('/account/login');
  expect(service.onLogin).not.toHaveBeenCalled();
});

it('offers a fresh recovery request when a reset link is incomplete', async () => {
  const service = setup('/account/reset'); newPassword();
  fireEvent.click(screen.getByRole('button', { name: 'Guardar contraseña' }));
  await screen.findByText('Abre el enlace completo recibido para continuar.');
  expect(service.reset).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Solicitar otro enlace de recuperación' }));
  expect(window.location.pathname).toBe('/account/recover');
  expect(screen.getByRole('heading', { name: 'Recupera tu acceso' })).toBeTruthy();
});

it('provides confirmation resend from login without sending implicitly', () => {
  const service = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Reenviar correo de confirmación' }));
  expect(window.location.pathname).toBe('/account/resend');
  expect(service.resend).not.toHaveBeenCalled();
});

it('sends confirmation resend through the existing cookie and CSRF protocol', async () => {
  const send = vi.fn(async (path: string) => path === '/account/csrf'
    ? Response.json({ token: 'synthetic-csrf' }) : Response.json({ message: 'Si corresponde, recibirás los pasos para confirmar tu cuenta.' }, { status: 202 }));
  const service = createAccountService(createApiClient({ send }, ''));
  await service.resend('qa@example.invalid');
  expect(send.mock.calls.map(call => call[0])).toEqual(['/account/csrf', '/account/resend']);
  const init = (send.mock.calls as unknown as [string, RequestInit][])[1][1];
  expect(init.credentials).toBe('same-origin');
  expect(new Headers(init.headers).get('X-QP-CSRF')).toBe('synthetic-csrf');
  expect(JSON.parse(init.body as string)).toEqual({ email: 'qa@example.invalid' });
});
