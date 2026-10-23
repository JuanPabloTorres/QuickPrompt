import { useSyncExternalStore } from 'react';

const media = () => typeof matchMedia === 'function' ? matchMedia('(prefers-reduced-motion: reduce)') : null;
function subscribe(listener: () => void) {
  const query = media(); query?.addEventListener('change', listener);
  return () => query?.removeEventListener('change', listener);
}
const snapshot = () => media()?.matches ?? false;
const serverSnapshot = () => false;
// Also responds if the OS preference changes while NOVA is already open.
export function useMotionPreference(decorativeMotion: boolean) {
  return useSyncExternalStore(subscribe, snapshot, serverSnapshot) || !decorativeMotion;
}
