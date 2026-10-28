import { afterEach, expect, it, vi } from 'vitest';
import { accountEntryHtml } from '../vite.config';

afterEach(() => vi.unstubAllEnvs());
const page = '<!doctype html><html><head><title>QuickPrompt</title></head><body><div id="root"></div></body></html>';

it('keeps the local entry point unchanged unless the deployment explicitly selects accounts', () => {
  vi.stubEnv('QUICKPROMPT_WEB_MODE', '');
  expect(accountEntryHtml(page)).toBe(page);
  expect(accountEntryHtml(page, 'local')).toBe(page);
});

it('adds exactly one accounts declaration and never interpolates private proxy environment values', () => {
  vi.stubEnv('ORIGIN_SECRET', 'SYNTHETIC_PRIVATE_ORIGIN_SECRET');
  const marked = accountEntryHtml(page, 'accounts');
  expect(marked).toContain('<meta name="quickprompt-mode" content="accounts" />');
  expect(accountEntryHtml(marked, 'accounts')).toBe(marked);
  expect(marked).not.toContain('SYNTHETIC_PRIVATE_ORIGIN_SECRET');
});

it.each(['cloud', 'Accounts', 'accounts '])('rejects an unknown deployment mode %s', mode => {
  expect(() => accountEntryHtml(page, mode)).toThrow('QUICKPROMPT_WEB_MODE');
});

it('rejects conflicting mode markers, duplicates and missing head rather than booting another application', () => {
  expect(() => accountEntryHtml(page.replace('<head>', '<head><meta name="quickprompt-mode" content="local">'), 'accounts')).toThrow('conflicting');
  const twice = page.replace('<head>', '<head><meta name="quickprompt-mode" content="accounts"><meta name="quickprompt-mode" content="accounts">');
  expect(() => accountEntryHtml(twice, 'accounts')).toThrow('duplicate');
  expect(() => accountEntryHtml('<body>Missing entry metadata</body>', 'accounts')).toThrow('head element');
});
