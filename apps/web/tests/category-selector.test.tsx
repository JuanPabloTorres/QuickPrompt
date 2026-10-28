import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { CategorySelector } from '../src/features/prompts/CategorySelector';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('uses server-owned category IDs and keeps a changed draft value across a fresh render', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([{ id: 0, name: 'General' }, { id: 27, name: 'Programación' }])));
  const change = vi.fn();
  const view = render(<CategorySelector value={27} onChange={change} disabled={false} />);
  await screen.findByRole('option', { name: 'Programación' });
  expect(screen.getByRole('combobox', { name: 'Categoría (opcional)' })).toHaveProperty('value', '27');
  expect(change).not.toHaveBeenCalled();
  fireEvent.change(screen.getByRole('combobox'), { target: { value: '0' } }); expect(change).toHaveBeenCalledWith(0);
  view.rerender(<CategorySelector value={0} onChange={change} disabled />);
  expect(screen.getByRole('combobox')).toHaveProperty('value', '0'); expect(screen.getByRole('combobox')).toHaveProperty('disabled', true);
});
it('a failed catalogue request and its retry cannot replace the existing category or submit a form', async () => {
  let attempts = 0;
  vi.stubGlobal('fetch', vi.fn(async () => ++attempts === 1 ? Response.json({}, { status: 503 }) : Response.json([{ id: 3, name: 'Educación' }, { id: 27, name: 'Programación' }])));
  const submitted = vi.fn();
  function Form() { const [category, setCategory] = useState(27); return <form onSubmit={submitted}><CategorySelector value={category} onChange={setCategory} disabled={false} /></form>; }
  render(<Form />);
  await screen.findByRole('button', { name: 'Reintentar categorías' });
  expect(screen.getByRole('combobox')).toHaveProperty('value', '27');
  fireEvent.click(screen.getByRole('button', { name: 'Reintentar categorías' }));
  await screen.findByRole('option', { name: 'Programación' });
  await waitFor(() => expect(screen.getByRole('combobox')).toHaveProperty('disabled', false));
  expect(screen.getByRole('combobox')).toHaveProperty('value', '27'); expect(submitted).not.toHaveBeenCalled();
});
