import { useId, useState } from 'react';
import { Dialog } from '../../components/ui/Dialog';
import type { LocalCommand } from '../../services/commands';
import type { Prompt } from '../../services/api';

export function CommandDialog({ command, busy, error, onChoose, onResolve, onClose, onReturnFocus }: {
  command: LocalCommand; busy: boolean; error: string;
  onChoose: (prompt: Prompt, instruction: string) => void; onResolve: (title: string) => void;
  onClose: () => void; onReturnFocus: () => void;
}) {
  const [text, setText] = useState(''); const id = useId();
  const needsChange = command.kind === 'UPDATE' && command.candidates.length === 1;
  return <Dialog open onOpenChange={open => !open && onClose()} title={needsChange ? '¿Qué quieres cambiar?' : command.candidates.length > 1 ? 'Encontré varios prompts' : '¿Qué prompt buscas?'} description={command.message} onCloseAutoFocus={event => { event.preventDefault(); onReturnFocus(); }}>
    {command.candidates.length > 1 ? <div className="command-candidates">{command.candidates.map(prompt => <button className="suggestion-candidate" key={prompt.id} disabled={busy} aria-label={prompt.title} onClick={() => onChoose(prompt, command.instruction)}><span><strong>{prompt.title}</strong><span className="command-description">{prompt.description || `Versión ${prompt.revision}`}</span></span></button>)}</div> : <form onSubmit={event => { event.preventDefault(); if (text.trim()) { if (needsChange) onChoose(command.candidates[0], text); else onResolve(text); } }}>
      {needsChange && <p className="muted">{command.candidates[0].title}</p>}
      <label htmlFor={id}>{needsChange ? 'Cambio que necesitas' : 'Título del prompt'}</label>
      {needsChange ? <textarea id={id} value={text} onChange={event => setText(event.target.value)} maxLength={4000} rows={4} disabled={busy} autoFocus /> : <input id={id} value={text} onChange={event => setText(event.target.value)} maxLength={160} disabled={busy} autoFocus />}
      <div className="dialog-actions"><button type="button" className="text-button" onClick={onClose}>Cancelar</button><button className="primary" disabled={busy || !text.trim()}>{busy ? 'Preparando cambio…' : needsChange ? 'Preparar cambio' : 'Buscar prompt'}</button></div>
    </form>}
    {busy && <p role="status">Procesando en este equipo…</p>}{error && <p role="alert" className="error">{error}</p>}
  </Dialog>;
}
