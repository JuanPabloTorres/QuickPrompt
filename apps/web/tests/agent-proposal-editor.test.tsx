import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AgentUiDriver } from '../src/features/nova/AgentUiDriver';
import { AgentUiProvider, useAgentProposalSession } from '../src/features/nova/AgentUiContext';
import { createAgentProposalSession, type AgentProposalPort } from '../src/features/nova/AgentProposalSession';
import { EditorPreparation } from '../src/features/prompts/EditorPreparation';
import type { Prompt, PromptDraft } from '../src/services/api';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
const source: Prompt = { id: '10000000-0000-4000-8000-000000000001', revision: 1, title: 'Procedimiento', description: '', categoryId: 0,
  templateText: 'Explica el procedimiento.', variableNames: [], defaults: {}, isFavorite: false, isArchived: false,
  createdAt: '2026-10-20T12:00:00Z', updatedAt: '2026-10-20T12:00:00Z', uses: 0, lastUsedAt: null };
const proposal = 'Resume las notas con claridad 🚀.';

function Editor({ manual }: { manual: (draft: PromptDraft) => void }) {
  const [draft, setDraft] = useState<PromptDraft>(source);
  return <section data-testid="prompt-editor" data-prompt-id={source.id} data-revision={source.revision}>
    <input id="prompt-title" aria-label="Título" value={draft.title} onChange={event => setDraft(value => ({ ...value, title: event.target.value }))} />
    <textarea id="prompt-content" aria-label="Contenido" value={draft.templateText} onChange={event => setDraft(value => ({ ...value, templateText: event.target.value }))} />
    <EditorPreparation source={source} draft={draft} values={{}} onChange={value => { manual(value); setDraft(value); }} onValues={vi.fn()} onUse={vi.fn()} />
  </section>;
}

it.each([false, true])('editor proposal uses scoped real driver when available, manual=%s', async agent => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([{ top: 0, left: 0, width: 100, height: 30 }] as unknown as DOMRectList);
  HTMLElement.prototype.scrollIntoView = vi.fn(); vi.stubGlobal('matchMedia', () => ({ matches: true }));
  vi.stubGlobal('fetch', vi.fn(async (url: string, options?: RequestInit) => {
    if (url.endsWith('/inspect')) return Response.json({ variableNames: [] });
    if (url.endsWith('/compile')) return Response.json({ text: JSON.parse(options!.body as string).templateText, missingVariables: [] });
    if (url.endsWith('/nova/propose')) return Response.json({ templateId: source.id, baseRevision: source.revision, text: proposal, reasons: [], changes: [], warnings: [], model: 'qwen3:8b' });
    throw new Error('Unexpected route ' + url);
  }));
  const session = createAgentProposalSession(), manual = vi.fn(), target = vi.fn(), driver = new AgentUiDriver(target); driver.speed = 'fast';
  const release = agent ? session.register(driver) : () => {};
  const writing = vi.spyOn(driver, 'proposal');
  render(agent ? <AgentUiProvider session={session}><Editor manual={manual} /></AgentUiProvider> : <Editor manual={manual} />);
  fireEvent.change(screen.getByLabelText('Cambio que necesitas'), { target: { value: 'Hazlo más breve.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Preparar cambio en el editor' }));
  await waitFor(() => expect(screen.getByLabelText('Contenido')).toHaveProperty('value', proposal));
  if (agent) { expect(writing).toHaveBeenCalledWith(proposal, expect.any(AbortSignal)); expect(manual).not.toHaveBeenCalled(); expect(target).toHaveBeenCalled(); }
  else { expect(writing).not.toHaveBeenCalled(); expect(manual).toHaveBeenCalledOnce(); }
  release(); expect(await session.apply('Fuera de sesión.', new AbortController().signal)).toBe(false);
});

it('providers allocate independent proposal owners per mount', async () => {
  const ports: AgentProposalPort[] = [];
  function Read() { const session = useAgentProposalSession(); if (session && !ports.includes(session)) ports.push(session); return null; }
  render(<><AgentUiProvider><Read /></AgentUiProvider><AgentUiProvider><Read /></AgentUiProvider></>);
  expect(ports).toHaveLength(2); expect(ports[0]).not.toBe(ports[1]);
  const writer = { proposal: vi.fn(async () => {}) }; ports[0].register(writer);
  expect(await ports[1].apply('Otra aplicación.', new AbortController().signal)).toBe(false); expect(writer.proposal).not.toHaveBeenCalled();
});
