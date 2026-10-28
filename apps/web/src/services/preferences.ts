import { z } from 'zod';
import { emptySchema, openApiResponse, request, type ApiClient } from './api';

export const preferencesSchema = z.object({ language: z.enum(['es', 'en']).nullable(), format: z.enum(['JSON', 'Markdown', 'Tabla', 'Texto']).nullable(), providerId: z.string().nullable() });
export type PreparationPreferences = z.infer<typeof preferencesSchema>;
export interface PreferenceClient {
  get(signal?: AbortSignal): Promise<PreparationPreferences>;
  save(preferences: PreparationPreferences, confirmed: boolean, signal?: AbortSignal): Promise<PreparationPreferences>;
  forget(signal?: AbortSignal): Promise<void>;
}
export function createPreferenceClient(client: ApiClient = { request, open: openApiResponse }): PreferenceClient {
  return {
    get: signal => client.request('/nova/preferences', preferencesSchema, { signal }),
    save: (preferences, confirmed, signal) => client.request('/nova/preferences', preferencesSchema, { method: 'PUT', body: JSON.stringify({ preferences, confirmed }), signal }),
    forget: signal => client.request('/nova/preferences', emptySchema, { method: 'DELETE', signal }),
  };
}
export const preferences = createPreferenceClient();
