import { useEffect, useState } from 'react';

/** Keep agent controls within the current dialog's accessible focus scope. */
export function useAgentOverlayHost(useDock = false) {
  const [host, setHost] = useState<HTMLElement | null>(null);
  useEffect(() => {
    const update = () => setHost(previous => {
      const dialogs = document.querySelectorAll<HTMLElement>('[data-nova-activity-host]');
      const dock = useDock ? document.querySelector<HTMLElement>('[data-nova-dock-live-host]') : null;
      const next = dialogs.item(dialogs.length - 1) ?? (dock && !dock.closest('.nova-dock[hidden]') ? dock : null) ?? document.body;
      return previous === next ? previous : next;
    });
    update();
    const observer = new MutationObserver(update);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['hidden'] });
    return () => observer.disconnect();
  }, [useDock]);
  return host;
}
