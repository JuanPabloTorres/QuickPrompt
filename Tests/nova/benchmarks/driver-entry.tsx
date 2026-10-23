// QA entry only. Imports the actual application, controlled fields and UI driver.
// It is served by the isolated benchmark; never included in the production build.
import { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import App from '../../../apps/web/src/app/App';
import { AgentUiDriver, type CursorTarget, type UiAction } from '../../../apps/web/src/features/nova/AgentUiDriver';
import { NovaAgentCursor } from '../../../apps/web/src/features/nova/NovaAgentCursor';
import '../../../apps/web/src/design/tokens.css';
import '../../../apps/web/src/design/canvas.css';

declare global { interface Window { novaMeasureUi?: (speed: 'normal' | 'fast', body: string) => Promise<{ durationMs: number; exactText: boolean; inputEvents: number; cursorTargets: number }> } }
function Harness() {
  const [target, setTarget] = useState<CursorTarget | null>(null);
  useEffect(() => {
    window.novaMeasureUi = async (speed, body) => {
      let inputEvents = 0, cursorTargets = 0;
      const observe = (event: Event) => { if ((event.target as HTMLElement).id === 'prompt-content') inputEvents++; };
      document.addEventListener('input', observe);
      const driver = new AgentUiDriver(next => { if (next) cursorTargets++; setTarget(next); }); driver.speed = speed;
      const start = performance.now();
      try {
        const state = await driver.execute({ runId: crypto.randomUUID(), actionId: crypto.randomUUID(), sequence: 1, tool: 'ui_prepare_creation', target: null, arguments: { title: 'Medición de escritura', templateText: body }, timestamp: new Date().toISOString(), executionStatus: 'DISPATCHED', verificationStatus: 'PENDING' } satisfies UiAction);
        return { durationMs: performance.now() - start, exactText: state.templateText === body && state.title === 'Medición de escritura', inputEvents, cursorTargets };
      } finally { document.removeEventListener('input', observe); }
    };
    return () => { delete window.novaMeasureUi; };
  }, []);
  return <><App /><NovaAgentCursor target={target} /></>;
}
createRoot(document.getElementById('root')!).render(<Harness />);
