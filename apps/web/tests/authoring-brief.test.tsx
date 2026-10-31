import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptAuthoringBrief } from '../src/features/prompts/PromptAuthoringBrief';
import type { AuthoringBriefReview } from '../src/services/live';

afterEach(cleanup);
const uri = 'https://refactoring.guru/design-patterns';
const review: AuthoringBriefReview = { basisHash: 'host-bound-basis', originalRequest: `Conceptualiza software. Aplica Factory y usa ${uri}.`, source: null,
  inputs: ['nombre_proyecto', 'proposito', 'tipo'], proposed: { title: 'Software', description: '', task: 'Conceptualiza la idea; usa Factory si procede.', steps: [],
    constraints: [], outputRequirements: [], references: [{ uri, role: null }] } };

it('requires a human task before requesting a model proposal and explains that no model has run', async () => {
  const confirm = vi.fn().mockResolvedValue(undefined);
  render(<PromptAuthoringBrief runId="establish" sequence={2} review={{ ...review, stage: 'ESTABLISH_TASK', inputs: [],
    proposed: { ...review.proposed, task: '', references: [] } }} identity={confirm} onConfirm={confirm} onClarify={vi.fn()} />);
  const submit = screen.getByRole('button', { name: 'Establecer tarea y pedir propuesta' });
  expect(screen.getByText(/todavía no ha llamado al modelo/)).toBeTruthy();
  fireEvent.click(screen.getByRole('checkbox')); expect(submit).toHaveProperty('disabled', true);
  fireEvent.change(screen.getByLabelText('Tarea que realizará el futuro asistente'), { target: { value: 'Conceptualiza software. Aplica Factory.' } });
  expect(screen.getByRole('checkbox')).toHaveProperty('checked', false);
  fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(submit);
  await waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
  expect(confirm.mock.calls[0][0].content.task).toBe('Conceptualiza software. Aplica Factory.');
});

it('keeps established requirements selected until the user explicitly applies and reviews a suggestion', async () => {
  const established = { ...review.proposed, task: 'Aplica Factory.', references: [] };
  const suggestion = { ...established, task: 'Usa Factory sólo si procede.' };
  const confirm = vi.fn().mockResolvedValue(undefined);
  render(<PromptAuthoringBrief runId="suggestion" sequence={7} review={{ ...review, stage: 'REVIEW_CONTENT',
    proposed: established, establishedTask: established, suggested: suggestion }} identity={confirm} onConfirm={confirm} onClarify={vi.fn()} />);
  const task = screen.getByLabelText('Tarea que realizará el futuro asistente');
  expect(task).toHaveProperty('value', established.task);
  expect(screen.getByText('Mejoras propuestas por NOVA · todavía sin aplicar')).toBeTruthy();
  fireEvent.click(screen.getByRole('checkbox'));
  fireEvent.click(screen.getByRole('button', { name: 'Aplicar propuesta para revisarla' }));
  expect(task).toHaveProperty('value', suggestion.task);
  expect(screen.getByRole('checkbox')).toHaveProperty('checked', false);
  expect(confirm).not.toHaveBeenCalled();
  fireEvent.change(task, { target: { value: 'Aplica Factory sin condición.' } });
  fireEvent.click(screen.getByRole('checkbox'));
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar tarea y componer plantilla' }));
  await waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
  expect(confirm.mock.calls[0][0].content.task).toBe('Aplica Factory sin condición.');
});

it('requires explicit reference roles and review, sends the edited task only, and never claims AI semantic acceptance', async () => {
  const confirm = vi.fn().mockResolvedValue(undefined), clarify = vi.fn();
  render(<PromptAuthoringBrief runId="run-one" sequence={12} review={review} identity={confirm} onConfirm={confirm} onClarify={clarify} />);
  const submit = screen.getByRole('button', { name: 'Confirmar tarea y componer plantilla' });
  expect(submit).toHaveProperty('disabled', true); expect(confirm).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('Tarea que realizará el futuro asistente'), { target: { value: 'Conceptualiza <nombre_proyecto>. Aplica Factory.' } });
  fireEvent.click(screen.getByRole('checkbox')); expect(submit).toHaveProperty('disabled', true);
  fireEvent.change(screen.getByLabelText(`Uso de ${uri}`), { target: { value: 'FUTURE_TASK' } });
  expect(screen.getByRole('checkbox')).toHaveProperty('checked', false);
  fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(submit);
  await waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
  expect(confirm).toHaveBeenCalledWith({ basisHash: review.basisHash, confirmed: true, content: { ...review.proposed,
    task: 'Conceptualiza <nombre_proyecto>. Aplica Factory.', references: [{ uri, role: 'FUTURE_TASK' }] } });
  expect(clarify).not.toHaveBeenCalled(); expect(review.proposed.task).toContain('si procede');
});

