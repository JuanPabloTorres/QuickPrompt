import { useCallback, useLayoutEffect, useRef, useState } from 'react';

// Owns only an in-flight UI operation. Domain data and authorization stay with callers.
export function useBoundOperation(snapshot: object) {
  const current = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const cancel = useCallback(() => {
    current.current?.abort(); current.current = null; setBusy(false);
  }, []);
  useLayoutEffect(() => { cancel(); return cancel; }, [snapshot, cancel]);
  const begin = useCallback(() => {
    if (current.current) return null;
    const operation = new AbortController(); current.current = operation; setBusy(true);
    return operation;
  }, []);
  const isCurrent = useCallback((operation: AbortController) => current.current === operation && !operation.signal.aborted, []);
  const finish = useCallback((operation: AbortController) => {
    if (current.current !== operation) return;
    current.current = null; setBusy(false);
  }, []);
  return { busy, begin, isCurrent, finish, cancel };
}
