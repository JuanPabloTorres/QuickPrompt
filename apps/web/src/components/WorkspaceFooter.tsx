import { ArrowUpRight } from 'lucide-react';
import { AppIcon } from './ui/Icon';
import { version } from '../../package.json';

export function WorkspaceFooter({ onHelp, privacyLabel = 'Datos en tu equipo' }: { onHelp?: () => void; privacyLabel?: string }) {
  return <footer className="workspace-footer">
    <div><strong>QuickPrompt <span>NOVA</span></strong><p>Tu biblioteca. Tus ideas. Tu siguiente paso.</p></div>
    <div className="footer-meta"><span>v{version}</span><span className="footer-private"><i aria-hidden="true" />{privacyLabel}</span>{onHelp && <button type="button" className="text-button" onClick={onHelp}>Guía de QuickPrompt<AppIcon glyph={ArrowUpRight} size={16} /></button>}</div>
  </footer>;
}
