import { createContext, useContext, type ReactNode } from 'react';
import { defaultApplicationServices, type ApplicationServices } from '../services/application';

const ServicesContext = createContext<ApplicationServices>(defaultApplicationServices);
export type AgentRuntimeServices = Pick<ApplicationServices, 'live' | 'visibleSession'>;
const AgentServicesContext = createContext<AgentRuntimeServices | null>(null);

// One immutable composition per application mount; UI/domain state stays outside.
export function ApplicationServiceProvider({ services, children }: { services: ApplicationServices; children: ReactNode }) {
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}
export function useApplicationServices(): ApplicationServices { return useContext(ServicesContext); }

/** Private hosts supply only native agent ports; no fake local library or provider services. */
export function AgentRuntimeServiceProvider({ services, children }: { services: AgentRuntimeServices; children: ReactNode }) {
  return <AgentServicesContext.Provider value={services}>{children}</AgentServicesContext.Provider>;
}
export function useAgentRuntimeServices(): AgentRuntimeServices {
  const supplied = useContext(AgentServicesContext);
  const application = useContext(ServicesContext);
  return supplied ?? application;
}
