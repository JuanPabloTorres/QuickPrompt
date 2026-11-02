import { expect, it, vi } from 'vitest';
import { commitAccountProposal } from '../src/services/accountNovaCommit';
import { createAccountService } from '../src/services/accounts';
import { ApiError } from '../src/services/api';

const owner = '10000000-0000-4000-8000-000000000001';
const library = '10000000-0000-4000-8000-000000000002';
const target = '10000000-0000-4000-8000-000000000003';
const draft = { title: 'Aprobado', templateText: 'Explica <tema>.', description: 'Descripción aprobada', categoryId: 0 };
function setup() {
  const accounts = createAccountService(); const ports = accounts.nova(owner, library, target);
  const failure = new TypeError('Respuesta perdida después del guardado.');
  ports.live.commit = vi.fn().mockRejectedValue(failure);
  const run = { runId: owner, sessionId: owner, mode: 'SAME_TAB' as const, status: 'TOOL_RUNNING' as const,
    sequence: 7, targetId: target as string | null, revision: 1, pauseRequested: false, browserOpen: true, approval: 'CONSUMED', effectCommitted: true, errorCode: null };
  ports.live.record = vi.fn(async () => ({ run }));
  accounts.prompt = vi.fn(async () => ({ id: target, libraryId: library, title: draft.title, template: draft.templateText, revision: 1 }));
  return { ports, accounts, failure, run };
}
it('resolves a lost commit by reading the same run and exact private version, without another POST', async () => {
  const test = setup(); const signal = new AbortController().signal;
  const saved = await commitAccountProposal(test.ports, test.accounts, library, owner, draft, signal);
  expect(saved.id).toBe(target); expect(saved.revision).toBe(1);
  expect(test.ports.live.commit).toHaveBeenCalledExactlyOnceWith(owner, draft, signal);
  expect(test.ports.live.record).toHaveBeenCalledExactlyOnceWith(owner, expect.any(AbortSignal));
  expect(test.accounts.prompt).toHaveBeenCalledExactlyOnceWith(library, target, expect.any(AbortSignal));
});
it.each(['uncommitted', 'pending', 'foreign-run', 'missing-target'])('does not turn an unconfirmed receipt into success (%s)', async condition => {
  const test = setup();
  if (condition === 'uncommitted') test.run.effectCommitted = false;
  if (condition === 'pending') test.run.approval = 'PENDING';
  if (condition === 'foreign-run') test.run.runId = target;
  if (condition === 'missing-target') test.run.targetId = null;
  await expect(commitAccountProposal(test.ports, test.accounts, library, owner, draft, new AbortController().signal)).rejects.toBe(test.failure);
  expect(test.accounts.prompt).not.toHaveBeenCalled(); expect(test.ports.live.commit).toHaveBeenCalledOnce();
});
it('rejects a newer revision rather than substituting it for the committed artifact', async () => {
  const test = setup(); vi.mocked(test.accounts.prompt).mockResolvedValue({ id: target, libraryId: library, title: draft.title, template: 'Otra versión.', revision: 2 });
  await expect(commitAccountProposal(test.ports, test.accounts, library, owner, draft, new AbortController().signal)).rejects.toBe(test.failure);
  expect(test.ports.live.commit).toHaveBeenCalledOnce();
});
it.each([400, 401, 409])('preserves a known rejection without making recovery reads (%s)', async status => {
  const test = setup(); const rejected = new ApiError('Rechazado', status); vi.mocked(test.ports.live.commit).mockRejectedValue(rejected);
  await expect(commitAccountProposal(test.ports, test.accounts, library, owner, draft, new AbortController().signal)).rejects.toBe(rejected);
  expect(test.ports.live.record).not.toHaveBeenCalled(); expect(test.accounts.prompt).not.toHaveBeenCalled();
});
it('discards recovery after leaving the owning context', async () => {
  const test = setup(); const controller = new AbortController(); controller.abort();
  await expect(commitAccountProposal(test.ports, test.accounts, library, owner, draft, controller.signal)).rejects.toBe(test.failure);
  expect(test.ports.live.record).not.toHaveBeenCalled();
});

it('propagates an expired identity discovered while reading the committed source', async () => {
  const test = setup(); vi.mocked(test.accounts.prompt).mockRejectedValue(new ApiError('Sesión vencida', 401));
  await expect(commitAccountProposal(test.ports, test.accounts, library, owner, draft, new AbortController().signal)).rejects.toMatchObject({ status: 401 });
  expect(test.ports.live.commit).toHaveBeenCalledOnce();
});
