import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { isDeepStrictEqual } from 'node:util';
import { resolvedVercelConfig } from './prepare-config.mjs';

const repository = fileURLToPath(new URL('../../', import.meta.url));
const metadataPaths = ['openid-configuration', 'oauth-authorization-server', 'oauth-protected-resource/mcp'];
const endpointFields = ['authorization_endpoint', 'token_endpoint', 'revocation_endpoint', 'device_authorization_endpoint', 'jwks_uri'];

function publicOrigin(value) {
  resolvedVercelConfig(value); // Reuse the fixed, credential-free HTTPS origin boundary.
  return new URL(value).origin;
}

function exactPublicUrl(value, origin) {
  if (typeof value !== 'string') throw new Error('Public metadata requires a URL.');
  const url = new URL(value);
  if (url.origin !== origin || url.username || url.password || url.search || url.hash)
    throw new Error('Public metadata contains a URL outside the configured public origin.');
  return value;
}

function stringList(value) {
  if (!Array.isArray(value) || value.length > 100 || value.some(item => typeof item !== 'string' || !item || item.length > 200)
    || new Set(value).size !== value.length) throw new Error('Public metadata contains an invalid supported-values list.');
  return value;
}

/** Keep only public discovery fields already advertised by the live owner; invent no scopes or methods. */
export function validatedMetadata(documents, originValue) {
  const origin = publicOrigin(originValue);
  if (!Array.isArray(documents) || documents.length !== 3) throw new Error('All three public metadata documents are required.');
  const discoveries = documents.slice(0, 2).map(document => {
    if (!document || typeof document !== 'object' || Array.isArray(document)) throw new Error('Invalid discovery document.');
    const issuer = exactPublicUrl(document.issuer, origin);
    if (new URL(issuer).pathname !== '/') throw new Error('Discovery issuer must be the exact public origin.');
    const result = { issuer };
    for (const field of endpointFields) if (field in document) result[field] = exactPublicUrl(document[field], origin);
    if (result.jwks_uri !== `${origin}/connect/jwks` || result.token_endpoint !== `${origin}/connect/token`)
      throw new Error('Discovery must use the dynamic public JWKS and token endpoints.');
    for (const [field, value] of Object.entries(document)) {
      if (field.endsWith('_supported')) result[field] = typeof value === 'boolean' ? value : stringList(value);
    }
    return result;
  });
  if (!isDeepStrictEqual(discoveries[0], discoveries[1]))
    throw new Error('The two discovery documents disagree; obtain a consistent backend snapshot.');
  const resource = documents[2];
  if (!resource || typeof resource !== 'object' || resource.resource !== `${origin}/mcp`)
    throw new Error('Protected resource metadata must describe the configured MCP resource.');
  const servers = stringList(resource.authorization_servers);
  if (servers.length !== 1 || exactPublicUrl(servers[0], origin) !== discoveries[0].issuer)
    throw new Error('Protected resource metadata has another authorization server.');
  const methods = stringList(resource.bearer_methods_supported);
  if (methods.length !== 1 || methods[0] !== 'header') throw new Error('MCP bearer methods must remain header-only.');
  const scopes = stringList(resource.scopes_supported);
  if (!scopes.length || !Array.isArray(discoveries[0].scopes_supported)
    || scopes.some(scope => !discoveries[0].scopes_supported.includes(scope)))
    throw new Error('MCP scopes must be advertised by the actual authorization server.');
  if (typeof resource.resource_name !== 'string' || resource.resource_name.length > 200)
    throw new Error('Protected resource metadata requires its public resource name.');
  return [...discoveries, { resource: resource.resource, authorization_servers: servers,
    bearer_methods_supported: methods, scopes_supported: scopes, resource_name: resource.resource_name }];
}

/** Fetch fixed public metadata through the authenticated ingress; never follow redirects with the edge secret. */
export async function readMetadata(backendValue, originValue, secret, fetcher = fetch) {
  const backend = publicOrigin(backendValue);
  if (!/^[a-fA-F0-9]{64}$/.test(secret ?? '')) throw new Error('ORIGIN_SECRET must be configured privately as 64 hexadecimal characters.');
  const documents = [];
  for (const suffix of metadataPaths) {
    const response = await fetcher(`${backend}/.well-known/${suffix}`, { redirect: 'error', signal: AbortSignal.timeout(15000),
      headers: { 'X-QP-Origin-Secret': secret, Accept: 'application/json' } });
    if (!response.ok || !response.headers.get('content-type')?.includes('application/json'))
      throw new Error('The backend did not return public JSON metadata; response content omitted.');
    if (!response.body) throw new Error('The backend returned empty public metadata.');
    const chunks = []; let bytes = 0;
    for await (const chunk of response.body) {
      bytes += chunk.byteLength;
      if (bytes > 65536) throw new Error('Public metadata exceeds the bounded document size.');
      chunks.push(chunk);
    }
    documents.push(JSON.parse(Buffer.concat(chunks).toString('utf8')));
  }
  return validatedMetadata(documents, originValue);
}

/** Publish public JSON only in a fresh staging public/.well-known directory. */
export function writeMetadata(documents, origin, directory) {
  const validated = validatedMetadata(documents, origin);
  const target = path.resolve(directory), relative = path.relative(repository, target);
  const inRepository = relative === '' || !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
  if (path.basename(target) !== '.well-known' || path.basename(path.dirname(target)) !== 'public'
    || inRepository && !relative.startsWith(`.playwright${path.sep}`))
    throw new Error('Write metadata to an explicit private staging public/.well-known directory.');
  const files = metadataPaths.map(suffix => path.join(target, ...suffix.split('/')));
  if (files.some(file => fs.existsSync(file))) throw new Error('Metadata staging files already exist; use a fresh directory.');
  files.forEach((file, index) => {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, JSON.stringify(validated[index], null, 2) + '\n', { flag: 'wx', mode: 0o600 });
  });
  return files;
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
  const args = process.argv.slice(2);
  if (args.length !== 6 || args[0] !== '--backend-origin' || args[2] !== '--public-origin' || args[4] !== '--output')
    throw new Error('Usage: node deploy/vercel/prepare-metadata.mjs --backend-origin https://BACKEND --public-origin https://WEB --output PRIVATE_STAGING/web/public/.well-known');
  try {
    const documents = await readMetadata(args[1], args[3], process.env.ORIGIN_SECRET);
    console.log(`Wrote ${writeMetadata(documents, args[3], args[5]).length} public metadata documents.`);
  } catch {
    throw new Error('Metadata preparation failed; no credentials, response bodies or transport diagnostics are displayed.');
  }
}
