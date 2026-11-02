import { useId, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import { errorMessage } from '../../services/api';

export function inputNames(text: string): string[] {
  const names = text.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  if (names.length > 60 || new Set(names).size !== names.length) throw new Error('Usa hasta 60 nombres distintos, uno por línea.');
  return names;
}

export function PromptInputNames({ value, onChange, updating, disabled = false }: {
  value: string; onChange: (value: string) => void; updating: boolean; disabled?: boolean;
}) {
  const id = useId();
  return <div className="prepared-field"><label htmlFor={id}>{updating ? 'Variables a agregar' : 'Variables de la plantilla'}</label>
    <textarea id={id} value={value} onChange={event => onChange(event.target.value)} rows={4} maxLength={5000} disabled={disabled} aria-describedby={`${id}-hint`} />
    <p className="muted" id={`${id}-hint`}>Un nombre por línea, sin marcadores ni valores. {updating ? 'Las variables actuales se conservan. Lista vacía: cambiar sólo el texto.' : 'Lista vacía: crear sin variables.'} Las opciones de un campo pertenecen a la descripción de tu petición.</p>
  </div>;
}

export function PromptInputQuestion({ question, updating, onConfirm }: {
  question: string; updating: boolean; onConfirm: (names: string[]) => Promise<void>;
}) {
  const [open, setOpen] = useState(true), [text, setText] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState('');
  return <Dialog open={open} onOpenChange={setOpen} title="Define los campos del prompt" description={question}>
    <form data-nova-command="true" onSubmit={event => {
      event.preventDefault(); if (busy) return; setBusy(true); setError('');
      void (async () => { try { await onConfirm(inputNames(text)); setOpen(false); } catch (failure) { setError(errorMessage(failure)); } finally { setBusy(false); } })();
    }}>
      <PromptInputNames value={text} onChange={setText} updating={updating} disabled={busy} />
      <p>Se conserva tu petición original. Los valores de estas variables se completan cuando uses el prompt.</p>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="dialog-actions"><button type="button" className="secondary" disabled={busy} onClick={() => setOpen(false)}>Ahora no</button>
        <button type="submit" className="primary" disabled={busy}>{busy ? 'Continuando…' : text.trim() ? 'Continuar con estos campos' : updating ? 'Conservar las variables actuales' : 'Continuar sin variables'}</button></div>
    </form>
  </Dialog>;
}
