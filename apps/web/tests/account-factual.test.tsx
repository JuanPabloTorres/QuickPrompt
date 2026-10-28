import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AccountFactualReviewDialog } from '../src/features/accounts/AccountFactualReview';
import { AccountFactualDocuments } from '../src/features/accounts/AccountFactualDocuments';
import { UseAccountPromptDialog } from '../src/features/accounts/AccountExecutions';
import { createAccountService, type AccountPrompt } from '../src/services/accounts';
import { createApiClient } from '../src/services/api';
import type { FactualDocument, FactualReview } from '../src/services/accountFactual';

afterEach(() => { cleanup(); vi.useRealTimers(); });
const libraryId = '70000000-0000-4000-8000-000000000001', promptId = '70000000-0000-4000-8000-000000000002', connectionId = '70000000-0000-4000-8000-000000000003', reviewId = '70000000-0000-4000-8000-000000000004', documentId = '70000000-0000-4000-8000-000000000005';
const root = `/api/accounts/v1/libraries/${libraryId}`;
const prompt: AccountPrompt = { id: promptId, libraryId, revision: 3, title: 'Acta', template: 'Transforma <datos> con estilo <estilo>.' };
const original = { role: 'Propuesta de fecha', text: 'Fecha tentativa', sourceField: 'datos', quote: 'lunes' };
const source = 'Se propuso el lunes; no se aprobó una fecha.';
function fixture(status: FactualReview['status'] = 'REVIEW') {
  const calls: { path: string; method: string; body?: Record<string, unknown>; signal?: AbortSignal | null }[] = [];
  let failConfirm = 0, loseApproval = false, wrongLibrary = false, deleted = false, rateLimitPoll = false;
  let storedSources = { datos: source };
  let review: FactualReview = { id: reviewId, libraryId, promptId, promptRevision: 3, connectionId, version: 1, status, title: 'Acta revisada', sourceFields: { datos: source }, proposalFacts: [original], confirmedFacts: [], preview: null, documentId: null, edited: false, expiresAt: '2099-10-28T00:00:00Z', errorCode: null };
  const document = (): FactualDocument => ({ id: documentId, libraryId: wrongLibrary ? promptId : libraryId, reviewId, promptId, promptRevision: 3, title: review.title, text: review.preview ?? 'Texto aprobado literal.', facts: review.confirmedFacts, proposalFacts: review.proposalFacts, sourceFields: storedSources, edited: true, createdAt: '2026-10-28T00:00:00Z' });
  const service = createAccountService(createApiClient({ send: async (path, options) => {
    const method = options?.method ?? 'GET', body = options?.body ? JSON.parse(String(options.body)) as Record<string, unknown> : undefined;
    calls.push({ path, method, body, signal: options?.signal });
    if (path === '/account/csrf') return Response.json({ token: 'fixture' });
    if (path === '/account/options') return Response.json({ email: true, google: true, delegation: false, mailDelivery: 'fixture' });
    if (path === '/account/connections/') return Response.json([{ id: connectionId, label: 'Mi NOVA', profileId: 'ollama-qwen3-8b-proposals', active: true, revoked: false, createdAt: '', expiresAt: '2099-10-28T00:00:00Z' }]);
    if (path.endsWith('/render')) return Response.json({ promptId, templateRevision: 3, text: null, missingVariables: ['datos', 'estilo'] });
    if (path === `${root}/factual-reviews`) return Response.json(review);
    if (path === `${root}/factual-reviews/${reviewId}/confirm`) {
      if (failConfirm-- > 0) return Response.json({ detail: 'La conexión fue revocada.' }, { status: 409 });
      const facts = body!.facts as FactualReview['confirmedFacts'];
      review = { ...review, version: review.version + 1, status: 'COMPOSED', title: String(body!.title), confirmedFacts: facts, preview: review.version === 1 ? '# Acta\n\nFecha propuesta: lunes.\nNo hay aprobación.' : `# Acta\n\nCorrección confirmada: ${facts[0].text}` }; return Response.json(review);
    }
    if (path === `${root}/factual-reviews/${reviewId}/approve`) { review = { ...review, version: 3, status: 'APPROVED', documentId }; if (loseApproval) throw new Error('Respuesta de aprobación perdida.'); return Response.json(document()); }
    if (path === `${root}/factual-reviews/${reviewId}/cancel`) { review = { ...review, version: review.version + 1, status: 'CANCELLED' }; return Response.json(review); }
    if (path === `${root}/factual-reviews/${reviewId}`) {
      if (rateLimitPoll) { rateLimitPoll = false; return Response.json({ detail: 'Demasiadas consultas. Espera antes de consultar.' }, { status: 429 }); }
      return Response.json(review.status === 'APPROVED' ? { ...review, title: '', sourceFields: {}, proposalFacts: [], confirmedFacts: [], preview: null } : review);
    }
    if (path.startsWith(`${root}/factual-documents?`)) return Response.json({ items: deleted ? [] : [document()], total: deleted ? 0 : 1, page: 0, limit: 20 });
    if (path === `${root}/factual-documents/${documentId}` && method === 'DELETE') { deleted = true; return new Response(null, { status: 204 }); }
    if (path === `${root}/factual-documents/${documentId}`) return Response.json(document());
    throw new Error(`Unexpected factual fixture request: ${method} ${path}`);
  } }, ''));
  return { service, calls, failConfirm: () => { failConfirm = 1; }, loseApproval: () => { loseApproval = true; }, wrongLibrary: () => { wrongLibrary = true; }, rateLimitPoll: () => { rateLimitPoll = true; }, invalidProposalQuote: () => { review = { ...review, proposalFacts: [{ ...original, quote: 'lunez' }] }; }, noFacts: () => { review = { ...review, proposalFacts: [] }; }, expire: () => { review = { ...review, expiresAt: '2000-01-01T00:00:00Z' }; },
    historyWithoutGrounding: (text: string) => { storedSources = { datos: text }; const fact = { role: 'Fecha', text: 'No se aprobó la fecha.', sourceField: '', quote: '' }; review = { ...review, proposalFacts: [fact], confirmedFacts: [fact] }; } };
}
async function begin(fake: ReturnType<typeof fixture>) {
  render(<AccountFactualReviewDialog prompt={prompt} names={['datos', 'estilo']} initialValues={{ datos: source, estilo: 'claro' }} service={fake.service} onExpired={vi.fn()} onClose={vi.fn()} />);
  await screen.findByRole('option', { name: 'Mi NOVA' });
  fireEvent.change(screen.getByLabelText('Conexión de NOVA'), { target: { value: connectionId } });
  fireEvent.change(screen.getByLabelText('Título del documento'), { target: { value: 'Acta revisada' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'datos' }));
  fireEvent.click(screen.getByRole('button', { name: 'Solicitar propuesta de hechos' }));
  await screen.findByRole('heading', { name: 'Revisa los hechos propuestos' });
}

