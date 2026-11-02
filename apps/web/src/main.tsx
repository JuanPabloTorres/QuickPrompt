import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ErrorState } from './components/ui/Feedback';
import { AccountPage } from './features/accounts/AccountPage';
import { createAccountService } from './services/accounts';
import './design/tokens.css';
import './design/canvas.css';
import './design/agent-presence.css';
import './design/library.css';
import './design/prompt-detail.css';
import './design/workspace-shell.css';
import './design/accounts.css';

document.addEventListener('visibilitychange', () => { document.documentElement.dataset.inactive = String(document.hidden); });
const root = createRoot(document.getElementById('root')!);
if (document.querySelector('meta[name="quickprompt-mode"]')?.getAttribute('content') === 'accounts') {
  const accountService = createAccountService();
  root.render(<StrictMode><AccountPage service={accountService} /></StrictMode>);
} else {
  root.render(<p role="status">Preparando QuickPrompt…</p>);
  void Promise.all([import('./app/App'), import('./components/ServiceContext'), import('./services/application')])
    .then(([{ default: App }, { ApplicationServiceProvider }, { createApplicationServices }]) => {
      const services = createApplicationServices();
      root.render(<StrictMode><ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider></StrictMode>);
    }).catch(() => root.render(<ErrorState title="No se pudo abrir QuickPrompt" message="No se cargaron los componentes de la aplicación."><button className="secondary" onClick={() => window.location.reload()}>Reintentar</button></ErrorState>));
}
