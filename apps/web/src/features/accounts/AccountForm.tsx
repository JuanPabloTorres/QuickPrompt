import { useEffect, useMemo, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import { InlineSpinner } from '../../components/ui/Feedback';
import { ApiError, errorMessage } from '../../services/api';
import { useBoundOperation } from '../../services/useBoundOperation';
import type { AccountFormMode, AccountOptions, AccountService, AccountToken } from '../../services/accounts';

function initialForm(): { mode: AccountFormMode; token?: AccountToken } {
  const mode = window.location.pathname === '/account/confirm' ? 'confirm' : window.location.pathname === '/account/reset' ? 'reset' : 'login';
  if (mode === 'login') return { mode };
  const query = new URLSearchParams(window.location.search);
  const token = { userId: query.get('userId') ?? '', code: query.get('code') ?? '' };
  return { mode, token };
}
const titles: Record<AccountFormMode, string> = { login: 'Bienvenido a tu espacio', register: 'Crea tu cuenta', recover: 'Recupera tu acceso', confirm: 'Confirma tu correo', reset: 'Elige una nueva contraseña' };

export function AccountForm({ service, options, csrf, onLogin }: { service: AccountService; options: AccountOptions; csrf: string; onLogin: () => void }) {
  const [initial] = useState(initialForm);
  useEffect(() => { if (initial.mode !== 'login') window.history.replaceState(null, '', window.location.pathname); }, [initial.mode]);
  const [mode, setMode] = useState<AccountFormMode>(initial.mode);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const snapshot = useMemo(() => ({ mode, service }), [mode, service]);
  const operation = useBoundOperation(snapshot);
  function choose(next: AccountFormMode) { setMode(next); setPassword(''); setError(''); setNotice(''); }
  async function submit() {
    const pending = operation.begin(); if (!pending) return;
    setError(''); setNotice('');
    try {
      let message = '';
      if (mode === 'login') await service.login(email, password, pending.signal);
      else if (mode === 'register') message = (await service.register(email, password, pending.signal)).message;
      else if (mode === 'recover') message = (await service.recover(email, pending.signal)).message;
      else {
        if (!initial.token?.userId || !initial.token.code) throw new Error('Abre el enlace completo recibido para continuar.');
        if (mode === 'confirm') await service.confirm(initial.token, pending.signal);
        else await service.reset(initial.token, password, pending.signal);
        message = mode === 'confirm' ? 'Correo confirmado. Ya puedes entrar.' : 'Contraseña actualizada. Ya puedes entrar.';
      }
      if (operation.isCurrent(pending)) {
        if (mode === 'login') onLogin();
        else { setNotice(message); setPassword(''); if (mode === 'confirm' || mode === 'reset') setMode('login'); }
      }
    } catch (failure) {
      if (operation.isCurrent(pending)) setError(failure instanceof ApiError && failure.status === 401
        ? 'No pudimos iniciar sesión. Revisa tus datos, la confirmación del correo o vuelve a intentarlo más tarde.' : errorMessage(failure));
    } finally { operation.finish(pending); }
  }
  const needsEmail = mode === 'login' || mode === 'register' || mode === 'recover';
  const needsPassword = mode === 'login' || mode === 'register' || mode === 'reset';
  return <section className="account-card" aria-labelledby="account-form-title">
    <span className="eyebrow">TU CUENTA QUICKPROMPT</span><h2 id="account-form-title">{titles[mode]}</h2>
    {mode === 'login' && options.google && <><form action="/account/google" method="post"><input type="hidden" name="__RequestVerificationToken" value={csrf} /><button className="secondary account-google" disabled={operation.busy || !csrf}>Continuar con Google<AppIcon glyph={ArrowRight} size={16} /></button></form><div className="account-divider"><span>o utiliza tu correo</span></div></>}
    {options.mailDelivery === 'development-pickup' && mode !== 'login' && <p className="account-helper">Entorno de prueba: los mensajes se guardan en el buzón local de desarrollo.</p>}
    <form onSubmit={event => { event.preventDefault(); void submit(); }} aria-busy={operation.busy}>
      <fieldset disabled={operation.busy}>
        {needsEmail && <div><label htmlFor="account-email">Correo electrónico</label><input id="account-email" type="email" value={email} onChange={event => setEmail(event.target.value)} required maxLength={256} autoComplete="email" autoCapitalize="none" /></div>}
        {needsPassword && <div><label htmlFor="account-password">Contraseña</label><input id="account-password" type="password" value={password} onChange={event => setPassword(event.target.value)} required minLength={mode === 'login' ? undefined : 12} maxLength={256} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} aria-describedby={mode === 'login' ? undefined : 'account-password-help'} />{mode !== 'login' && <p id="account-password-help" className="account-helper">12 caracteres o más, con mayúsculas, minúsculas, números y un símbolo.</p>}</div>}
        {mode === 'confirm' && <p>Confirma que este correo te pertenece para activar tu cuenta.</p>}
        <button className="primary account-submit" disabled={!options.email && (mode === 'register' || mode === 'recover')}>{operation.busy && <InlineSpinner />}{mode === 'login' ? 'Entrar' : mode === 'register' ? 'Crear cuenta' : mode === 'recover' ? 'Enviar recuperación' : mode === 'confirm' ? 'Confirmar correo' : 'Guardar contraseña'}<AppIcon glyph={ArrowRight} size={16} /></button>
      </fieldset>
    </form>
    {error && <p role="alert" className="error">{error}</p>}{notice && <p role="status" className="account-notice">{notice}</p>}
    <div className="account-links">{mode === 'login' ? <><button type="button" onClick={() => choose('recover')} disabled={operation.busy}>Olvidé mi contraseña</button><button type="button" onClick={() => choose('register')} disabled={operation.busy || !options.email}>Crear una cuenta</button></> : <button type="button" onClick={() => choose('login')} disabled={operation.busy}>Volver a iniciar sesión</button>}</div>
  </section>;
}
