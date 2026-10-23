import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './app/App';
import { ApplicationServiceProvider } from './components/ServiceContext';
import { createApplicationServices } from './services/application';
import './design/tokens.css';
import './design/canvas.css';
import './design/agent-presence.css';
import './design/library.css';
import './design/prompt-detail.css';
import './design/workspace-shell.css';

document.addEventListener('visibilitychange', () => { document.documentElement.dataset.inactive = String(document.hidden); });
const services = createApplicationServices();
createRoot(document.getElementById('root')!).render(<StrictMode><ApplicationServiceProvider services={services}><App /></ApplicationServiceProvider></StrictMode>);
