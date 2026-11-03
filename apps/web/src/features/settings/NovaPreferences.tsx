import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useId, useMemo, useState } from 'react';
import { ApiError, errorMessage } from '../../services/api';
import { type PreferenceClient, type PreparationPreferences as Preferences } from '../../services/preferences';
import { useBoundOperation } from '../../services/useBoundOperation';
import { Check, RefreshCw, Trash2 } from 'lucide-react';
import { IconAction } from '../../components/ui/Icon';

const emptyPreferences: Preferences = { language: null, format: null, providerId: null };

export function NovaPreferences() {
  const { preferences } = useApplicationServices();
  return <NovaPreferenceEditor client={preferences} savedMessage="Preferencias confirmadas en este equipo." />;
}

/** Shared controls over an explicitly scoped port. Private accounts never fall back to local storage. */
export function NovaPreferenceEditor({ client, savedMessage, onExpired }: { client: PreferenceClient; savedMessage: string; onExpired?: () => void }) {
  const prefix = useId();
  const scope = useMemo(() => ({ client }), [client]);
  const operation = useBoundOperation(scope);
  const [preferences, setPreferences] = useState<Preferences>(emptyPreferences);
  const [loaded, setLoaded] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const [error, setError] = useState('');
  const [feedback, setFeedback] = useState('');
  useEffect(() => {
    const controller = new AbortController(); setLoaded(false); setError(''); setFeedback(''); setPreferences(emptyPreferences);
    client.get(controller.signal).then(result => { if (!controller.signal.aborted) { setPreferences(result); setLoaded(true); } })
      .catch(failure => {
        if (!controller.signal.aborted) {
          if (failure instanceof ApiError && failure.status === 401) onExpired?.();
          else setError(errorMessage(failure));
        }
      });
    return () => controller.abort();
  }, [client, refresh, onExpired]);
  async function work(action: (signal: AbortSignal) => Promise<Preferences>, message: string) {
    const pending = operation.begin(); if (!pending) return;
    setError(''); setFeedback('');
    try {
      const result = await action(pending.signal);
      if (operation.isCurrent(pending)) { setPreferences(result); setFeedback(message); }
    } catch (failure) {
      if (operation.isCurrent(pending)) {
        if (failure instanceof ApiError && failure.status === 401) onExpired?.();
        else setError(errorMessage(failure));
      }
    } finally { operation.finish(pending); }
  }
  const busy = operation.busy || !loaded;
  return <>
    <details className="nova-preferences"><summary>Preferencias de NOVA</summary><p className="muted">Solo se conservarán las opciones que confirmes. Puedes eliminarlas cuando quieras. El destino preferido no autoriza una conexión ni cambia el modelo local.</p>
      {!loaded && !error && <p role="status">Cargando preferencias…</p>}
      <div className="prepared-field"><label htmlFor={`${prefix}-language`}>Idioma de respuesta</label><select id={`${prefix}-language`} disabled={busy} value={preferences.language ?? ''} onChange={event => setPreferences(previous => ({ ...previous, language: event.target.value as Preferences['language'] || null }))}><option value="">Según cada solicitud</option><option value="es">Español</option><option value="en">Inglés</option></select></div>
      <div className="prepared-field"><label htmlFor={`${prefix}-format`}>Formato preferido</label><select id={`${prefix}-format`} disabled={busy} value={preferences.format ?? ''} onChange={event => setPreferences(previous => ({ ...previous, format: event.target.value as Preferences['format'] || null }))}><option value="">Según cada solicitud</option>{['JSON', 'Markdown', 'Tabla', 'Texto'].map(format => <option key={format}>{format}</option>)}</select></div>
      <div className="prepared-field"><label htmlFor={`${prefix}-provider`}>Destino preferido</label><select id={`${prefix}-provider`} disabled={busy} value={preferences.providerId ?? ''} onChange={event => setPreferences(previous => ({ ...previous, providerId: event.target.value || null }))}><option value="">Elegir al utilizar</option><option value="ollama">Qwen local</option><option value="chatgpt">ChatGPT</option><option value="claude">Claude</option><option value="gemini">Gemini</option><option value="copilot">Copilot</option><option value="perplexity">Perplexity</option></select></div>
      <div className="dialog-actions"><IconAction className="primary" label="Confirmar preferencias" glyph={Check} disabled={busy} onClick={() => void work(signal => client.save(preferences, true, signal), savedMessage)} /><IconAction className="secondary" label="Eliminar preferencias" glyph={Trash2} disabled={busy} onClick={() => void work(async signal => { await client.forget(signal); return emptyPreferences; }, 'Preferencias eliminadas.')} /></div>
      {!loaded && error && <IconAction className="secondary" label="Reintentar preferencias" glyph={RefreshCw} onClick={() => setRefresh(value => value + 1)} />}
    </details>
    {error && <p className="error" role="alert">{error}</p>}{feedback && <p className="notice" role="status">{feedback}</p>}
  </>;
}
