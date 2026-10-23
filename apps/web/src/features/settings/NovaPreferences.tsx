import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useState } from 'react';
import { errorMessage } from '../../services/api';
import { type PreparationPreferences as Preferences } from '../../services/preferences';

export function NovaPreferences() {
  const { preferences: preferenceClient } = useApplicationServices();
  const [preferences, setPreferences] = useState<Preferences>({ language: null, format: null, providerId: null });
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [feedback, setFeedback] = useState('');
  useEffect(() => { const controller = new AbortController(); preferenceClient.get(controller.signal).then(result => { if (!controller.signal.aborted) setPreferences(result); }).catch(error => { if (!controller.signal.aborted) setError(errorMessage(error)); }); return () => controller.abort(); }, []);
  async function work(action: () => Promise<void>) { setBusy(true); setError(''); setFeedback(''); try { await action(); } catch (error) { setError(errorMessage(error)); } finally { setBusy(false); } }
  return <>
    <details><summary>Preferencias de NOVA</summary><p className="muted">Solo se conservarán las opciones que confirmes. Puedes eliminarlas cuando quieras.</p>
      <div className="prepared-field"><label htmlFor="nova-language">Idioma de respuesta</label><select id="nova-language" disabled={busy} value={preferences.language ?? ''} onChange={event => setPreferences(previous => ({ ...previous, language: event.target.value as Preferences['language'] || null }))}><option value="">Según cada solicitud</option><option value="es">Español</option><option value="en">Inglés</option></select></div>
      <div className="prepared-field"><label htmlFor="nova-format">Formato preferido</label><select id="nova-format" disabled={busy} value={preferences.format ?? ''} onChange={event => setPreferences(previous => ({ ...previous, format: event.target.value as Preferences['format'] || null }))}><option value="">Según cada solicitud</option>{['JSON', 'Markdown', 'Tabla', 'Texto'].map(format => <option key={format}>{format}</option>)}</select></div>
      <div className="prepared-field"><label htmlFor="nova-provider">Destino preferido</label><select id="nova-provider" disabled={busy} value={preferences.providerId ?? ''} onChange={event => setPreferences(previous => ({ ...previous, providerId: event.target.value || null }))}><option value="">Elegir al utilizar</option><option value="ollama">Qwen local</option><option value="chatgpt">ChatGPT</option><option value="claude">Claude</option><option value="gemini">Gemini</option><option value="copilot">Copilot</option><option value="perplexity">Perplexity</option></select></div>
      <div className="dialog-actions"><button className="secondary" disabled={busy} onClick={() => void work(async () => { setPreferences(await preferenceClient.save(preferences, true)); setFeedback('Preferencias confirmadas en este equipo.'); })}>Confirmar preferencias</button><button className="text-button" disabled={busy} onClick={() => void work(async () => { await preferenceClient.forget(); setPreferences({ language: null, format: null, providerId: null }); setFeedback('Preferencias eliminadas.'); })}>Eliminar preferencias</button></div>
    </details>
    {error && <p className="error" role="alert">{error}</p>}{feedback && <p className="notice" role="status">{feedback}</p>}
  </>;
}
