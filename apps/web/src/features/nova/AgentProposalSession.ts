export interface AgentProposalWriter {
  proposal(text: string, signal: AbortSignal): Promise<void>;
}

export interface AgentProposalPort {
  register(writer: AgentProposalWriter): () => void;
  apply(text: string, signal: AbortSignal): Promise<boolean>;
}

// Owns only the current proposal destination. Driver lifetime and DOM authority
// stay with SameTabAgent/AgentUiDriver; a release is bound to one registration.
class AgentProposalSession implements AgentProposalPort {
  private current: { writer: AgentProposalWriter } | null = null;

  register(writer: AgentProposalWriter): () => void {
    const registration = { writer };
    this.current = registration;
    return () => { if (this.current === registration) this.current = null; };
  }

  async apply(text: string, signal: AbortSignal): Promise<boolean> {
    const registration = this.current;
    if (!registration) return false;
    if (signal.aborted) throw new Error('UI_CANCELLED');
    await registration.writer.proposal(text, signal);
    if (signal.aborted || this.current !== registration) throw new Error('UI_CANCELLED');
    return true;
  }
}

export function createAgentProposalSession(): AgentProposalPort { return new AgentProposalSession(); }
