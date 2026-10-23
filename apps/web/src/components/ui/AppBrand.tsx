import { NovaLogo } from '../../design/NovaLogo';
import { version as applicationVersion } from '../../../package.json';

export function AppBrand({ onClick }: { onClick: () => void }) {
  return <button data-nova-control="library.home" className="brand" onClick={onClick} aria-label="QuickPrompt NOVA, biblioteca"><NovaLogo size={32} /><span>QuickPrompt <span className="brand-nova">NOVA</span> <small className="brand-version" title="Versión inicial">v{applicationVersion}</small></span></button>;
}
