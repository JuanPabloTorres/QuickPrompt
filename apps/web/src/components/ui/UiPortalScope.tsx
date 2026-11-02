import { createContext, useContext } from 'react';

/** Presentation/lifecycle gate; it never grants authority to a domain operation. */
export type UiPortalScope = {
  active: boolean;
  dialogHost: HTMLElement | null;
  overlayHost: HTMLElement | null;
  onRetry: () => void;
};

const Context = createContext<UiPortalScope | null>(null);
export const UiPortalScopeProvider = Context.Provider;
export const useUiPortalScope = () => useContext(Context);
