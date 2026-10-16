export const VERSION = '1.0.0';
export const ORIGIN = 'http://127.0.0.1:5188';
export const PROVIDERS = Object.freeze({
  ChatGptWeb: { url: 'https://chatgpt.com/', host: 'chatgpt.com', adapter: 'chatgpt', allowed: true },
  ClaudeWeb: { url: 'https://claude.ai/new', host: 'claude.ai', adapter: 'claude', allowed: false }
});
export const uuid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value);
export function trustedSender(sender, extensionId) {
  try { return sender.id === extensionId && sender.frameId === 0 && sender.tab?.id !== undefined && new URL(sender.url).origin === ORIGIN && new URL(sender.tab.url).origin === ORIGIN; } catch { return false; }
}
export function destination(provider, url) {
  try { const p = PROVIDERS[provider], u = new URL(url); return !!p && u.protocol === 'https:' && u.hostname === p.host && !u.port && !u.username && !u.password && !u.search && !u.hash && (u.pathname === '/' || (provider === 'ClaudeWeb' && u.pathname === '/new')); } catch { return false; }
}
export function validate(request, now = Date.now()) {
  const keys = ['version','requestId','provider','compiledPrompt','action','createdAtUtc','expiresAtUtc','explicitUserAction','templateId','revisionId'];
  if (!request || Object.keys(request).some(k => !keys.includes(k)) || request.version !== 1 || !uuid(request.requestId) || !Object.hasOwn(PROVIDERS, request.provider) || !['PrepareOnly','PrepareAndSend'].includes(request.action) || request.explicitUserAction !== true) throw new Error('invalid-request');
  if (typeof request.compiledPrompt !== 'string' || !request.compiledPrompt.trim() || request.compiledPrompt.length > 20000) throw new Error('invalid-prompt');
  const start = Date.parse(request.createdAtUtc), end = Date.parse(request.expiresAtUtc);
  if (!Number.isFinite(start) || !Number.isFinite(end) || start > now + 5000 || end <= now || end <= start || end-start > 120000 || now-start > 120000) throw new Error('expired-request');
  if ([request.templateId,request.revisionId].some(v => v != null && !uuid(v))) throw new Error('invalid-metadata');
  return request;
}
// Serial reservation in the service worker plus durable metadata prevents replay after suspension.
export class ReplayLedger {
  #queue = Promise.resolve();
  constructor(storage) { this.storage = storage; }
  reserve(request) {
    const work = this.#queue.then(async () => {
      const now = Date.now(), stored = (await this.storage.get('requests')).requests || {};
      for (const [id,r] of Object.entries(stored)) if (r.expiresAt < now) delete stored[id];
      if (Object.hasOwn(stored, request.requestId)) throw new Error('replay-request');
      if (Object.keys(stored).length >= 2048) throw new Error('ledger-full');
      stored[request.requestId] = { expiresAt: Date.parse(request.expiresAtUtc)+60000, provider: request.provider, action: request.action, state: 'Ready', explicitUserAction: true, adapterVersion: VERSION };
      await this.storage.set({requests:stored});
    });
    this.#queue = work.catch(() => {}); return work;
  }
  finish(id, result) {
    const work = this.#queue.then(async () => {
      const stored = (await this.storage.get('requests')).requests || {};
      if (Object.hasOwn(stored,id)) {
        stored[id] = {...stored[id],state:result.state,code:result.code,durationMs:result.durationMs};
        const audit=(await this.storage.get('audit')).audit || [];
        audit.unshift({requestId:id,provider:stored[id].provider,action:stored[id].action,explicitUserAction:true,state:result.state,code:result.code,durationMs:result.durationMs,adapterVersion:VERSION,atUtc:new Date().toISOString()});
        await this.storage.set({requests:stored,audit:audit.slice(0,200)});
      }
    }); this.#queue=work.catch(()=>{});return work;
  }
}
