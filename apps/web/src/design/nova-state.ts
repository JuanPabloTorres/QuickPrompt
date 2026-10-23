import type { NovaVisualState } from './NovaLogo';

// Presentation of observed execution facts only; never controls the agent.
export function visualState(status?: string, stage?: string, tool?: string | null, paused = false): NovaVisualState {
  if (!status || paused || status === 'PAUSED' || status === 'CANCELLED') return 'idle';
  if (status === 'FAILED' || status === 'BLOCKED') return 'error';
  if (status === 'COMPLETED' || status === 'AWAITING_APPROVAL') return 'ready';
  if (status === 'VERIFYING' || stage === 'Verification') return 'validating';
  if (stage === 'Generation' || stage === 'GenerationRetry' || tool === 'ui_execute_local') return 'generating';
  if (tool === 'ui_search_prompt') return 'searching';
  if (status === 'TOOL_RUNNING' && tool?.startsWith('ui_')) return 'navigating';
  return 'thinking';
}
