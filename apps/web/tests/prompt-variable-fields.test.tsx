import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PromptVariableFields } from '../src/features/prompts/PromptVariableFields';

afterEach(cleanup);

it('leaves values with the caller and preserves an explicit empty value instead of restoring a default', () => {
  const values = Object.freeze({});
  const defaults = Object.freeze({ tema: 'valor inicial' });
  const change = vi.fn();
  const props = { names: ['tema'], values, defaults, idPrefix: 'editor-variable', onValueChange: change };
  const view = render(<PromptVariableFields {...props} />);
  expect(screen.getByLabelText('tema')).toHaveProperty('value', 'valor inicial');
  fireEvent.change(screen.getByLabelText('tema'), { target: { value: 'α\n<contenido literal>' } });
  expect(change).toHaveBeenLastCalledWith('tema', 'α\n<contenido literal>');
  expect(values).toEqual({});
  view.rerender(<PromptVariableFields {...props} values={{ tema: '' }} />);
  expect(screen.getByLabelText('tema')).toHaveProperty('value', '');
});

it('keeps command-dialog fields distinct from the workspace targets used by the visible agent', () => {
  const workspaceChange = vi.fn(), commandChange = vi.fn();
  render(<>
    <PromptVariableFields names={['tema']} values={{ tema: 'origen' }} idPrefix="variable" workspace onValueChange={workspaceChange} />
    <PromptVariableFields names={['tema']} values={{}} idPrefix="command-value" onValueChange={commandChange} />
  </>);
  const fields = screen.getAllByLabelText('tema');
  expect(fields[0].id).not.toBe(fields[1].id);
  expect(screen.getAllByTestId('variable-field')).toEqual([fields[0]]);
  fireEvent.change(fields[1], { target: { value: 'sólo comando' } });
  expect(commandChange).toHaveBeenCalledWith('tema', 'sólo comando');
  expect(workspaceChange).not.toHaveBeenCalled();
});