it('uses an explicit capability and two independent approvals; preserves literal human roles and exact preview', async () => {
  const fake = fixture(); await begin(fake);
  expect(fake.calls.find(call => call.path === `${root}/factual-reviews`)?.body).toMatchObject({ promptId, revision: 3, connectionId, sourceFields: ['datos'], values: { datos: source, estilo: 'claro' } });
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  fireEvent.change(screen.getByLabelText('Rol del hecho 1'), { target: { value: 'Propuesta sin aprobación' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.change(screen.getByLabelText('Texto del hecho 1'), { target: { value: 'Sólo se propuso una fecha.' } });
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  const preview = await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  expect(within(preview).getByText('Se guardarán el documento, los hechos y las fuentes seleccionadas en tu historial hasta que los elimines.')).toBeTruthy();
  expect(preview.querySelector('pre')?.textContent).toBe('# Acta\n\nFecha propuesta: lunes.\nNo hay aprobación.');
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
  expect(screen.getByRole('button', { name: 'Guardar documento aprobado' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Apruebo guardar este texto exacto en mi historial de documentos.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Guardar documento aprobado' }));
  await screen.findByText('Documento aprobado y guardado. Lo encontrarás en Documentos aprobados de esta biblioteca.');
  expect(fake.calls.find(call => call.path.endsWith('/confirm'))?.body).toMatchObject({ version: 1, confirmed: true, facts: [{ ...original, role: 'Propuesta sin aprobación', text: 'Sólo se propuso una fecha.' }] });
  expect(fake.calls.find(call => call.path.endsWith('/approve'))?.body).toEqual({ version: 2, confirmed: true });
  expect(fake.calls.filter(call => /compile|run-local|\/use$/.test(call.path))).toHaveLength(0);
});

it('a literal quote does not bypass the human decision and invalid quotes cannot be confirmed', async () => {
  const fake = fixture(); await begin(fake);
  fireEvent.change(screen.getByLabelText('Extracto literal del hecho 1'), { target: { value: 'se aprobó el lunes' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  expect(screen.getByRole('alert').textContent).toContain('no coincide literalmente');
  expect(fake.calls.some(call => call.path.endsWith('/confirm'))).toBe(false);
});

it('shows a nonliteral quote from the model proposal and permits human correction without bypassing either approval', async () => {
  const fake = fixture(); fake.invalidProposalQuote(); await begin(fake);
  expect(screen.getByLabelText('Extracto literal del hecho 1')).toHaveProperty('value', 'lunez');
  expect(screen.getByRole('alert').textContent).toContain('El extracto no coincide literalmente');
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  expect(fake.calls.some(call => call.path.endsWith('/confirm') || call.path.endsWith('/approve'))).toBe(false);
  fireEvent.change(screen.getByLabelText('Extracto literal del hecho 1'), { target: { value: 'lunes' } });
  expect(screen.queryByText('El extracto no coincide literalmente con el campo seleccionado.')).toBeNull();
  expect(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' })).toHaveProperty('checked', false);
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  expect(fake.calls.find(call => call.path.endsWith('/confirm'))?.body?.facts).toEqual([{ ...original, quote: 'lunes' }]);
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
  expect(screen.getByRole('button', { name: 'Guardar documento aprobado' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Apruebo guardar este texto exacto en mi historial de documentos.' }));
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
  fireEvent.click(screen.getByRole('button', { name: 'Guardar documento aprobado' }));
  await screen.findByText('Documento aprobado y guardado. Lo encontrarás en Documentos aprobados de esta biblioteca.');
  expect(fake.calls.filter(call => call.path === `${root}/factual-reviews`)).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/approve'))).toHaveLength(1);
});

it('reports revocation without saving and can consult authoritative status; cancellation is explicit', async () => {
  const fake = fixture(); fake.failConfirm(); await begin(fake);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  await screen.findByText(/La conexión fue revocada/);
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
  fireEvent.click(screen.getByRole('button', { name: 'Cancelar revisión' }));
  await screen.findByText('Revisión cancelada. No se guardó un documento.');
});

it('expired reviews cannot confirm even after the checkbox is selected', async () => {
  const fake = fixture(); fake.expire(); await begin(fake);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  expect(fake.calls.some(call => call.path.endsWith('/confirm'))).toBe(false);
});

it('a lost approval response requires an authoritative read and does not repeat the write', async () => {
  const fake = fixture(); fake.loseApproval(); await begin(fake);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Apruebo guardar este texto exacto en mi historial de documentos.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Guardar documento aprobado' }));
  await screen.findByText(/Respuesta de aprobación perdida/);
  expect(screen.getByRole('button', { name: 'Guardar documento aprobado' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Consultar estado actual' }));
  await screen.findByText('Documento aprobado y guardado. Lo encontrarás en Documentos aprobados de esta biblioteca.');
  expect(fake.calls.filter(call => call.path.endsWith('/approve'))).toHaveLength(1);
});

it('corrects confirmed facts from the preview without another proposal and requires approval of the new revision', async () => {
  const fake = fixture(); await begin(fake);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Apruebo guardar este texto exacto en mi historial de documentos.' }));
  const staleApprove = screen.getByRole('button', { name: 'Guardar documento aprobado' });
  fireEvent.click(screen.getByRole('button', { name: 'Corregir hechos' }));
  expect(staleApprove.isConnected).toBe(false); fireEvent.click(staleApprove);
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
  expect(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' })).toHaveProperty('checked', false);
  expect(screen.getByLabelText('Texto del hecho 1')).toHaveProperty('value', original.text);
  fireEvent.change(screen.getByLabelText('Texto del hecho 1'), { target: { value: 'La fecha sigue pendiente de aprobación.' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  const preview = await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  expect(preview.querySelector('pre')?.textContent).toBe('# Acta\n\nCorrección confirmada: La fecha sigue pendiente de aprobación.');
  expect(screen.getByRole('button', { name: 'Guardar documento aprobado' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Apruebo guardar este texto exacto en mi historial de documentos.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Guardar documento aprobado' }));
  await screen.findByText('Documento aprobado y guardado. Lo encontrarás en Documentos aprobados de esta biblioteca.');
  expect(fake.calls.filter(call => call.path === `${root}/factual-reviews`)).toHaveLength(1);
  expect(fake.calls.filter(call => call.path.endsWith('/confirm')).map(call => call.body?.version)).toEqual([1, 2]);
  expect(fake.calls.find(call => call.path.endsWith('/approve'))?.body).toEqual({ version: 3, confirmed: true });
});

it('history reads approved snapshots without a model call or mutation and requires explicit deletion', async () => {
  const fake = fixture();
  render(<AccountFactualDocuments libraryId={libraryId} service={fake.service} onExpired={vi.fn()} />);
  expect(fake.calls).toHaveLength(0);
  fireEvent.click(screen.getByRole('button', { name: 'Ver documentos aprobados' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir documento Acta revisada' }));
  const dialog = await screen.findByRole('dialog'); await within(dialog).findByText('Texto aprobado literal.');
  fireEvent.click(within(dialog).getByText('Fuentes seleccionadas conservadas'));
  expect(within(dialog).getByRole('region', { name: 'Fuente guardada datos' }).querySelector('pre')?.textContent).toBe(source);
  expect(fake.calls.every(call => call.method === 'GET')).toBe(true);
  fireEvent.click(within(dialog).getByRole('button', { name: 'Eliminar documento' }));
  expect(within(dialog).getByRole('button', { name: 'Eliminar documento definitivamente' })).toHaveProperty('disabled', true);
  fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Confirmo eliminar sólo este documento aprobado.' }));
  fireEvent.click(within(dialog).getByRole('button', { name: 'Eliminar documento definitivamente' }));
  await screen.findByText('Aún no hay documentos aprobados en esta página.');
  expect(fake.calls.find(call => call.method === 'DELETE')?.body).toEqual({ confirmed: true });
});

it('rejects a document from another library at the service boundary', async () => {
  const fake = fixture(); fake.wrongLibrary();
  await expect(fake.service.factual(libraryId).document(documentId)).rejects.toThrow('no corresponde');
  await expect(fake.service.factual(libraryId).documents()).rejects.toThrow('no corresponde');
});

it('does not attribute an ungrounded model proposal to a human and reads escaped original sources without another model call', async () => {
  const fake = fixture(), literal = '<script>window.secret = true;</script>\nFuente original áé'; fake.historyWithoutGrounding(literal);
  render(<AccountFactualDocuments libraryId={libraryId} service={fake.service} onExpired={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Ver documentos aprobados' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Abrir documento Acta revisada' }));
  const dialog = await screen.findByRole('dialog'); await within(dialog).findByText('Texto aprobado literal.');
  fireEvent.click(within(dialog).getByText('Hechos confirmados y trazabilidad'));
  expect(within(dialog).getByText('Sin fuente documental; confirmado en revisión.')).toBeTruthy();
  fireEvent.click(within(dialog).getByText('Propuesta original de NOVA · IA'));
  expect(within(dialog).getByText('Sin fuente documental en la propuesta de IA.')).toBeTruthy();
  expect(within(dialog).queryByText(/Declaración manual|declaración que añades tú/)).toBeNull();
  fireEvent.click(within(dialog).getByText('Fuentes seleccionadas conservadas'));
  const stored = within(dialog).getByRole('region', { name: 'Fuente guardada datos' });
  expect(stored.querySelector('pre')?.textContent).toBe(literal); expect(stored.querySelector('script')).toBeNull();
  expect(fake.calls.every(call => call.method === 'GET' && call.path.includes('/factual-documents'))).toBe(true);
});

it('rejects an unselected field returned as a source instead of silently exposing it for review', async () => {
  const fake = fixture();
  await expect(fake.service.factual(libraryId).start({ operationId: documentId, promptId, revision: 3, connectionId, values: { datos: source, estilo: 'claro' }, sourceFields: ['estilo'], title: 'Acta revisada' })).rejects.toThrow('campos elegidos');
});

it('mounts the factual capability only when explicitly selected from the real use dialog', async () => {
  const fake = fixture(); render(<UseAccountPromptDialog prompt={prompt} service={fake.service} onExpired={vi.fn()} onClose={vi.fn()} onSubmitted={vi.fn()} />);
  const button = await screen.findByRole('button', { name: 'Transformación factual con revisión' });
  await waitFor(() => expect(button).toHaveProperty('disabled', false));
  expect(fake.calls.some(call => call.path.includes('factual-'))).toBe(false);
  fireEvent.click(button); await screen.findByRole('dialog', { name: 'Transformación factual con revisión' });
  expect(fake.calls.some(call => call.path.includes('factual-'))).toBe(false);
});

it('allows a clearly identified human declaration after an empty proposal, never an empty confirmation', async () => {
  const fake = fixture(); fake.noFacts(); await begin(fake);
  expect(screen.getByText('Sin hechos propuestos: añade y revisa hechos antes de confirmar.')).toBeTruthy();
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  expect(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' })).toHaveProperty('disabled', true);
  fireEvent.click(screen.getByRole('button', { name: 'Añadir hecho revisado' }));
  fireEvent.change(screen.getByLabelText('Rol del hecho 1'), { target: { value: 'Declaración del revisor' } });
  fireEvent.change(screen.getByLabelText('Texto del hecho 1'), { target: { value: 'Todavía no se autorizó una fecha.' } });
  expect(screen.getByText('Sin fuente documental; revisa esta afirmación.')).toBeTruthy();
  fireEvent.click(screen.getByRole('checkbox', { name: 'Revisé los hechos, sus roles y sus extractos de origen.' }));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar hechos y ver documento' }));
  await screen.findByRole('region', { name: 'Documento pendiente de aprobación' });
  expect(fake.calls.find(call => call.path.endsWith('/confirm'))?.body?.facts).toEqual([{ role: 'Declaración del revisor', text: 'Todavía no se autorizó una fecha.', sourceField: '', quote: '' }]);
});

it('aborts a pending proposal subscription when its context unmounts', async () => {
  const fake = fixture('ADMITTED');
  const view = render(<AccountFactualReviewDialog prompt={prompt} names={['datos', 'estilo']} initialValues={{ datos: source, estilo: 'claro' }} service={fake.service} onExpired={vi.fn()} onClose={vi.fn()} />);
  await screen.findByRole('option', { name: 'Mi NOVA' });
  fireEvent.change(screen.getByLabelText('Conexión de NOVA'), { target: { value: connectionId } });
  fireEvent.change(screen.getByLabelText('Título del documento'), { target: { value: 'Acta revisada' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'datos' }));
  fireEvent.click(screen.getByRole('button', { name: 'Solicitar propuesta de hechos' }));
  await screen.findByText(/NOVA está proponiendo hechos/);
  await waitFor(() => expect(fake.calls.some(call => call.path === `${root}/factual-reviews/${reviewId}`)).toBe(true), { timeout: 5000 });
  const read = fake.calls.find(call => call.path === `${root}/factual-reviews/${reviewId}`)!;
  view.unmount(); expect(read.signal?.aborted).toBe(true);
  expect(fake.calls.some(call => call.path.endsWith('/approve'))).toBe(false);
});

it('a polling quota rejection preserves the review, stops automatic retries and permits only explicit status recovery', async () => {
  const fake = fixture('ADMITTED'); fake.rateLimitPoll();
  render(<AccountFactualReviewDialog prompt={prompt} names={['datos', 'estilo']} initialValues={{ datos: source, estilo: 'claro' }} service={fake.service} onExpired={vi.fn()} onClose={vi.fn()} />);
  await screen.findByRole('option', { name: 'Mi NOVA' });
  fireEvent.change(screen.getByLabelText('Conexión de NOVA'), { target: { value: connectionId } });
  fireEvent.change(screen.getByLabelText('Título del documento'), { target: { value: 'Acta revisada' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'datos' }));
  fireEvent.click(screen.getByRole('button', { name: 'Solicitar propuesta de hechos' }));
  await screen.findByText(/Demasiadas consultas/, {}, { timeout: 4000 });
  const reads = () => fake.calls.filter(call => call.path === `${root}/factual-reviews/${reviewId}`).length;
  vi.useFakeTimers(); await act(async () => { await vi.advanceTimersByTimeAsync(3500); });
  expect(reads()).toBe(1); expect(screen.getByText(/NOVA está proponiendo hechos/)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Consultar estado actual' }));
  await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
  expect(reads()).toBe(2); expect(fake.calls.filter(call => call.path === `${root}/factual-reviews`)).toHaveLength(1);
}, 10000);
