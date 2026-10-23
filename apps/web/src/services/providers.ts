import { z } from 'zod';
import { compileSchema, openApiResponse, post, request, type ApiClient } from './api';

const providersSchema = z.array(z.object({ providerId: z.string(), displayName: z.string(), destinationUrl: z.string().url().nullable(), modesSupported: z.array(z.string()), canSubmit: z.boolean(), availabilityStatus: z.string() }));
const transferSchema = compileSchema.extend({ destinationUrl: z.string().url(), displayName: z.string(), mode: z.literal('COPY_AND_OPEN') });
export type ProviderCapability = z.infer<typeof providersSchema>[number];
interface PromptUse { promptId: string; revision: number; values: Record<string, string>; confirmed: boolean }
export interface ProviderClient {
  list(signal?: AbortSignal): Promise<ProviderCapability[]>;
  models(signal?: AbortSignal): Promise<{ models: string[] }>;
  prepare(input: PromptUse & { providerId: string }, signal?: AbortSignal): Promise<z.infer<typeof transferSchema>>;
  execute(input: PromptUse & { model: string; expectedText: string }, signal: AbortSignal): Promise<Response>;
}

// Discovery and transport only. Windows/clipboard remain with the interaction
// owner; server rules and stream verification retain their existing authority.
export function createProviderClient(client: ApiClient = { request, open: openApiResponse }): ProviderClient {
  return {
    list: signal => client.request('/providers', providersSchema, { signal }),
    models: signal => client.request('/ai/local/models', z.object({ models: z.array(z.string()) }), { signal }),
    prepare: (input, signal) => client.request('/transfers/prepare', transferSchema, post(input, signal)),
    execute: (input, signal) => client.open('/ai/local/execute', post(input, signal)),
  };
}
export const providers = createProviderClient();
