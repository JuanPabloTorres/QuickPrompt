import { z } from 'zod';
import { promptSchema } from './api';

export const commandSchema = z.object({
  kind: z.enum(['NONE', 'OPEN', 'EDIT', 'UPDATE', 'LIBRARY', 'REFRESH', 'FAVORITES', 'RECENT', 'ARCHIVE']),
  candidates: z.array(promptSchema).max(6), instruction: z.string().max(4000), message: z.string(),
});
export type LocalCommand = z.infer<typeof commandSchema>;
export const proposalSchema = z.object({ text: z.string(), reasons: z.array(z.string()), changes: z.array(z.string()), warnings: z.array(z.string()), templateId: z.string().uuid().nullable(), baseRevision: z.number(), model: z.string() });
