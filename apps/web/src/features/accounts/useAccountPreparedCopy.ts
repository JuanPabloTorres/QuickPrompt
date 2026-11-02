import { useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';
import { ApiError } from '../../services/api';
import type { AccountExecutionResult, AccountService } from '../../services/accounts';
import { useAccountOperation } from './useAccountOperation';

/** Clipboard success and the idempotent use receipt are separate, scope-bound effects. */
export function useAccountPreparedCopy(libraryId: string, result: AccountExecutionResult | undefined, service: AccountService, onExpired: () => void, onUsed?: () => void) {
  const scope = useMemo(() => ({ libraryId, result, service }), [libraryId, result, service]);
  const operation = useAccountOperation(scope, onExpired);
  const active = useUiPortalScope()?.active !== false;
  useLayoutEffect(() => { if (!active) operation.cancel(); }, [active, operation.cancel]);
  const used = useRef(onUsed); used.current = onUsed;
  const [notice, setNotice] = useState<{ scope: object; text: string }>();
  const [pending, setPending] = useState<{ scope: object; snapshot: AccountExecutionResult; conflict: boolean }>();
  const current = pending?.scope === scope ? pending : undefined;
  function registered() {
    setPending(undefined);
    setNotice({ scope, text: 'Instrucción copiada. Uso confirmado para esta preparación.' });
    used.current?.();
  }
  function rejected(failure: unknown) {
    if (failure instanceof ApiError && (failure.status === 409 || failure.status === 404))
      setPending(previous => previous?.scope === scope ? { ...previous, conflict: true } : previous);
  }
  function copy() {
    if (!result || !active || operation.busy || current) return;
    const snapshot = { ...result, usage: result.usage ? { operationId: result.usage.operationId, values: { ...result.usage.values } } : result.usage };
    setNotice(undefined);
    void operation.run(async signal => {
      await navigator.clipboard.writeText(snapshot.text);
      if (signal.aborted) throw new DOMException('Operación cancelada', 'AbortError');
      setNotice({ scope, text: 'Instrucción copiada. El uso todavía no se ha confirmado.' });
      if (!snapshot.usage) return false;
      setPending({ scope, snapshot, conflict: false });
      await service.recordPromptUse(libraryId, snapshot, signal);
      return true;
    }, confirmed => {
      if (confirmed) registered();
      else setNotice({ scope, text: 'Instrucción copiada. Este resultado anterior no permite verificar un registro de uso.' });
    }, '', rejected);
  }
  function retry() {
    if (!current || !active || current.conflict || operation.busy) return;
    void operation.run(signal => service.recordPromptUse(libraryId, current.snapshot, signal), registered, '', rejected);
  }
  return { copy, retry, busy: operation.busy, pending: !!current, conflict: !!current?.conflict, error: operation.error,
    notice: notice?.scope === scope ? notice.text : '' };
}
