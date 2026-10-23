import { expect, it, vi } from 'vitest';
import { createAgentProposalSession as makeSession } from '../src/features/nova/AgentProposalSession';

it('an old release cannot revoke a newer writer in the same session', async () => {
  const session = makeSession(), first = { proposal: vi.fn(async () => {}) }, second = { proposal: vi.fn(async () => {}) };
  const releaseFirst = session.register(first); const releaseSecond = session.register(second);
  releaseFirst(); releaseFirst();
  expect(await session.apply('Contenido de la sesión.', new AbortController().signal)).toBe(true);
  expect(second.proposal).toHaveBeenCalledOnce(); expect(first.proposal).not.toHaveBeenCalled();
  releaseSecond(); expect(await session.apply('Sin escritor.', new AbortController().signal)).toBe(false);
});

it('independent sessions do not send proposals to another session writer', async () => {
  const one = makeSession(), two = makeSession(), first = { proposal: vi.fn(async () => {}) }, second = { proposal: vi.fn(async () => {}) };
  const releaseFirst = one.register(first); const releaseSecond = two.register(second); const signal = new AbortController().signal;
  expect(await one.apply('Uno.', signal)).toBe(true); expect(first.proposal).toHaveBeenCalledWith('Uno.', signal);
  expect(second.proposal).not.toHaveBeenCalled(); releaseFirst();
  expect(await two.apply('Dos.', signal)).toBe(true); expect(second.proposal).toHaveBeenCalledWith('Dos.', signal); releaseSecond();
});

it('a pending proposal cannot acknowledge success after its writer was replaced', async () => {
  const session = makeSession(); let finish!: () => void;
  session.register({ proposal: () => new Promise<void>(resolve => { finish = resolve; }) });
  const pending = session.apply('Propuesta pendiente.', new AbortController().signal);
  const rejection = expect(pending).rejects.toThrow('UI_CANCELLED');
  const release = session.register({ proposal: vi.fn(async () => {}) }); finish(); await rejection; release();
});

it('an absent writer preserves the manual path and a released writer stays absent', async () => {
  const session = makeSession(), signal = new AbortController().signal;
  expect(await session.apply('Manual.', signal)).toBe(false);
  const writer = { proposal: vi.fn(async () => {}) }; const release = session.register(writer);
  expect(await session.apply('NOVA.', signal)).toBe(true); expect(writer.proposal).toHaveBeenCalledWith('NOVA.', signal);
  release(); expect(await session.apply('Manual otra vez.', signal)).toBe(false);
});

it('cancellation prevents a new write and prevents a late successful acknowledgement', async () => {
  const session = makeSession(), controller = new AbortController(), writer = { proposal: vi.fn(async () => {}) };
  session.register(writer); controller.abort();
  await expect(session.apply('Cancelado.', controller.signal)).rejects.toThrow('UI_CANCELLED'); expect(writer.proposal).not.toHaveBeenCalled();
  let finish!: () => void; const running = new AbortController();
  session.register({ proposal: () => new Promise<void>(resolve => { finish = resolve; }) });
  const pending = session.apply('En curso.', running.signal), rejection = expect(pending).rejects.toThrow('UI_CANCELLED');
  running.abort(); finish(); await rejection;
});

it('preserves the writer error instead of declaring manual fallback or success', async () => {
  const session = makeSession(), error = new Error('UI_VALUE_MISMATCH');
  session.register({ proposal: async () => { throw error; } });
  await expect(session.apply('Contenido.', new AbortController().signal)).rejects.toBe(error);
});
