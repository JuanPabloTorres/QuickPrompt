import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useRef, useState } from 'react';
import { History } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { AppIcon } from '../../components/ui/Icon';
import { errorMessage, type Prompt } from '../../services/api';
import { type PromptVersion } from '../../services/library';

function DefaultValues({ values }: { values: Record<string, string> }) {
  return Object.keys(values).length > 0 ? <><h4>Valores predeterminados</h4><dl>{Object.entries(values).map(([name, value]) =>
    <div key={name}><dt>{name}</dt><dd><pre>{value}</pre></dd></div>)}</dl></> : null;
}

export function PromptVersions({ prompt, onClose, onRestored, onCurrentChanged }: {
  prompt: Prompt; onClose: () => void; onRestored: (prompt: Prompt) => void; onCurrentChanged?: (prompt: Prompt) => void;
}) {
  const { prompts } = useApplicationServices();
  const [versions, setVersions] = useState<PromptVersion[]>([]);
  const [chosenRevision, setChosenRevision] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [restoring, setRestoring] = useState(false);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const refreshController = useRef<AbortController | null>(null);
  useEffect(() => () => refreshController.current?.abort(), []);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    setVersions([]); setChosenRevision(null);
    prompts.versions(prompt.id, controller.signal)
      .then(result => { if (!controller.signal.aborted) { setVersions(result); setChosenRevision(result[0]?.revision ?? null); } })
      .catch(failure => { if (!controller.signal.aborted) setError(errorMessage(failure)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [prompt.id, prompt.revision, reload]);
  const selected = versions.find(version => version.revision === chosenRevision);
  async function refresh() {
    if (loading || restoring) return;
    if (!onCurrentChanged) { setReload(value => value + 1); return; }
    refreshController.current?.abort();
    const controller = new AbortController(); refreshController.current = controller;
    setLoading(true); setError('');
    try {
      const current = await prompts.get(prompt.id, controller.signal);
      if (!controller.signal.aborted) { onCurrentChanged(current); setReload(value => value + 1); }
    } catch (failure) { if (!controller.signal.aborted) { setError(errorMessage(failure)); setLoading(false); } }
  }
  async function restore() {
    if (restoring || !selected || selected.revision >= prompt.revision) return;
    setRestoring(true); setError('');
    try {
      const result = await prompts.restoreVersion(prompt, selected.revision, true);
      onRestored(result);
    } catch (failure) { setError(errorMessage(failure)); }
    finally { setRestoring(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !restoring) onClose(); }}
    title={`Versiones de «${prompt.title}»`}
    description="Compara el contenido antes de restaurar. Se creará una versión nueva; las anteriores se conservarán. Tus favoritos, archivo y contadores permanecerán como están.">
    {loading ? <p role="status">Cargando versiones…</p> : <>
      <label htmlFor="prompt-version">Versión para comparar</label>
      <select id="prompt-version" value={chosenRevision ?? ''} disabled={restoring} onChange={event => setChosenRevision(Number(event.target.value))}>
        {!versions.length && <option value="">No hay versiones disponibles</option>}
        {versions.map(version => <option key={version.revision} value={version.revision}>Versión {version.revision} · {new Date(version.createdAt).toLocaleString()}</option>)}
      </select>
      {selected && <div className="variable-grid">
        <section className="prompt-preview" aria-label="Versión actual"><h3>Actual · versión {prompt.revision}</h3><strong>{prompt.title}</strong><p>{prompt.description}</p><pre>{prompt.templateText}</pre>
          <DefaultValues values={prompt.defaults} />
        </section>
        <section className="prompt-preview" aria-label="Versión seleccionada"><h3>Seleccionada · versión {selected.revision}</h3><strong>{selected.snapshot.title}</strong><p>{selected.snapshot.description}</p><pre>{selected.snapshot.template}</pre>
          <DefaultValues values={selected.snapshot.variables} />
        </section>
      </div>}
    </>}
    {error && <p role="alert" className="error">{error}</p>}
    <div className="dialog-actions">
      <button type="button" className="secondary" disabled={restoring} onClick={onClose}>Cancelar</button>
      {error && <button type="button" className="secondary" disabled={restoring || loading} onClick={refresh}>Actualizar comparación</button>}
      <button type="button" className="primary" disabled={loading || restoring || !selected || selected.revision >= prompt.revision} onClick={restore}>
        <AppIcon glyph={History} size={20} aria-hidden="true" />{restoring ? 'Restaurando…' : `Restaurar versión ${chosenRevision ?? ''}`}
      </button>
    </div>
  </Dialog>;
}
