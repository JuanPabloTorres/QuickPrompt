import { cleanup, renderHook } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { useInterfacePreferences } from '../src/hooks/useInterfacePreferences';
afterEach(() => { cleanup(); localStorage.clear(); });
it('starts in fast mode and respects an explicitly saved demonstrative preference', () => {
  localStorage.clear();
  const first = renderHook(() => useInterfacePreferences()); expect(first.result.current[0].agentSpeed).toBe('fast'); first.unmount();
  localStorage.setItem('nova-interface-v1', JSON.stringify({ view: 'rows', executionMode: 'SAME_TAB', rememberDraft: false, agentSpeed: 'normal' }));
  const restored = renderHook(() => useInterfacePreferences()); expect(restored.result.current[0].agentSpeed).toBe('normal'); expect(restored.result.current[0].view).toBe('rows');
});
