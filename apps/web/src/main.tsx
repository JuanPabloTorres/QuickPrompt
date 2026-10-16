import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './app/App';
import './design/tokens.css';
import './design/canvas.css';

document.addEventListener('visibilitychange', () => { document.documentElement.dataset.inactive = String(document.hidden); });
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
