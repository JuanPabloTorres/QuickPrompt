import { useEffect, useState } from 'react';
import { z } from 'zod';

const schema = z.object({ view: z.enum(['cards', 'rows']), executionMode: z.enum(['SAME_TAB', 'LIVE']), rememberDraft: z.boolean(), agentSpeed: z.enum(['normal', 'fast']), decorativeMotion: z.boolean().default(true) });
export type InterfacePreferences = z.infer<typeof schema>;
const defaults: InterfacePreferences = { view: 'cards', executionMode: 'SAME_TAB', rememberDraft: false, agentSpeed: 'fast', decorativeMotion: true };
export function useInterfacePreferences() {
  const [preferences, setPreferences] = useState<InterfacePreferences>(() => {
    try { const parsed = schema.safeParse(JSON.parse(localStorage.getItem('nova-interface-v1') ?? 'null')); return parsed.success ? parsed.data : defaults; }
    catch { return defaults; }
  });
  useEffect(() => { try { localStorage.setItem('nova-interface-v1', JSON.stringify(preferences)); } catch { /* Settings remain usable without storage. */ } }, [preferences]);
  useEffect(() => { document.documentElement.dataset.motion = preferences.decorativeMotion ? 'full' : 'reduced'; }, [preferences.decorativeMotion]);
  return [preferences, setPreferences] as const;
}
