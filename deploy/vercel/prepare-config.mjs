import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repository = fileURLToPath(new URL('../../', import.meta.url));
const template = new URL('../../apps/web/vercel.json.template', import.meta.url);
const placeholder = 'https://REPLACE_BACKEND_ORIGIN.invalid';

/** Resolve only the fixed backend origin; the edge secret stays an environment reference. */
export function resolvedVercelConfig(backendOrigin) {
  const backend = new URL(backendOrigin);
  if (backend.protocol !== 'https:' || backend.username || backend.password || backend.pathname !== '/' || backend.search || backend.hash
    || backend.hostname.includes('*') || backend.hostname.endsWith('.invalid') || ['localhost', '127.0.0.1', '[::1]'].includes(backend.hostname)) {
    throw new Error('Supply the real HTTPS backend origin without credentials, a path, query or fragment.');
  }
  return JSON.parse(fs.readFileSync(template, 'utf8').replaceAll(placeholder, backend.origin));
}

/** Write a fresh staging configuration, never a tracked frontend configuration or secret literal. */
export function prepareVercelConfig(backendOrigin, output) {
  const target = path.resolve(output);
  const relative = path.relative(repository, target);
  const inRepository = relative === '' || !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
  if (path.basename(target) !== 'vercel.json' || inRepository && !relative.startsWith(`.playwright${path.sep}`)) {
    throw new Error('Write vercel.json to an explicit private staging directory outside tracked source.');
  }
  const config = resolvedVercelConfig(backendOrigin);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, JSON.stringify(config, null, 2) + '\n', { flag: 'wx', mode: 0o600 });
  return target;
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
  const args = process.argv.slice(2);
  if (args.length !== 4 || args[0] !== '--backend-origin' || args[2] !== '--output')
    throw new Error('Usage: node deploy/vercel/prepare-config.mjs --backend-origin https://BACKEND --output PRIVATE_STAGING/vercel.json');
  console.log(prepareVercelConfig(args[1], args[3]));
}
