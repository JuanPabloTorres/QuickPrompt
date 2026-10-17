import { useState } from 'react';
import { z } from 'zod';
import { Download, ShieldCheck } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage, post, request } from '../../services/api';
import { NovaPreferences } from './NovaPreferences';

const previewSchema = z.object({ newTemplates: z.number(), duplicates: z.number(), conflicts: z.number(), items: z.array(z.object({ title: z.string(), status: z.string() })) });
export function LibraryTools({ onClose, onUpdated, notify: announce }: { onClose: () => void; onUpdated: () => void; notify: (message: string) => void }) {
  const [data, setData] = useState<unknown>(null);
  const [preview, setPreview] = useState<z.infer<typeof previewSchema> | null>(null);
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
    const archive = await request('/library/export', z.unknown());
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
      imported = await request('/library/legacy/convert', z.unknown(), { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: file });
    } else imported = JSON.parse(await file.text());
    if (restore) {
      const inspected = await request('/library/restore/preview', z.object({ preview: previewSchema, expectedSnapshot: z.string() }), post(imported));
      setPreview(inspected.preview); setSnapshot(inspected.expectedSnapshot);
    } else setPreview(await request('/library/import/preview', previewSchema, post(imported)));
    setData(imported);
  }
  async function apply() {
    const result = await request(restore ? '/library/restore' : '/library/import', z.object({ imported: z.number() }), post({ data, confirmed: true, expectedSnapshot: snapshot || undefined }));
    setData(null); setPreview(null); setConfirmed(false); onUpdated();
    notify(`${result.imported} prompts procesados. ${restore ? 'Se creó un respaldo SQLite previo al reemplazo.' : 'El archivo original se conservó.'}`);
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Tu biblioteca permanece contigo" description="Exporta todos tus datos o importa un archivo que elijas. Todo se procesa en este equipo.">
    <div className="data-actions"><button className="secondary" disabled={busy} onClick={() => work(exportLibrary)}><Download size={17} aria-hidden="true" />Exportar respaldo JSON</button><button className="text-button" disabled={busy} onClick={() => work(async () => { const backup = await request('/library/backup', z.object({ fileName: z.string(), integrity: z.literal('ok') }), post({})); notify(`Respaldo SQLite verificado: ${backup.fileName}`); })}><ShieldCheck size={17} aria-hidden="true" />Crear respaldo SQLite</button></div>
    <details><summary>Importar o restaurar un archivo</summary><label className="restore-choice"><input type="checkbox" checked={restore} disabled={busy} onChange={e => { setRestore(e.target.checked); setData(null); setPreview(null); setConfirmed(false); }} />Reemplazar toda la biblioteca desde un respaldo</label><label htmlFor="library-file">Archivo {restore ? 'JSON de respaldo' : 'JSON o SQLite legado'}</label><input key={String(restore)} id="library-file" type="file" disabled={busy} accept={restore ? '.json' : '.json,.sqlite,.db,.db3'} onChange={e => { const file = e.target.files?.[0]; if (file) work(() => inspect(file)); e.target.value = ''; }} /></details>
    {preview && <div className="import-preview"><h3>Revisión antes de {restore ? 'restaurar' : 'importar'}</h3><p>{preview.newTemplates} nuevos · {preview.duplicates} duplicados · {preview.conflicts} conflictos</p><ul>{preview.items.slice(0, 50).map((item, i) => <li key={i}>{item.title} <span className="muted">· {item.status}</span></li>)}</ul>{preview.items.length > 50 && <p className="muted">Mostrando los primeros 50 de {preview.items.length} elementos validados.</p>}{restore && <label className="restore-choice"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Confirmo reemplazar todos los datos actuales. Se creará un respaldo SQLite antes del cambio.</label>}<div className="dialog-actions"><button className="text-button" disabled={busy} onClick={() => { setData(null); setPreview(null); }}>Cancelar importación</button><button className={restore ? 'destructive' : 'primary'} disabled={busy || (restore && !confirmed)} onClick={() => work(apply)}>{restore ? 'Restaurar biblioteca' : 'Confirmar importación'}</button></div></div>}
    <NovaPreferences />
    {error && <p className="error" role="alert">{error}</p>}
    {feedback && <p className="notice" role="status">{feedback}</p>}
    <div className="dialog-actions"><button className="secondary" disabled={busy} onClick={onClose}>Cerrar</button></div>
  </Dialog>;
}
