import { useEffect, useMemo, useState, type ComponentPropsWithoutRef } from 'react';
import { ArrowLeft, KeyRound, LockKeyhole, LogIn, MailCheck, MailPlus, MailQuestion, Send, UserRoundPlus, type LucideIcon } from 'lucide-react';
import { AppIcon } from '../../components/ui/Icon';
import { InlineSpinner } from '../../components/ui/Feedback';
import googleSignIn from '../../design/google-sign-in.svg';
import { ApiError, errorMessage } from '../../services/api';
import { useBoundOperation } from '../../services/useBoundOperation';
import type { AccountFormMode, AccountOptions, AccountService, AccountToken } from '../../services/accounts';

function initialForm(): { mode: AccountFormMode; token?: AccountToken } {
  const mode = (['login', 'register', 'recover', 'resend', 'confirm', 'reset'] as const)
    .find(value => window.location.pathname === `/account/${value}`) ?? 'login';
  if (mode !== 'confirm' && mode !== 'reset') return { mode };
  const query = new URLSearchParams(window.location.search);
  const token = { userId: query.get('userId') ?? '', code: query.get('code') ?? '' };
  return { mode, token };
}
const accountActions: Record<AccountFormMode, { title: string; label: string; glyph: LucideIcon }> = {
  login: { title: 'Inicia sesión', label: 'Entrar', glyph: LogIn },
  register: { title: 'Crea tu cuenta', label: 'Crear cuenta', glyph: UserRoundPlus },
  recover: { title: 'Recupera tu acceso', label: 'Enviar recuperación', glyph: Send },
  resend: { title: 'Reenvía tu confirmación', label: 'Reenviar confirmación', glyph: Send },
  confirm: { title: 'Confirma tu correo', label: 'Confirmar correo', glyph: MailCheck },
  reset: { title: 'Elige una nueva contraseña', label: 'Guardar contraseña', glyph: LockKeyhole },
};
const accountSteps: Partial<Record<AccountFormMode, { step: string; guide: string }>> = {
  register: { step: 'Tu correo · Paso 1 de 2', guide: '¿Qué correo quieres usar? Te enviaremos un enlace para confirmar tu cuenta.' },
  confirm: { step: 'Tu correo · Paso 2 de 2', guide: 'Llegaste desde tu correo. Confirma que te pertenece para activar tu cuenta.' },
  recover: { step: 'Recuperar acceso · Paso 1 de 2', guide: '¿Con qué correo entras a QuickPrompt? Si corresponde, te enviaremos un enlace para recuperar el acceso.' },
  reset: { step: 'Recuperar acceso · Paso 2 de 2', guide: 'Elige una contraseña nueva para volver a entrar a tu cuenta.' },
  resend: { step: 'Confirmación por correo', guide: '¿A qué correo debemos reenviar el enlace? Revisa también la carpeta de spam.' },
};

function accountFailure(mode: AccountFormMode, failure: unknown) {
  if (failure instanceof ApiError) {
    if (mode === 'login' && failure.status === 401)
      return 'No pudimos iniciar sesión. Revisa tus datos y la confirmación del correo.';
    if (failure.status === 400 && mode === 'reset')
      return 'No pudimos cambiar la contraseña. Revisa sus requisitos o solicita otro enlace de recuperación.';
    if (failure.status === 400 && mode === 'confirm')
      return 'No pudimos confirmar el correo con este enlace. Solicita otro enlace de confirmación.';
  }
  return errorMessage(failure);
}

function AccountIconButton({ label, glyph, children, ...button }: ComponentPropsWithoutRef<'button'> & { label: string; glyph?: LucideIcon }) {
  return <span className="account-icon-action">
    <button {...button} aria-label={label}>{children ?? (glyph && <AppIcon glyph={glyph} />)}</button>
    <span className="account-icon-hint" aria-hidden="true">{label}</span>
  </span>;
}

