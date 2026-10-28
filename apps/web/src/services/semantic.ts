import { z } from 'zod';

export const semanticSchema = z.object({
  intentions: z.array(z.string()).max(12), targetReference: z.string(), targetDescription: z.string(), resolvedTargetId: z.string().uuid().nullable(),
  query: z.string(), requestedTransformation: z.string(), desiredOutcome: z.string(),
  constraints: z.object({ forbiddenOperations: z.array(z.string()), preserveOriginal: z.boolean(), language: z.string().nullable(), format: z.string().nullable(), literalClauses: z.array(z.string()), targetTerms: z.array(z.string()), shortening: z.boolean(), userProhibitions: z.array(z.string()).nullable().optional() }),
  proposedSteps: z.array(z.object({ id: z.number().int(), operation: z.string(), dependsOn: z.array(z.number().int()) })).max(12),
  missingInformation: z.array(z.string()), requiresConfirmation: z.boolean(), executionEvidence: z.string(), providerId: z.string().nullable(), explanation: z.string(),
  validationNotes: z.array(z.string()).nullable().optional(),
  deferredGoals: z.array(z.string()).nullable().optional(), deferredOutcome: z.string().nullable().optional(),
  continuesPendingTask: z.boolean().optional(),
});
