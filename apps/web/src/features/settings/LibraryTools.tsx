import { useApplicationServices } from '../../components/ServiceContext';
import { AppIcon } from '../../components/ui/Icon';
import { useState, type ReactNode } from 'react';
import { Download, ShieldCheck } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';
import { type ImportPreview } from '../../services/library';
import { NovaPreferences } from './NovaPreferences';
import { version as applicationVersion } from '../../../package.json';

export function LibraryTools({ onClose, onUpdated, notify: announce, children }: { onClose: () => void; onUpdated: () => void; notify: (message: string) => void; children?: ReactNode }) {
  const { archives } = useApplicationServices();
  const [data, setData] = useState<unknown>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [snapshot, setSnapshot] = useState('');
  const [restore, setRestore] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  function notify(text: string) { setFeedback(text); announce(text); }
  async function work(action: () => Promise<unknown>) {
    setBusy(true); setError('');
    try { await action(); } catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  async function exportLibrary() {
    const archive = await archives.export();
    const url = URL.createObjectURL(new Blob([JSON.stringify(archive, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'quickprompt-backup-v2.json'; anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000); notify('Respaldo exportado. Guárdalo en un lugar privado.');
  }
  async function inspect(file: File) {
    setData(null); setPreview(null); setSnapshot(''); setConfirmed(false);
    if (file.size > 32 * 1024 * 1024) throw new Error('El archivo supera 32 MiB.');
    let imported: unknown;
    if (/\.(sqlite|db|db3)$/i.test(file.name)) {
      if (restore) throw new Error('Para restaurar utiliza un respaldo JSON de QuickPrompt. SQLite legado se importa de forma aditiva.');
      if (file.size > 4000000) throw new Error('El SQLite legado supera 4 MB.');
      imported = await archives.convertLegacy(file);
    } else imported = JSON.parse(await file.text());
    if (restore) {
      const inspected = await archives.inspectRestore(imported);
      setPreview(inspected.preview); setSnapshot(inspected.expectedSnapshot);
    } else setPreview(await archives.inspect(imported));
    setData(imported);
  }
  async function apply() {
    const result = await (restore ? archives.restore({ data, confirmed: true, expectedSnapshot: snapshot }) : archives.import({ data, confirmed: true }));
    setData(null); setPreview(null); setConfirmed(false); onUpdated();
    notify(`${result.imported} prompts procesados. ${restore ? 'Se creó un respaldo SQLite previo al reemplazo.' : 'El archivo original se conservó.'}`);
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Configuración" description="Personaliza la apariencia, NOVA y los datos de este equipo." className="settings-dialog">
    <p className="muted">QuickPrompt NOVA · v{applicationVersion} · Versión inicial</p>
    {children}
    <h3 className="settings-data-heading">Datos y respaldos</h3>
    <div className="data-actions"><button className="secondary" disabled={busy} onClick={() => work(exportLibrary)}><AppIcon glyph={Download} size={20} aria-hidden="true"  />Exportar respaldo JSON</button><button className="text-button" disabled={busy} onClick={() => work(async () => { const backup = await archives.backup(); notify(`Respaldo SQLite verificado: ${backup.fileName}`); })}><AppIcon glyph={ShieldCheck} size={20} aria-hidden="true"  />Crear respaldo SQLite</button></div>
    <details><summary>Importar o restaurar un archivo</summary><label className="restore-choice"><input type="checkbox" checked={restore} disabled={busy} onChange={e => { setRestore(e.target.checked); setData(null); setPreview(null); setConfirmed(false); }} />Reemplazar toda la biblioteca desde un respaldo</label><label htmlFor="library-file">Archivo {restore ? 'JSON de respaldo' : 'JSON o SQLite legado'}</label><input key={String(restore)} id="library-file" type="file" disabled={busy} accept={restore ? '.json' : '.json,.sqlite,.db,.db3'} onChange={e => { const file = e.target.files?.[0]; if (file) work(() => inspect(file)); e.target.value = ''; }} /></details>
    {preview && <div className="import-preview"><h3>Revisión antes de {restore ? 'restaurar' : 'importar'}</h3><p>{preview.newTemplates} nuevos · {preview.duplicates} duplicados · {preview.conflicts} conflictos</p><ul>{preview.items.slice(0, 50).map((item, i) => <li key={i}>{item.title} <span className="muted">· {item.status}</span></li>)}</ul>{preview.items.length > 50 && <p className="muted">Mostrando los primeros 50 de {preview.items.length} elementos validados.</p>}{restore && <label className="restore-choice"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Confirmo reemplazar todos los datos actuales. Se creará un respaldo SQLite antes del cambio.</label>}<div className="dialog-actions"><button className="text-button" disabled={busy} onClick={() => { setData(null); setPreview(null); }}>Cancelar importación</button><button className={restore ? 'destructive' : 'primary'} disabled={busy || (restore && !confirmed)} onClick={() => work(apply)}>{restore ? 'Restaurar biblioteca' : 'Confirmar importación'}</button></div></div>}
    <NovaPreferences />
    {error && <p className="error" role="alert">{error}</p>}
    {feedback && <p className="notice" role="status">{feedback}</p>}
    <div className="dialog-actions"><button className="secondary" disabled={busy} onClick={onClose}>Cerrar</button></div>
  </Dialog>;
}
