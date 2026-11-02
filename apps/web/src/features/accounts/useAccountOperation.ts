import { useCallback, useState } from 'react';
import { ApiError, errorMessage } from '../../services/api';
import { useBoundOperation } from '../../services/useBoundOperation';

/** Presentation lifecycle only: discard late responses and return expired sessions to the account entry. */
export function useAccountOperation(scope: object, onExpired: () => void) {
  const { busy, begin, isCurrent, finish, cancel } = useBoundOperation(scope);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const clear = useCallback(() => { setError(''); setNotice(''); }, []);
  const fail = useCallback((failure: unknown) => {
    if (failure instanceof ApiError && failure.status === 401) onExpired();
    else setError(errorMessage(failure));
  }, [onExpired]);
  const run = useCallback(async <T,>(work: (signal: AbortSignal) => Promise<T>, complete: (result: T) => void, message = '', rejected?: (failure: unknown) => void) => {
    const pending = begin(); if (!pending) return;
    setError(''); setNotice('');
    try {
      const result = await work(pending.signal);
      if (isCurrent(pending)) { complete(result); setNotice(message); }
    } catch (failure) {
      if (isCurrent(pending)) { fail(failure); rejected?.(failure); }
    } finally { finish(pending); }
  }, [begin, isCurrent, finish, fail]);
  return { busy, error, notice, fail, run, cancel, clear };
}
