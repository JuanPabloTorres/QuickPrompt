import { useEffect, useMemo, useState } from 'react';
import { ApiError, errorMessage } from '../../services/api';
import type { AccountEditorInspection, AccountPrompt, AccountService } from '../../services/accounts';

/** The server owns placeholder syntax. A response may describe only its original editor snapshot. */
export function useAccountEditorInspection(libraryId: string, source: Pick<AccountPrompt, 'id' | 'revision'> | null,
  templateText: string, values: Record<string, string>, service: AccountService, onExpired: () => void) {
  const definition = useMemo(() => ({ libraryId, service, promptId: source?.id ?? null, revision: source?.revision ?? null, templateText }),
    [libraryId, source?.id, source?.revision, templateText, service]);
  const scope = useMemo(() => ({ definition,
    input: { promptId: definition.promptId, revision: definition.revision, templateText, values } }), [definition, templateText, values]);
  const [snapshot, setSnapshot] = useState<{ scope: typeof scope; result?: AccountEditorInspection; error?: string }>();
  const [fields, setFields] = useState<{ definition: typeof definition; names: string[] }>();
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      void service.inspectEditor(libraryId, scope.input, controller.signal).then(result => {
        if (!controller.signal.aborted) { setSnapshot({ scope, result }); setFields({ definition, names: result.variableNames }); }
      }).catch(failure => {
        if (controller.signal.aborted) return;
        if (failure instanceof ApiError && failure.status === 401) onExpired();
        else setSnapshot({ scope, error: errorMessage(failure) });
      });
    }, 150);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [scope, definition, libraryId, service, onExpired, retry]);
  const current = snapshot?.scope === scope ? snapshot : undefined;
  return { result: current?.result, error: current?.error, variableNames: fields?.definition === definition ? fields.names : [],
    pending: !current, retry: () => { setSnapshot(undefined); setRetry(value => value + 1); } };
}
