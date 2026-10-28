import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptAuthoringBrief } from '../src/features/prompts/PromptAuthoringBrief';
import type { AuthoringBriefReview } from '../src/services/live';

afterEach(cleanup);
const uri = 'https://refactoring.guru/design-patterns';
const review: AuthoringBriefReview = { basisHash: 'host-bound-basis', originalRequest: `Conceptualiza software. Aplica Factory y usa ${uri}.`, source: null,
  inputs: ['nombre_proyecto', 'proposito', 'tipo'], proposed: { title: 'Software', description: '', task: 'Conceptualiza la idea; usa Factory si procede.', steps: [],
    constraints: [], outputRequirements: [], references: [{ uri, role: null }] } };

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
