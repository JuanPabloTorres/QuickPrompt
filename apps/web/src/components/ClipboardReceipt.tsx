import type { UiObservation } from '../services/live';

// Published only by the owning Copy operation after the browser accepts its write.
// This is an application receipt, not a claim about another app's clipboard read.
export function ClipboardReceipt({ receipt }: { receipt: NonNullable<UiObservation['clipboard']> | null }) {
  return receipt && <span className="sr-only" data-testid="clipboard-receipt"
    data-operation-id={receipt.operationId} data-prompt-id={receipt.promptId ?? ''}
    data-revision={receipt.revision ?? ''} data-copy-content={receipt.copyContent ?? 'RENDERED'} data-clipboard-text={receipt.text}>Texto copiado</span>;
}
