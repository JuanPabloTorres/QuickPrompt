import { createContext, useContext, useState, type ReactNode } from 'react';
import { createAgentProposalSession, type AgentProposalPort } from './AgentProposalSession';

const AgentUiContext = createContext<AgentProposalPort | null>(null);

// One stable UI resource owner per application mount; no HTTP/domain state.
export function AgentUiProvider({ children, session }: { children: ReactNode; session?: AgentProposalPort }) {
  const [owned] = useState(createAgentProposalSession);
  return <AgentUiContext.Provider value={session ?? owned}>{children}</AgentUiContext.Provider>;
}

export function useAgentProposalSession(): AgentProposalPort | null { return useContext(AgentUiContext); }