it('makes clarification an explicit alternate state and does not compose or save from the reply', async () => {
  const confirm = vi.fn(), clarify = vi.fn().mockResolvedValue(undefined);
  render(<PromptAuthoringBrief runId="run-two" sequence={8} review={review} identity={confirm} onConfirm={confirm} onClarify={clarify} />);
  fireEvent.click(screen.getByRole('button', { name: 'Necesito aclarar el encargo' }));
  expect(screen.queryByLabelText('Tarea que realizará el futuro asistente')).toBeNull();
  fireEvent.change(screen.getByLabelText('Aclaración de tu encargo'), { target: { value: 'Debe evaluar si conviene aceptar o rechazar, no decidirlo antes.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Enviar aclaración' }));
  await waitFor(() => expect(clarify).toHaveBeenCalledWith('Debe evaluar si conviene aceptar o rechazar, no decidirlo antes.'));
  expect(confirm).not.toHaveBeenCalled();
});

it('reviews answer-only references even when the original request contains no link and sends every chosen role', async () => {
  const future = 'https://reference.example.test/lectura?edition=2#resumen';
  const session = 'https://session.example.test/revision';
  const answerReview: AuthoringBriefReview = { ...review, originalRequest: 'Crea un prompt para estudiar.', inputs: [],
    proposed: { ...review.proposed, task: 'Explica el tema de estudio.', references: [{ uri: future, role: null }, { uri: session, role: null }] } };
  const confirm = vi.fn().mockResolvedValue(undefined);
  render(<PromptAuthoringBrief runId="answer-references" sequence={14} review={answerReview} identity={confirm} onConfirm={confirm} onClarify={vi.fn()} />);
  const submit = screen.getByRole('button', { name: 'Confirmar tarea y componer plantilla' });
  fireEvent.change(screen.getByLabelText(`Uso de ${future}`), { target: { value: 'FUTURE_TASK' } });
  fireEvent.click(screen.getByRole('checkbox'));
  expect(submit).toHaveProperty('disabled', true);
  fireEvent.change(screen.getByLabelText(`Uso de ${session}`), { target: { value: 'CURRENT_SESSION' } });
  expect(screen.getByRole('checkbox')).toHaveProperty('checked', false);
  fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(submit);
  await waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
  expect(confirm).toHaveBeenCalledWith({ basisHash: answerReview.basisHash, confirmed: true, content: { ...answerReview.proposed,
    references: [{ uri: future, role: 'FUTURE_TASK' }, { uri: session, role: 'CURRENT_SESSION' }] } });
});

it('preserves edits on rejection and blocks duplicate submission while admission is uncertain', async () => {
  let reject!: (error: Error) => void;
  const confirm = vi.fn(() => new Promise<void>((_, no) => { reject = no; }));
  render(<PromptAuthoringBrief runId="run-three" sequence={9} review={{ ...review, proposed: { ...review.proposed, references: [] } }} identity={confirm} onConfirm={confirm} onClarify={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Tarea que realizará el futuro asistente'), { target: { value: 'Aplica Factory siempre.' } });
  fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(screen.getByRole('button', { name: 'Confirmar tarea y componer plantilla' }));
  expect(screen.getByRole('button', { name: 'Ahora no' })).toHaveProperty('disabled', true);
  fireEvent.submit(screen.getByLabelText('Tarea que realizará el futuro asistente').closest('form')!);
  expect(confirm).toHaveBeenCalledTimes(1); reject(new Error('El origen cambió.'));
  expect((await screen.findByRole('alert')).textContent).toContain('El origen cambió.');
  expect(screen.getByLabelText('Tarea que realizará el futuro asistente')).toHaveProperty('value', 'Aplica Factory siempre.');
});
