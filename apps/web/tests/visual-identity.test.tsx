import { useState } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Dialog } from '../src/components/ui/Dialog';
import { useInterfacePreferences } from '../src/hooks/useInterfacePreferences';
import { visualState } from '../src/design/nova-state';
import { useMotionPreference } from '../src/hooks/useMotionPreference';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); localStorage.clear(); delete document.documentElement.dataset.motion; });
it('responds to a changed OS motion preference without reloading the app', () => {
  let reduced = false;
  const query = new EventTarget(); Object.defineProperty(query, 'matches', { get: () => reduced });
  vi.stubGlobal('matchMedia', () => query);
  function Example() { const reduced = useMotionPreference(true); return <output>{reduced ? 'Estable' : 'Animado'}</output>; }
  render(<Example />); expect(screen.getByText('Animado')).toBeTruthy();
  act(() => { reduced = true; query.dispatchEvent(new Event('change')); });
  expect(screen.getByText('Estable')).toBeTruthy();
});
it('returns focus to the actual opener of a controlled dialog without a Radix trigger', async () => {
  function Example() { const [open, setOpen] = useState(false); return <><button onClick={() => setOpen(true)}>Elegir destino</button><Dialog open={open} onOpenChange={setOpen} title="Destino" description="Elige dónde usarlo."><button>Copiar</button></Dialog></>; }
  render(<Example />);
  const opener = screen.getByRole('button', { name: 'Elegir destino' }); opener.focus(); fireEvent.click(opener);
  await screen.findByRole('dialog'); fireEvent.click(screen.getByRole('button', { name: 'Cerrar diálogo' }));
  await waitFor(() => expect(document.activeElement).toBe(opener));
});
it('migrates existing settings without losing view, speed, mode or draft consent', async () => {
  localStorage.setItem('nova-interface-v1', JSON.stringify({ view: 'rows', executionMode: 'LIVE', rememberDraft: true, agentSpeed: 'fast' }));
  function Example() { const [value, change] = useInterfacePreferences(); return <><output>{JSON.stringify(value)}</output><button onClick={() => change({ ...value, decorativeMotion: false })}>Reducir movimiento</button></>; }
  render(<Example />); fireEvent.click(screen.getByRole('button'));
  await waitFor(() => expect(document.documentElement.dataset.motion).toBe('reduced'));
  expect(JSON.parse(localStorage.getItem('nova-interface-v1')!)).toEqual({ view: 'rows', executionMode: 'LIVE', rememberDraft: true, agentSpeed: 'fast', decorativeMotion: false });
});
it('does not depict work after cancellation, pause or completion even with a preceding generation event', () => {
  expect(visualState('PAUSED', 'Generation', 'ui_execute_local')).toBe('idle');
  expect(visualState('TOOL_RUNNING', 'Generation', 'ui_execute_local', true)).toBe('idle');
  expect(visualState('CANCELLED', 'Generation')).toBe('idle');
  expect(visualState('COMPLETED', 'Generation')).toBe('ready');
  expect(visualState('FAILED', 'Generation')).toBe('error');
  expect(visualState('VERIFYING', 'Generation')).toBe('validating');
  expect(visualState('TOOL_RUNNING', 'Generation')).toBe('generating');
  expect(visualState('TOOL_RUNNING', 'UiAction', 'ui_search_prompt')).toBe('searching');
  expect(visualState('TOOL_RUNNING', 'UiAction', 'ui_open_prompt')).toBe('navigating');
});
