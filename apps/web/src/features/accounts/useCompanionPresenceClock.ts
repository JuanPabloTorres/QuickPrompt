import { useEffect, useMemo, useState } from 'react';
import type { AccountConnection } from '../../services/accounts';
import { companionPresenceDeadline } from '../../services/companionPresence';

/** One local timer per list. Never refreshes authorization, performs HTTP or changes agent ownership. */
export function useCompanionPresenceClock(connections: AccountConnection[] | undefined) {
  const received = useMemo(() => ({ wall: Date.now(), tick: performance.now() }), [connections]);
  const [tick, setTick] = useState(() => performance.now());
  // A wall-clock correction cannot prolong a cached positive signal indefinitely.
  const now = received.wall + Math.max(0, tick - received.tick);
  useEffect(() => {
    const current = received.wall + Math.max(0, performance.now() - received.tick);
    const deadlines = (connections ?? []).map(item => companionPresenceDeadline(item, received.wall))
      .filter((deadline): deadline is number => deadline !== null && deadline > current);
    if (!deadlines.length) return;
    const timer = window.setTimeout(() => setTick(performance.now()), Math.max(1, Math.min(...deadlines) - current + 1));
    return () => window.clearTimeout(timer);
  }, [connections, received, tick]);
  return { now, receivedAt: received.wall };
}
