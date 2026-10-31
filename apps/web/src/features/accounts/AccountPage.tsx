import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, ArrowRight, LockKeyhole, Plus, ShieldCheck } from 'lucide-react';
import { AppBrand } from '../../components/ui/AppBrand';
import { AppIcon } from '../../components/ui/Icon';
import { ErrorState, InlineSpinner } from '../../components/ui/Feedback';
import { errorMessage } from '../../services/api';
import type { AccountLibrary, AccountOptions, AccountService, AccountUser } from '../../services/accounts';
import { AccountForm } from './AccountForm';
import { AccountPromptLibrary } from './AccountPromptLibrary';
import { AccountInvitations } from './AccountSharing';
import { AccountConnections } from './AccountConnections';
import { useAccountOperation } from './useAccountOperation';
import { UiPortalScopeProvider } from '../../components/ui/UiPortalScope';
import { NovaPreferenceEditor } from '../settings/NovaPreferences';
import { WorkspaceFooter } from '../../components/WorkspaceFooter';
import { useInterfaceTheme } from '../../hooks/useInterfaceTheme';
import { AccountWorkspaceSidebar, type AccountWorkspaceView } from './AccountWorkspaceSidebar';

type Boot = { user: AccountUser | null; options: AccountOptions; csrf: string };
export function AccountPage({ service }: { service: AccountService }) {
  const [theme, setTheme] = useInterfaceTheme();
  const [boot, setBoot] = useState<Boot>();
  const [failure, setFailure] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [checking, setChecking] = useState(false);
  const [checkFailed, setCheckFailed] = useState(false);
  const [dialogHost, setDialogHost] = useState<HTMLDivElement | null>(null);
  const [overlayHost, setOverlayHost] = useState<HTMLDivElement | null>(null);
  const identityProbe = useRef<AbortController | null>(null);
  const reload = useCallback(() => {
    identityProbe.current?.abort(); identityProbe.current = null;
    setChecking(false); setCheckFailed(false); setBoot(undefined); setFailure(''); setRefresh(value => value + 1);
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([service.current(controller.signal), service.options(controller.signal), service.csrf(controller.signal)])
      .then(([user, options, csrf]) => { if (!controller.signal.aborted) setBoot({ user, options, csrf }); })
      .catch(error => { if (!controller.signal.aborted) setFailure(errorMessage(error)); });
    return () => controller.abort();
  }, [service, refresh]);
  const userId = boot?.user?.id;
  const verifyIdentity = useCallback(() => {
    if (!userId || identityProbe.current) return;
    const controller = new AbortController(); identityProbe.current = controller;
    const timer = setTimeout(() => {
      controller.abort();
      if (identityProbe.current === controller) { identityProbe.current = null; setCheckFailed(true); }
    }, 4000);
    setChecking(true); setCheckFailed(false);
    void (async () => {
      try {
        const user = await service.current(controller.signal);
        if (identityProbe.current !== controller || controller.signal.aborted) return;
        if (user?.id !== userId) { reload(); return; }
        setBoot(current => current?.user?.id === userId ? { ...current, user } : current);
        setChecking(false);
      } catch {
        // Unknown identity stays masked; a transient error must not expose cache.
        if (identityProbe.current === controller) setCheckFailed(true);
      } finally {
        clearTimeout(timer);
        if (identityProbe.current === controller) identityProbe.current = null;
      }
    })();
  }, [service, userId, reload]);
  useEffect(() => {
    if (!userId) return;
    // Another tab can replace the host's session cookie. Re-enter through /me
    // before exposing cached private views after returning to this window.
    const visible = () => { if (document.visibilityState === 'visible') verifyIdentity(); };
    window.addEventListener('focus', visible);
    document.addEventListener('visibilitychange', visible);
    return () => {
      window.removeEventListener('focus', visible); document.removeEventListener('visibilitychange', visible);
      identityProbe.current?.abort(); identityProbe.current = null;
    };
  }, [userId, verifyIdentity]);
  const portalScope = useMemo(() => ({ active: !checking, dialogHost, overlayHost, onRetry: verifyIdentity }), [checking, dialogHost, overlayHost, verifyIdentity]);
  return <UiPortalScopeProvider value={portalScope}><div className={boot?.user ? 'app-shell accounts-page accounts-workspace' : 'accounts-page'}>
    <a className="skip-link" href="#main">Ir al contenido</a>
    {!boot?.user && <header className="accounts-brand"><AppBrand onClick={() => { window.history.replaceState(null, '', '/'); reload(); }} /></header>}
    <main className="accounts-main" id="main" tabIndex={-1}>
      {!boot ? failure ? <ErrorState title="No pudimos abrir tu espacio" message={failure}><button className="secondary" onClick={reload}>Reintentar</button></ErrorState>
        : <p role="status"><InlineSpinner /> Preparando tu espacio…</p>
        : boot.user ? <>
          {checking && <div role={checkFailed ? 'alert' : 'status'}><p>{checkFailed ? 'No pudimos confirmar tu acceso. El contenido sigue oculto.' : 'Comprobando tu sesión. Tu trabajo está en pausa…'}</p><button type="button" className="secondary" data-nova-user-control="true" onClick={verifyIdentity}>Volver a comprobar acceso</button></div>}
          <div hidden={checking}><AccountLibraries key={boot.user.id} user={boot.user} options={boot.options} service={service} onExit={reload} theme={theme} onTheme={() => setTheme(theme === 'dark' ? 'light' : 'dark')} /></div>
        </>
          : <div className="accounts-entry">
            <section className="accounts-intro"><span className="eyebrow">QUICKPROMPT, CONTIGO</span><h1>Un espacio propio para tus instrucciones.</h1><p>Entra para crear tus bibliotecas privadas y conservar lo que funciona.</p><div className="accounts-assurance"><AppIcon glyph={ShieldCheck} size={24} /><p>Tu cuenta identifica tu espacio.<br />Tú decides qué instrucciones guardar.</p></div></section>
            <AccountForm service={service} options={boot.options} csrf={boot.csrf} onLogin={reload} />
          </div>}
    </main>
    <div ref={setDialogHost} />
    <div ref={setOverlayHost} hidden={checking} />
    <WorkspaceFooter privacyLabel="Bibliotecas privadas de tu cuenta" />
  </div></UiPortalScopeProvider>;
}

