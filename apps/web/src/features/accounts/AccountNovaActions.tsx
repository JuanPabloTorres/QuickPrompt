import { useState } from 'react';
import type { AccountPrompt } from '../../services/accounts';
import { errorMessage } from '../../services/api';
import type { LiveCommand } from '../../services/live';
import { PromptInputNames, inputNames } from '../prompts/PromptInputNames';
import type { AccountAgentBinding } from './AccountNovaBoundary';

/** Explicit choices compose the native command contract; free text never selects permissions. */
export function AccountNovaActions({ agent, prompts, source, unavailable }: {
  agent: AccountAgentBinding; prompts: AccountPrompt[]; source?: AccountPrompt; unavailable: boolean;
}) {
  const [action, setAction] = useState<'CREATE_DRAFT' | 'PROPOSE_ADAPTATION' | 'OPEN_PROMPT'>('CREATE_DRAFT');
  const [selected, setSelected] = useState('');
  const [details, setDetails] = useState('');
  const [names, setNames] = useState('');
  const [error, setError] = useState('');
  if (!agent.services) return null;
  const target = action === 'CREATE_DRAFT' ? undefined : source ?? prompts.find(prompt => prompt.id === selected);
  const disabled = agent.busy || unavailable;
  function submit() {
    if (disabled) return;
    try {
      if (action !== 'CREATE_DRAFT' && !target) throw new Error('Elige un prompt de esta biblioteca.');
      const message = action === 'OPEN_PROMPT' ? 'Abre el prompt seleccionado para leerlo, sin modificarlo ni ejecutar su tarea.' : details.trim();
      if (!message) throw new Error('Describe el contenido o los cambios que necesitas.');
      const command: LiveCommand = { operation: action, content: message,
        ...(action === 'OPEN_PROMPT' ? {} : { inputNames: inputNames(names) }) };
      setError(''); void agent.begin(message, command, target);
    } catch (failure) { setError(errorMessage(failure)); }
  }
  return <section className="account-remembered" aria-label="Acciones privadas de NOVA">
    <h3>Trabaja con NOVA</h3><p className="muted">Elige la acción. NOVA trabaja en esta pantalla; cualquier guardado requiere revisar y aprobar la propuesta.</p>
    <form className="account-prompt-form" data-nova-command="true" onSubmit={event => { event.preventDefault(); submit(); }}>
      <fieldset disabled={disabled}><label htmlFor="account-nova-action">Acción de NOVA</label><select id="account-nova-action" value={action} onChange={event => { setAction(event.target.value as typeof action); setError(''); }}>
        <option value="CREATE_DRAFT">Crear y revisar un prompt</option><option value="PROPOSE_ADAPTATION">Actualizar y revisar un prompt</option><option value="OPEN_PROMPT">Abrir un prompt</option>
      </select>
        {action !== 'CREATE_DRAFT' && (source ? <p>Origen: {source.title} · versión {source.revision}</p> : <><label htmlFor="account-nova-source">Prompt de origen</label><select id="account-nova-source" value={selected} onChange={event => setSelected(event.target.value)}>
          <option value="">Elige un prompt</option>{prompts.map(prompt => <option key={prompt.id} value={prompt.id}>{prompt.title} · versión {prompt.revision}</option>)}
        </select></>)}
        {action !== 'OPEN_PROMPT' && <><label htmlFor="account-nova-request">{action === 'CREATE_DRAFT' ? 'Qué prompt necesitas' : 'Cambios solicitados'}</label><textarea id="account-nova-request" rows={4} maxLength={16000} value={details} onChange={event => setDetails(event.target.value)} />
          <PromptInputNames value={names} onChange={setNames} updating={action === 'PROPOSE_ADAPTATION'} disabled={disabled} /></>}
      </fieldset>
      {unavailable && <p className="muted">Cierra el diálogo actual antes de iniciar otra acción. Tu borrador se conserva.</p>}
      {error && <p role="alert" className="error">{error}</p>}
      <button className="primary" disabled={disabled || action !== 'CREATE_DRAFT' && !target || action !== 'OPEN_PROMPT' && !details.trim()}>{agent.busy ? 'NOVA está trabajando…' : action === 'OPEN_PROMPT' ? 'Abrir con NOVA' : 'Preparar con NOVA'}</button>
    </form>
  </section>;
}
