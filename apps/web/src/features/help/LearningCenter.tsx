import { useApplicationServices } from '../../components/ServiceContext';
import { useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';


const lessons = [
  ['01-biblioteca', '1. Usar un prompt de la biblioteca', 'Elige una colección, abre una tarea, completa tus datos y usa Qwen para obtener una respuesta.'],
  ['02-nova', '2. Pedirle un prompt a NOVA', 'Escribe lo que necesitas. NOVA busca o prepara la instrucción y tú decides si guardarla.'],
  ['03-variables', '3. Crear y quitar variables con un botón', 'Selecciona una palabra o frase del contenido. El mismo botón la convierte en variable o la devuelve a texto.'],
];
export function LearningCenter({ onClose, onImported }: { onClose: () => void; onImported: () => void }) {
  const { archives } = useApplicationServices();
  const [busy, setBusy] = useState(false); const [feedback, setFeedback] = useState(''); const [error, setError] = useState('');
  async function addLibrary() {
    if (busy) return; setBusy(true); setError('');
    try {
      const response = await fetch('/library/biblioteca-practica.es.json'); if (!response.ok) throw new Error('No se pudo abrir la biblioteca práctica.');
      const data: unknown = await response.json();
      const preview = await archives.inspect(data);
      if (preview.conflicts) throw new Error('Hay versiones diferentes en tu biblioteca. Revisa el archivo en Datos y respaldos antes de añadirlo.');
      const result = await archives.import({ data, confirmed: true });
      setFeedback(result.imported ? `${result.imported} prompts añadidos. Cierra esta guía y elige una colección.` : 'Ya tienes estos prompts. No se añadieron duplicados.'); onImported();
    } catch (error) { setError(errorMessage(error)); } finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => !open && !busy && onClose()} title="Empieza aquí: guía y videos" description="Un prompt es una instrucción que le das a una IA. QuickPrompt te ayuda a guardarla, completar tus datos y reutilizarla.">
    <ol className="learning-steps"><li><strong>Elige una tarea.</strong> En Tu biblioteca, selecciona una colección y abre un prompt. Si la lista está filtrada, vacía el campo «Buscar en la biblioteca».</li><li><strong>Completa tus datos.</strong> Por ejemplo, pega el documento o escribe el destinatario del correo. Eso es lo que significan las variables.</li><li><strong>Obtén la respuesta.</strong> Pulsa Usar en… y Ejecutar en este equipo para usar Qwen. O copia la instrucción y pégala en tu IA habitual.</li></ol>
    <p><strong>NOVA prepara el prompt.</strong> Si escribes una intención arriba, verás la instrucción preparada. Para que la IA responda a esa instrucción, pulsa Usar en… No necesitas abrir Nuevo prompt para empezar.</p>
    <section className="learning-library"><h3>60 prompts para tareas reales</h3><p>20 colecciones: correos, estudio, programación, trabajo, ventas, documentos, cocina, viajes y más. Puedes añadirlos otra vez sin duplicar ni reemplazar tus prompts.</p><button className="primary" disabled={busy} onClick={addLibrary}>{busy ? 'Añadiendo…' : 'Añadir biblioteca práctica'}</button> <a href="/library/biblioteca-practica.es.json" download>Descargar biblioteca</a></section>
    {feedback && <p className="notice" role="status">{feedback}</p>}{error && <p className="error" role="alert">{error}</p>}
    <p className="muted">Videos cortos de la aplicación real, con texto explicativo en español. No necesitan sonido. Puedes pausar, ampliar o repetir cada paso.</p>
    <div className="lesson-list">{lessons.map(([id, title, description]) => <section className="lesson" key={id}><h3>{title}</h3><p className="muted">{description}</p><video controls preload="none" poster={`/tutoriales/${id}.png`} aria-label={title}><source src={`/tutoriales/${id}.webm`} type="video/webm" /><track kind="captions" src={`/tutoriales/${id}.vtt`} srcLang="es" label="Español" default />Tu navegador no reproduce este video. <a href={`/tutoriales/${id}.webm`}>Descargar video</a></video><div className="lesson-actions"><button className="secondary" aria-label={`Ampliar: ${title}`} onClick={event => { const video = event.currentTarget.closest('section')?.querySelector('video'); if (video) video.requestFullscreen().catch(() => setError('Utiliza el control de pantalla completa del reproductor.')); }}>Ampliar video</button><a href={`/tutoriales/${id}.webm`} download>Descargar este video</a></div></section>)}</div>
    <button className="secondary" onClick={onClose} disabled={busy}>Volver a mis prompts</button>
  </Dialog>;
}
