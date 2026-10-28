import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { Dialog } from '../../components/ui/Dialog';
import { AppIcon } from '../../components/ui/Icon';
import { errorMessage, type Prompt } from '../../services/api';
import { type SharedPromptPacket } from '../../services/library';

export function SharePrompt({ prompt, onClose, onReload }: { prompt: Prompt; onClose: () => void; onReload: () => void }) {
  const { prompts } = useApplicationServices();
  const [result, setResult] = useState<{ id: string; revision: number; packet: SharedPromptPacket } | null>(null);
  const [error, setError] = useState('');
  const [feedback, setFeedback] = useState('');
  const packet = result?.id === prompt.id && result.revision === prompt.revision ? result.packet : null;
  useEffect(() => {
    const controller = new AbortController(); setResult(null); setError(''); setFeedback('');
    prompts.share(prompt, controller.signal)
      .then(packet => { if (!controller.signal.aborted) setResult({ id: prompt.id, revision: prompt.revision, packet }); })
      .catch(failure => { if (!controller.signal.aborted) setError(errorMessage(failure)); });
    return () => controller.abort();
  }, [prompt.id, prompt.revision]);
  function download() {
    if (!packet) return;
    try {
      const url = URL.createObjectURL(new Blob([JSON.stringify(packet, null, 2)], { type: 'application/json;charset=utf-8' }));
      const release = URL.revokeObjectURL.bind(URL);
      const link = document.createElement('a'); link.href = url; link.download = 'quickprompt-plantilla.json'; link.click();
      setTimeout(() => release(url), 1000); setFeedback('Archivo de plantilla preparado para compartir.');
    } catch (failure) { setError(errorMessage(failure)); }
  }
  return <Dialog open onOpenChange={open => { if (!open) onClose(); }} title={`Compartir «${prompt.title}»`}
    description="Revisa la plantilla que quieres compartir. El archivo incluye título, contenido, descripción y categoría. Tus valores predeterminados y el historial permanecen privados.">
    {packet ? <section className="prompt-preview" aria-label="Plantilla que se compartirá"><h3>{packet.prompt.title}</h3><p>{packet.prompt.description}</p><pre>{packet.prompt.template}</pre></section>
      : !error && <p role="status">Preparando la plantilla…</p>}
    {error && <p className="error" role="alert">{error}</p>}
    {feedback && <p role="status">{feedback}</p>}
    <div className="dialog-actions"><button type="button" className="secondary" onClick={onClose}>Cancelar</button>
      {error && <button type="button" className="secondary" onClick={onReload}>Actualizar prompt</button>}
      <button type="button" className="primary" disabled={!packet} onClick={download}><AppIcon glyph={Download} size={20} aria-hidden="true" />Descargar plantilla (.json)</button>
    </div>
  </Dialog>;
}
