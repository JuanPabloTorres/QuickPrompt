import { useEffect, useState } from 'react';
import { useUiPortalScope } from '../../components/ui/UiPortalScope';

/** Keep agent controls within the current dialog's accessible focus scope. */
export function useAgentOverlayHost(useDock = false) {
  const scope = useUiPortalScope();
  const [host, setHost] = useState<HTMLElement | null>(null);
  useEffect(() => {
    const update = () => setHost(previous => {
      const dialogs = (scope?.dialogHost ?? document).querySelectorAll<HTMLElement>('[data-nova-activity-host]');
      const dock = useDock ? document.querySelector<HTMLElement>('[data-nova-dock-live-host]') : null;
      const next = scope?.active === false ? scope.overlayHost :
        dialogs.item(dialogs.length - 1) ?? (dock && !dock.closest('.nova-dock[hidden]') ? dock : null) ?? scope?.overlayHost ?? document.body;
      return previous === next ? previous : next;
    });
    update();
    const observer = new MutationObserver(update);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['hidden'] });
    return () => observer.disconnect();
  }, [useDock, scope?.active, scope?.dialogHost, scope?.overlayHost]);
  return host;
}
