// @vitest-environment node
import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const config = JSON.parse(readFileSync(new URL('../vercel.json.template', import.meta.url), 'utf8'));
const entry = config.routes.find((route: { dest?: string; methods?: string[] }) => route.dest === '/index.html' && route.methods);
const forwarding = config.routes.find((route: { src?: string }) => route.src === '^/(api|account|connect)(/.*)?$');

it.each(['login', 'register', 'recover', 'resend', 'confirm', 'reset'])('serves %s as HTML on GET/HEAD while preserving API POST forwarding', mode => {
  const path = `/account/${mode}`;
  expect(new RegExp(entry.src).test(path)).toBe(true);
  expect(entry.methods).toEqual(['GET', 'HEAD']);
  expect(new RegExp(forwarding.src).test(path)).toBe(true);
  expect(forwarding.methods).toBeUndefined();
  expect(config.routes.indexOf(entry)).toBeLessThan(config.routes.indexOf(forwarding));
});
