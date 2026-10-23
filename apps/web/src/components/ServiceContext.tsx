import { createContext, useContext, type ReactNode } from 'react';
import { defaultApplicationServices, type ApplicationServices } from '../services/application';

const ServicesContext = createContext<ApplicationServices>(defaultApplicationServices);

// One immutable composition per application mount; UI/domain state stays outside.
export function ApplicationServiceProvider({ services, children }: { services: ApplicationServices; children: ReactNode }) {
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}
export function useApplicationServices(): ApplicationServices { return useContext(ServicesContext); }
