import { ArrowUpRight } from 'lucide-react';
import { IconAction } from './ui/Icon';
import { version } from '../../package.json';

export function WorkspaceFooter({ onHelp, privacyLabel = 'Datos en tu equipo' }: { onHelp?: () => void; privacyLabel?: string }) {
  return <footer className="workspace-footer">
    <div><strong>QuickPrompt <span>NOVA</span></strong><p>Tu biblioteca. Tus ideas. Tu siguiente paso.</p></div>
    <div className="footer-meta"><span>v{version}</span><span className="footer-private"><i aria-hidden="true" />{privacyLabel}</span>{onHelp && <IconAction label="Guía de QuickPrompt" glyph={ArrowUpRight} onClick={onHelp} />}</div>
  </footer>;
}
