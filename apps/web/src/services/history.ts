import { z } from 'zod';
import { createApiClient, type ApiClient } from './api';

const executionSchema = z.object({ id: z.string().uuid(), model: z.string(), response: z.string(), elapsedMs: z.number().nonnegative(), createdAt: z.string() });
const entrySchema = z.object({ id: z.string().uuid(), templateId: z.string().uuid(), revision: z.number().int().positive(), title: z.string(), text: z.string(), createdAt: z.string(), executions: z.array(executionSchema) });
const pageSchema = z.object({ page: z.number().int().nonnegative(), pageSize: z.literal(10), total: z.number().int().nonnegative(), responseCount: z.number().int().nonnegative(), transferCount: z.number().int().nonnegative(), expectedSnapshot: z.string().regex(/^[A-F0-9]{64}$/), items: z.array(entrySchema).max(10) });
export type HistoryPage = z.infer<typeof pageSchema>;
export interface CompilationHistoryClient {
  list(page: number, signal?: AbortSignal): Promise<HistoryPage>;
  clear(expectedSnapshot: string, signal?: AbortSignal): Promise<void>;
}
export function createHistoryClient(client: ApiClient = createApiClient()): CompilationHistoryClient {
  return {
    list: (page, signal) => client.request(`/history?page=${page}`, pageSchema, { signal }),
    clear: (expectedSnapshot, signal) => client.request('/history/clear', z.undefined(), { method: 'POST', body: JSON.stringify({ confirmed: true, expectedSnapshot }), signal }),
  };
}
export const history = createHistoryClient();