function AccountLibraries({ user, options, service, onExit, theme, onTheme }: {
  user: AccountUser; options: AccountOptions; service: AccountService; onExit: () => void; theme: string; onTheme: () => void;
}) {
  const [libraries, setLibraries] = useState<AccountLibrary[]>();
  const [name, setName] = useState('');
  const [selected, setSelected] = useState('');
  const [view, setView] = useState<AccountWorkspaceView>(options.delegation && window.location.pathname === '/connect/verify' ? 'connections' : 'libraries');
  const snapshot = useMemo(() => ({ id: user.id, service }), [user.id, service]);
  const operation = useAccountOperation(snapshot, onExit);
  useEffect(() => {
    const controller = new AbortController();
    service.libraries(controller.signal).then(result => { if (!controller.signal.aborted) setLibraries(result); })
      .catch(failure => { if (!controller.signal.aborted) operation.fail(failure); });
    return () => controller.abort();
  }, [service, user.id, operation.fail]);
  async function act(kind: 'create' | 'logout') {
    if (kind === 'logout') await operation.run(signal => service.logout(signal), onExit);
    else await operation.run(signal => service.createLibrary(name, signal), library => {
      setLibraries(items => [...(items ?? []), library]); setName('');
    }, 'Biblioteca creada en tu cuenta.');
  }
  const active = libraries?.find(item => item.id === selected);
  return <section className="account-libraries">
    <AccountWorkspaceSidebar view={view} theme={theme} busy={operation.busy} delegation={options.delegation}
      onHome={() => { setSelected(''); setView('libraries'); }} onView={setView} onTheme={onTheme} onLogout={() => void act('logout')} />
    <div className="account-welcome"><div><span className="eyebrow">TU ESPACIO</span><h1>{view === 'preferences' ? 'Mis preferencias' : view === 'invitations' ? 'Invitaciones' : view === 'connections' ? 'Conexiones locales' : 'Mis bibliotecas'}</h1><p>{user.email}</p></div></div>
    <p className="accounts-stage-note">Tus prompts son privados hasta que publicas una versión e invitas a otra cuenta. Con un equipo autorizado, NOVA prepara contenido y tú revisas cada guardado.</p>
    {operation.error && <p role="alert" className="error">{operation.error}</p>}{operation.notice && <p role="status">{operation.notice}</p>}
    {view === 'preferences' ? <NovaPreferenceEditor client={service.preferences} savedMessage="Preferencias confirmadas en tu cuenta." onExpired={onExit} /> : view === 'connections' ? <AccountConnections service={service} onExpired={onExit} /> : view === 'invitations' ? <AccountInvitations libraries={libraries ?? []} service={service} onExpired={onExit} onImported={libraryId => { setSelected(libraryId); setView('libraries'); }} /> : active ? <>
      <button className="text-button" onClick={() => setSelected('')}><AppIcon glyph={ArrowLeft} />Volver a mis bibliotecas</button>
      <AccountPromptLibrary key={`${user.id}:${active.id}`} library={active} service={service} onExpired={onExit} agentAccountId={options.delegation ? user.id : undefined} />
    </> : <><form className="account-new-library" onSubmit={event => { event.preventDefault(); void act('create'); }}>
      <div><label htmlFor="account-library-name">Nombre de tu biblioteca</label><input id="account-library-name" value={name} onChange={event => setName(event.target.value)} required maxLength={160} disabled={operation.busy} autoComplete="off" /></div>
      <button className="primary" disabled={operation.busy || !name.trim()}>{operation.busy ? <InlineSpinner /> : <AppIcon glyph={Plus} />}Crear biblioteca</button>
    </form>
    {!libraries ? !operation.error && <p role="status">Cargando bibliotecas…</p> : libraries.length ? <ul className="account-library-list">{libraries.map(library => <li key={library.id}><AppIcon glyph={LockKeyhole} size={20} /><div><h2>{library.name}</h2><span>Biblioteca privada</span></div><button className="secondary" aria-label={`Abrir biblioteca ${library.name}`} onClick={() => setSelected(library.id)}>Abrir<AppIcon glyph={ArrowRight} size={16} /></button></li>)}</ul> : <div className="empty-state"><h2>Tu primera biblioteca empieza aquí.</h2><p>Ponle un nombre para crear tu espacio.</p></div>}
    </>}
  </section>;
}
