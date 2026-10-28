import { useCallback, useMemo } from 'react';
import { ApiError } from '../../services/api';
import type { AccountPrompt, AccountService } from '../../services/accounts';
import { CollectionManager } from '../groups/GroupSelector';

/** Account composition only: the shared manager never chooses an identity or falls back to the local library. */
export function AccountCollectionDialog({ libraryId, prompt, service, onExpired, onUpdated, onClose }: { libraryId: string; prompt?: AccountPrompt; service: AccountService; onExpired: () => void; onUpdated: () => void; onClose: () => void }) {
  const collections = useMemo(() => service.collections(libraryId), [libraryId, service]);
  const failure = useCallback((error: unknown) => { if (error instanceof ApiError && error.status === 401) onExpired(); }, [onExpired]);
  return <CollectionManager collections={collections} prompt={prompt} onFailure={failure} onUpdated={onUpdated} onClose={onClose} />;
}
