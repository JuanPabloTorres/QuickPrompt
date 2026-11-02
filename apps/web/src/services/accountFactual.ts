import { z } from 'zod';
import { emptySchema, type ApiClient } from './api';

const factSchema = z.object({ role: z.string(), text: z.string(), sourceField: z.string(), quote: z.string() });
const summarySchema = z.object({ id: z.string().uuid(), libraryId: z.string().uuid(), reviewId: z.string().uuid(), promptId: z.string().uuid(), promptRevision: z.number().int().positive(), title: z.string(), edited: z.boolean(), createdAt: z.string() });
const documentSchema = summarySchema.extend({ text: z.string(), facts: z.array(factSchema), proposalFacts: z.array(factSchema), sourceFields: z.record(z.string(), z.string()) });
const reviewSchema = z.object({ id: z.string().uuid(), libraryId: z.string().uuid(), promptId: z.string().uuid(), promptRevision: z.number().int().positive(), connectionId: z.string().uuid(), version: z.number().int().positive(),
  status: z.enum(['ADMITTED', 'PROPOSING', 'REVIEW', 'COMPOSED', 'APPROVED', 'CANCELLED', 'INVALIDATED', 'EXPIRED', 'FAILED']), title: z.string(), sourceFields: z.record(z.string(), z.string()), proposalFacts: z.array(factSchema), confirmedFacts: z.array(factSchema), preview: z.string().nullable(), documentId: z.string().uuid().nullable(), edited: z.boolean(), expiresAt: z.string(), errorCode: z.string().nullable() });
export type FactualFact = z.infer<typeof factSchema>;
export type FactualReview = z.infer<typeof reviewSchema>;
export type FactualDocument = z.infer<typeof documentSchema>;
export type FactualDocumentSummary = z.infer<typeof summarySchema>;
export type FactualStart = { operationId: string; promptId: string; revision: number; connectionId: string; values: Record<string, string>; sourceFields: string[]; title: string };
type Mutate = <T>(path: string, schema: z.ZodType<T>, body: unknown, signal?: AbortSignal, method?: string) => Promise<T>;

/** Cookie/CSRF comes from the account owner; every read is bound to its requested library/resource. */
export function createAccountFactualService(client: ApiClient, mutate: Mutate, libraryId: string) {
  const root = `/api/accounts/v1/libraries/${encodeURIComponent(libraryId)}`;
  const reviewPath = (id: string) => `${root}/factual-reviews/${encodeURIComponent(id)}`;
  function own<T extends { libraryId: string; id: string }>(value: T, id?: string) {
    if (value.libraryId !== libraryId || (id !== undefined && value.id !== id)) throw new Error('El documento o revisión no corresponde a esta biblioteca y solicitud.');
    return value;
  }
  async function change(id: string, action: string, body: unknown, signal?: AbortSignal) {
    return own(await mutate(`${reviewPath(id)}/${action}`, reviewSchema, body, signal), id);
  }
  return {
    async start(input: FactualStart, signal?: AbortSignal) {
      const result = own(await mutate(`${root}/factual-reviews`, reviewSchema, input, signal));
      if (result.promptId !== input.promptId || result.promptRevision !== input.revision || result.connectionId !== input.connectionId) throw new Error('La revisión no corresponde al prompt y conexión elegidos.');
      if (['ADMITTED', 'PROPOSING', 'REVIEW', 'COMPOSED'].includes(result.status)
        && (Object.keys(result.sourceFields).length !== input.sourceFields.length || input.sourceFields.some(name => result.sourceFields[name] !== input.values[name]))) throw new Error('Las fuentes no corresponden a los campos elegidos y sus valores literales.');
      return result;
    },
    async review(id: string, signal?: AbortSignal) { return own(await client.request(reviewPath(id), reviewSchema, { signal }), id); },
    confirm: (id: string, version: number, facts: FactualFact[], title: string, signal?: AbortSignal) => change(id, 'confirm', { version, confirmed: true, facts, title }, signal),
    async approve(id: string, version: number, signal?: AbortSignal) {
      const result = own(await mutate(`${reviewPath(id)}/approve`, documentSchema, { version, confirmed: true }, signal));
      if (result.reviewId !== id) throw new Error('El documento no corresponde a la revisión aprobada.');
      return result;
    },
    cancel: (id: string, version: number, signal?: AbortSignal) => change(id, 'cancel', { version }, signal),
    async documents(page = 0, signal?: AbortSignal) {
      const result = await client.request(`${root}/factual-documents?page=${page}&limit=20`, z.object({ items: z.array(summarySchema), total: z.number().int().nonnegative(), page: z.number().int().nonnegative(), limit: z.literal(20) }), { signal });
      if (result.page !== page || result.items.length > 20 || new Set(result.items.map(item => item.id)).size !== result.items.length) throw new Error('La página de documentos no corresponde a la solicitud.');
      result.items.forEach(item => own(item)); return result;
    },
    async document(id: string, signal?: AbortSignal) { return own(await client.request(`${root}/factual-documents/${encodeURIComponent(id)}`, documentSchema, { signal }), id); },
    remove: (id: string, signal?: AbortSignal) => mutate(`${root}/factual-documents/${encodeURIComponent(id)}`, emptySchema, { confirmed: true }, signal, 'DELETE'),
  };
}