export function AccountForm({ service, options, csrf, onLogin }: { service: AccountService; options: AccountOptions; csrf: string; onLogin: () => void }) {
  const [initial] = useState(initialForm);
  useEffect(() => { if (initial.token) window.history.replaceState(null, '', window.location.pathname); }, [initial]);
  const [mode, setMode] = useState<AccountFormMode>(initial.mode);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const snapshot = useMemo(() => ({ mode, service }), [mode, service]);
  const operation = useBoundOperation(snapshot);
  function choose(next: AccountFormMode) {
    window.history.replaceState(null, '', `/account/${next}`);
    setMode(next); setPassword(''); setConfirmation(''); setError(''); setNotice('');
  }
  async function submit() {
    if (!options.email && (mode === 'register' || mode === 'recover' || mode === 'resend')) return;
    if ((mode === 'register' || mode === 'reset') && password !== confirmation) {
      setError('Las contraseñas no coinciden.'); return;
    }
    const pending = operation.begin(); if (!pending) return;
    setError(''); setNotice('');
    try {
      let message = '';
      if (mode === 'login') await service.login(email, password, pending.signal);
      else if (mode === 'register') message = (await service.register(email, password, pending.signal)).message;
      else if (mode === 'recover') message = (await service.recover(email, pending.signal)).message;
      else if (mode === 'resend') message = (await service.resend(email, pending.signal)).message;
      else {
        if (!initial.token?.userId || !initial.token.code) throw new Error('Abre el enlace completo recibido para continuar.');
        if (mode === 'confirm') await service.confirm(initial.token, pending.signal);
        else await service.reset(initial.token, password, pending.signal);
        message = mode === 'confirm' ? 'Correo confirmado. Ya puedes entrar.' : 'Contraseña actualizada. Ya puedes entrar.';
      }
      if (operation.isCurrent(pending)) {
        if (mode === 'login') onLogin();
        else {
          if (mode === 'confirm' || mode === 'reset') choose('login');
          setNotice(message); setPassword(''); setConfirmation('');
        }
      }
    } catch (failure) {
      if (operation.isCurrent(pending)) setError(accountFailure(mode, failure));
    } finally { operation.finish(pending); }
  }
  const needsEmail = mode === 'login' || mode === 'register' || mode === 'recover' || mode === 'resend';
  const needsPassword = mode === 'login' || mode === 'register' || mode === 'reset';
  const guidance = accountSteps[mode];
  const action = accountActions[mode];
  return <section className="account-card" aria-labelledby="account-form-title">
    <div className="account-card-heading"><span className="account-card-symbol"><AppIcon glyph={mode === 'login' ? LockKeyhole : action.glyph} /></span><h2 id="account-form-title">{action.title}</h2></div>
    {options.mailDelivery === 'development-pickup' && mode !== 'login' && <p className="account-helper">Entorno de prueba: los mensajes se guardan en el buzón local de desarrollo.</p>}
    {!options.email && needsEmail && <p className="account-helper">El registro y la recuperación por correo no están disponibles.{options.google && mode === 'login' ? ' Puedes continuar con Google.' : ' Puedes volver a iniciar sesión con una cuenta existente.'}</p>}
    {guidance && (options.email || !needsEmail) && <div className="account-guide"><span>{guidance.step}</span><p>{guidance.guide}</p></div>}
    <form onSubmit={event => { event.preventDefault(); void submit(); }} aria-busy={operation.busy}>
      <fieldset disabled={operation.busy}>
        {needsEmail && <div><label htmlFor="account-email">Correo electrónico</label><input id="account-email" type="email" value={email} onChange={event => setEmail(event.target.value)} required maxLength={256} autoComplete="email" autoCapitalize="none" /></div>}
        {needsPassword && <div><label htmlFor="account-password">Contraseña</label><input id="account-password" type="password" value={password} onChange={event => setPassword(event.target.value)} required minLength={mode === 'login' ? undefined : 12} maxLength={256} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} aria-describedby={mode === 'login' ? undefined : 'account-password-help'} />{mode !== 'login' && <p id="account-password-help" className="account-helper">12 caracteres o más, con mayúsculas, minúsculas, números y un símbolo.</p>}</div>}
        {(mode === 'register' || mode === 'reset') && <div><label htmlFor="account-password-confirmation">Repite la contraseña</label><input id="account-password-confirmation" type="password" value={confirmation} onChange={event => { setConfirmation(event.target.value); setError(''); }} required maxLength={256} autoComplete="new-password" aria-invalid={error === 'Las contraseñas no coinciden.'} aria-describedby={error === 'Las contraseñas no coinciden.' ? 'account-form-error' : undefined} /></div>}
        <AccountIconButton type="submit" className="primary icon-button account-submit" label={action.label} glyph={action.glyph} disabled={!options.email && needsEmail && mode !== 'login'}>{operation.busy ? <InlineSpinner /> : undefined}</AccountIconButton>
      </fieldset>
    </form>
    {error && <p id="account-form-error" role="alert" className="error">{error}</p>}
    {notice && <div className="account-notice">
      <p role="status">{notice}</p>
      {needsEmail && mode !== 'login' && <p>Revisa tu bandeja de entrada y la carpeta de spam.</p>}
    </div>}
    <div className="account-links" role="group" aria-label="Opciones de acceso">
      {mode === 'login' ? <>
        {options.google && <form action="/account/google" method="post"><input type="hidden" name="__RequestVerificationToken" value={csrf} /><AccountIconButton type="submit" className="account-google" label="Continuar con Google" disabled={operation.busy || !csrf}><img src={googleSignIn} alt="" width="40" height="40" /></AccountIconButton></form>}
        <AccountIconButton type="button" className="secondary icon-button" label="Crear una cuenta" glyph={UserRoundPlus} onClick={() => choose('register')} disabled={operation.busy} />
        <AccountIconButton type="button" className="secondary icon-button" label="Olvidé mi contraseña" glyph={KeyRound} onClick={() => choose('recover')} disabled={operation.busy} />
      </> : <AccountIconButton type="button" className="secondary icon-button" label="Volver a iniciar sesión" glyph={ArrowLeft} onClick={() => choose('login')} disabled={operation.busy} />}
      {(mode === 'login' || mode === 'register' || mode === 'confirm') && <AccountIconButton type="button" className="secondary icon-button" label="Reenviar correo de confirmación" glyph={MailPlus} onClick={() => choose('resend')} disabled={operation.busy} />}
      {mode === 'reset' && <AccountIconButton type="button" className="secondary icon-button" label="Solicitar otro enlace de recuperación" glyph={MailQuestion} onClick={() => choose('recover')} disabled={operation.busy} />}
    </div>
  </section>;
}
