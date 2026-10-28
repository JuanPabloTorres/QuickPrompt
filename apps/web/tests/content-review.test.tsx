import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ContentReview } from '../src/features/nova/ContentReview';
import { contentReviewSchema, type ContentReview as Review } from '../src/services/live';

afterEach(cleanup);

const review: Review = {
  status: 'REQUIRES_HUMAN_CONTENT_REVIEW', contractHash: 'contract-fixture', bodyHash: 'body-fixture', originalRequest: 'Cambia tres por cinco. Conserva <tema>.',
  source: { promptId: '10000000-0000-4000-8000-000000000001', revision: 3, bodyHash: 'source-fixture', originalText: 'Describe tres opciones para <tema>.' },
  ledger: [{ id: 1, source: 'SOURCE', start: 0, length: 34, text: 'Describe tres opciones para <tema>.', role: 'CONTEXT', replacementText: null, changeAuthority: [] }],
  literalChanges: { entries: [
    { id: 'missing-clause', kind: 'MISSING_SOURCE_CLAUSE', originalText: 'No inventes datos.', proposedText: null },
    { id: 'removed-uri', kind: 'REMOVED_SOURCE_URI', originalText: 'https://example.test/original', proposedText: null },
    { id: 'added-uri', kind: 'ADDED_URI', originalText: null, proposedText: 'https://example.test/proposed' },
  ] },
};

it('shows original work and literal human answers separately as escaped data', () => {
  const answer = '<img src=x onerror="alert(1)">';
  render(<ContentReview review={{ ...review, userClarifications: [{ question: '¿Qué clase de software?', answer }] }} />);
  expect(screen.getByText(review.originalRequest)).toBeTruthy();
  expect(screen.getByText('Tus aclaraciones')).toBeTruthy();
  expect(screen.getByText('¿Qué clase de software?')).toBeTruthy();
  expect(screen.getByText(answer)).toBeTruthy(); expect(document.querySelector('img')).toBeNull();
});

it('presents original material and literal differences as escaped text without semantic interpretations', () => {
  const maliciousText = '<img src=x onerror="alert(1)">';
  render(<ContentReview review={{ ...review, originalRequest: maliciousText, ledger: [{ ...review.ledger[0], text: maliciousText }],
    literalChanges: { entries: [{ ...review.literalChanges.entries[0], originalText: maliciousText }] } }} />);
  const section = screen.getByRole('region', { name: 'Revisión del contenido' });
  expect(within(section).getAllByText(maliciousText)).toHaveLength(3);
  expect(section.querySelector('img')).toBeNull(); expect(section.querySelector('script')).toBeNull();
  expect(within(section).queryByText('Cómo se interpretó el encargo')).toBeNull();
  expect(within(section).getByText('Material de referencia')).toBeTruthy();
  expect(section.textContent).toContain('Esta comparación señala texto y enlaces que cambiaron; no determina si el significado se conserva.');
  fireEvent.click(within(section).getByText('Texto de origen · versión 3'));
  expect(within(section).getAllByText('Describe tres opciones para <tema>.')).toHaveLength(1);
});

it('provides one controlled acknowledgement for each literal difference and no global checkbox', () => {
  const changed = vi.fn();
  render(<ContentReview review={review} approvedLiteralChangeIds={['removed-uri']} onApproveLiteralChange={changed} />);
  const checkboxes = screen.getAllByRole('checkbox');
  expect(checkboxes).toHaveLength(3); expect(checkboxes.map(box => (box as HTMLInputElement).checked)).toEqual([false, true, false]);
  expect(screen.getByText('Fragmento del original que no aparece literalmente')).toBeTruthy();
  expect(screen.getByText('Enlace del original que no aparece')).toBeTruthy();
  expect(screen.getByText('Enlace añadido a la propuesta')).toBeTruthy();
  fireEvent.click(checkboxes[0]); expect(changed).toHaveBeenLastCalledWith('missing-clause', true);
  fireEvent.click(checkboxes[1]); expect(changed).toHaveBeenLastCalledWith('removed-uri', false);
  expect(screen.queryByRole('link')).toBeNull();
});

it('keeps differences read-only when the proposal is not controllable', () => {
  const changed = vi.fn();
  render(<ContentReview review={review} onApproveLiteralChange={changed} disabled />);
  for (const checkbox of screen.getAllByRole('checkbox')) { expect(checkbox).toHaveProperty('disabled', true); fireEvent.click(checkbox); }
  expect(changed).not.toHaveBeenCalled();
});

it('validates identity-bearing difference records and rejects obsolete semantic roles', () => {
  expect(contentReviewSchema.parse(review)).toEqual(review);
  expect(contentReviewSchema.safeParse({ ...review, contractHash: undefined }).success).toBe(false);
  expect(contentReviewSchema.safeParse({ ...review, literalChanges: undefined }).success).toBe(false);
  expect(contentReviewSchema.safeParse({ ...review, literalChanges: { entries: [review.literalChanges.entries[0], review.literalChanges.entries[0]] } }).success).toBe(false);
  expect(contentReviewSchema.safeParse({ ...review, ledger: [{ ...review.ledger[0], role: 'FUTURE_TASK' }] }).success).toBe(false);
  expect(contentReviewSchema.safeParse({ ...review, ledger: [{ ...review.ledger[0], start: -1 }] }).success).toBe(false);
});

it('presents the server-masked case-data material without treating it as template content', () => {
  const masked = { ...review, ledger: [{ ...review.ledger[0], text: '••••••', role: 'INPUT_DATA' as const }], literalChanges: { entries: [] } };
  expect(contentReviewSchema.safeParse(masked).success).toBe(true);
  render(<ContentReview review={masked} />);
  fireEvent.click(screen.getByText('Material de referencia'));
  expect(screen.getByText('Datos del caso (excluidos de la plantilla)')).toBeTruthy(); expect(screen.getByText('••••••')).toBeTruthy();
  expect(screen.queryByRole('checkbox')).toBeNull();
});

it('distinguishes the saved source from a changed working draft and avoids duplicate source bodies', () => {
  const working = { ...review, source: { ...review.source, workingDraftId: '10000000-0000-4000-8000-000000000002',
    originalText: 'Borrador que todavía no se guardó.', savedOriginalText: 'Original guardado. No inventes datos.' } };
  const mounted = render(<ContentReview review={working} />);
  fireEvent.click(screen.getByText('Borrador de trabajo')); fireEvent.click(screen.getByText('Versión guardada de origen · versión 3'));
  expect(screen.getByText(working.source.originalText)).toBeTruthy(); expect(screen.getByText(working.source.savedOriginalText)).toBeTruthy();
  mounted.rerender(<ContentReview review={{ ...working, source: { ...working.source, savedOriginalText: working.source.originalText } }} />);
  expect(screen.queryByText('Versión guardada de origen · versión 3')).toBeNull();
});
