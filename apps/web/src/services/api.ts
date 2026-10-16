import { z } from 'zod';

export const promptSchema = z.object({
  id: z.string().uuid(), title: z.string(), description: z.string(), templateText: z.string(),
  categoryId: z.number().int(), isFavorite: z.boolean(), isArchived: z.boolean(), revision: z.number().int().positive(),
  createdAt: z.string(), updatedAt: z.string(), uses: z.number().int(), lastUsedAt: z.string().nullable(),
  variableNames: z.array(z.string()), defaults: z.record(z.string(), z.string()),
});
export type Prompt = z.infer<typeof promptSchema>;
export type PromptDraft = Pick<Prompt, 'title' | 'templateText' | 'description' | 'categoryId'> & { revision?: number };
export const pageSchema = z.object({ items: z.array(promptSchema), total: z.number().int().nonnegative() });
export const groupsSchema = z.array(z.object({ group: z.object({ id: z.string().uuid(), name: z.string(), revision: z.number() }), templates: z.number(), instructions: z.number() }));
export type Group = z.infer<typeof groupsSchema>[number];
export class ApiError extends Error {
  constructor(message: string, public status: number) { super(message); }
}
export async function request<T>(path: string, schema: z.ZodType<T>, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`/api/v1${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', 'X-QuickPrompt-Client': 'nova-v1', ...options.headers },
  });
  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    const parsed = z.object({ detail: z.string().optional(), title: z.string().optional() }).safeParse(problem);
    throw new ApiError(parsed.success ? parsed.data.detail ?? parsed.data.title ?? 'No se pudo completar la operación.' : 'No se pudo conectar con la biblioteca.', response.status);
  }
  if (response.status === 204) return schema.parse(undefined);
  const result = schema.safeParse(await response.json());
  if (!result.success) throw new Error('La respuesta del servidor no es compatible. Actualiza la aplicación.');
  return result.data;
}
export const post = (body: unknown, signal?: AbortSignal): RequestInit => ({ method: 'POST', body: JSON.stringify(body), signal });
export const compileSchema = z.object({ text: z.string(), templateRevision: z.number().int() });
export const inspectSchema = z.object({ variableNames: z.array(z.string()) });
export const emptySchema = z.undefined();
export const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'No se pudo completar la operación.';
