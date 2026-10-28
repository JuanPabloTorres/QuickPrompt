import type { ReactNode } from 'react';
import { Check, CircleAlert, LoaderCircle } from 'lucide-react';
import { AppIcon } from './Icon';
import { NovaLogo, type NovaVisualState } from '../../design/NovaLogo';

export function InlineSpinner() { return <AppIcon glyph={LoaderCircle} size={16} className="nova-working" />; }
export function NovaActivity({ state, children }: { state: NovaVisualState; children: ReactNode }) { return <span className="status-feedback" data-state={state}><NovaLogo state={state} />{children}</span>; }
export function NovaProgress({ children }: { children: ReactNode }) { return <span className="status-feedback" role="status"><NovaLogo state="generating" />{children}</span>; }
export function SaveIndicator({ busy, children }: { busy: boolean; children: ReactNode }) { return <>{busy ? <InlineSpinner /> : <AppIcon glyph={Check} />}<span>{children}</span></>; }
export function TransferStatus({ children }: { children: ReactNode }) { return <p className="notice status-feedback" role="status"><AppIcon glyph={Check} size={16} />{children}</p>; }
export function EmptyState({ title, children }: { title: string; children: ReactNode }) { return <div className="empty-state"><h3>{title}</h3>{children}</div>; }
export function ErrorState({ title, message, children }: { title: string; message: string; children?: ReactNode }) { return <div className="empty-state"><AppIcon glyph={CircleAlert} size={32} /><h3>{title}</h3><p role="alert" className="error">{message}</p>{children}</div>; }
// Shown only after a sustained library load: CSS delays visibility, not the request.
export function PromptSkeleton() { return <div className="prompt-skeleton" aria-hidden="true">{[0, 1, 2].map(key => <div key={key}><span /><span /><span /></div>)}</div>; }
