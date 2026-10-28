import { useLayoutEffect, useRef, useState } from 'react';
import type { PromptDraft } from '../../services/api';

export type TextSelection = { start: number; end: number };
type Snapshot = { draft: PromptDraft; originals: ReadonlyMap<string, string>; selection: TextSelection };
type History = { identity: string; present: Snapshot; past: Snapshot[]; future: Snapshot[] };
const snapshot = (draft: PromptDraft): Snapshot => ({ draft, originals: new Map(), selection: { start: 0, end: 0 } });
const sameDraft = (left: PromptDraft, right: PromptDraft) => left === right || JSON.stringify(left) === JSON.stringify(right);
function trim(stack: Snapshot[]) {
  let size = 0;
  for (let index = stack.length - 1; index >= 0; index--) {
    const item = stack[index];
    size += item.draft.templateText.length + item.draft.title.length + item.draft.description.length
      + Object.values(item.draft.defaults ?? {}).reduce((total, value) => total + value.length, 0)
      + [...item.originals.values()].reduce((total, value) => total + value.length, 0);
    if (stack.length - index > 100 || size > 3_200_000) { stack.splice(0, index + 1); break; }
  }
}

// UI interaction history only. App owns the draft; the server owns syntax and persistence.
export function useEditorHistory(draft: PromptDraft, identity: string, onChange: (draft: PromptDraft) => void) {
  const history = useRef<History>({ identity, present: snapshot(draft), past: [], future: [] });
  const [availability, setAvailability] = useState({ undo: false, redo: false });
  function publish() { const current = history.current; setAvailability({ undo: !!current.past.length, redo: !!current.future.length }); }
  useLayoutEffect(() => {
    const current = history.current;
    if (current.identity !== identity) {
      history.current = { identity, present: snapshot(draft), past: [], future: [] }; publish();
    } else if (!sameDraft(current.present.draft, draft)) {
      current.past.push(current.present); trim(current.past); current.future = [];
      current.present = { ...current.present, draft }; publish();
    }
  }, [draft, identity]);
  function commit(next: PromptDraft, selection = history.current.present.selection, originals = history.current.present.originals) {
    const current = history.current;
    if (!sameDraft(current.present.draft, next)) {
      current.past.push(current.present); trim(current.past); current.future = [];
      current.present = { draft: next, originals: new Map(originals), selection }; publish();
    }
    onChange(next);
  }
  function move(direction: 'undo' | 'redo') {
    const current = history.current, from = direction === 'undo' ? current.past : current.future;
    const next = from.pop(); if (!next) return null;
    const destination = direction === 'undo' ? current.future : current.past;
    destination.push(current.present); trim(destination); current.present = next;
    publish(); onChange(next.draft); return next;
  }
  return { ...availability, commit, move,
    capture: (selection: TextSelection) => { history.current.present = { ...history.current.present, selection }; },
    originals: () => history.current.present.originals };
}
