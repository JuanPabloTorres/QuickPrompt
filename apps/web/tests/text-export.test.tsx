import { Blob as NodeBlob } from 'node:buffer';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PreparedUse } from '../src/features/nova/PreparedUse';
import type { PreparedDraft } from '../src/services/preparation';
import { downloadPreparedText } from '../src/services/textExport';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); });

it('downloads BOM-free UTF-8 with LF, preserving Unicode, whitespace and final empty lines', async () => {
  vi.useFakeTimers();
  vi.stubGlobal('Blob', NodeBlob);
  const create = vi.fn((_blob: NodeBlob) => 'blob:owned-export'), revoke = vi.fn();
  vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: revoke });
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  downloadPreparedText('  Ω · 🚀\r\n\r\nA\rB\n\n');
  const blob = create.mock.calls[0][0] as unknown as NodeBlob;
  expect([...new Uint8Array(await blob.arrayBuffer())]).toEqual([...new TextEncoder().encode('  Ω · 🚀\n\nA\nB\n\n')]);
  expect(click.mock.instances[0]).toHaveProperty('download', 'quickprompt.txt');
  expect(click.mock.instances[0]).toHaveProperty('href', 'blob:owned-export');
  expect(revoke).not.toHaveBeenCalled();
  vi.advanceTimersByTime(1000);
  expect(revoke).toHaveBeenCalledWith('blob:owned-export');
});

it('exports a prepared draft without clipboard, model calls, usage or saving', async () => {
  vi.stubGlobal('Blob', NodeBlob);
  const create = vi.fn((_blob: NodeBlob) => 'blob:owned-export');
  vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: vi.fn() });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  const copy = vi.fn(), open = vi.spyOn(window, 'open');
  vi.stubGlobal('navigator', { clipboard: { writeText: copy } });
  const request = vi.fn(async (_url: string, _options?: RequestInit) => Response.json([]));
  vi.stubGlobal('fetch', request);
  render(<PreparedUse draft={{ id: 'draft', compiledText: 'Ω\n🚀\n\n' } as PreparedDraft} onClose={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Descargar texto (.txt)' }));
  expect(create).toHaveBeenCalledOnce();
  expect(copy).not.toHaveBeenCalled(); expect(open).not.toHaveBeenCalled();
  expect(request).toHaveBeenCalledOnce(); // Catalog GET only.
  expect(String(request.mock.calls[0][0])).toContain('/providers');
  expect(request.mock.calls[0][1]?.method ?? 'GET').toBe('GET');
});
