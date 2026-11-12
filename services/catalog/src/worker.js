import { SCHEMA_VERSION, readCatalogResponse, validateCatalog } from './contract.js';

const CACHE_SECONDS = 300;
const securityHeaders = {
  'Content-Type': 'application/json; charset=utf-8',
  'X-Content-Type-Options': 'nosniff',
  'Content-Security-Policy': "default-src 'none'; frame-ancestors 'none'",
  'Referrer-Policy': 'no-referrer',
  'Access-Control-Allow-Origin': '*'
};

function json(value, status = 200, headers = {}) {
  return new Response(JSON.stringify(value), { status,
    headers: { ...securityHeaders, 'Cache-Control': 'no-store', ...headers } });
}
function unavailable() { return json({ error: 'catalog_unavailable' }, 503, { 'Retry-After': '60' }); }
function catalogResponse(catalog, createdAt) {
  return json(catalog, 200, {
    'Cache-Control': `public, max-age=${CACHE_SECONDS}, must-revalidate`,
    // Preserve the original Date on cache hits so downstream caches cannot
    // extend freshness for another five minutes every time it is served.
    Date: new Date(createdAt).toUTCString()
  });
}

// Injection isolates network/cache boundaries for meaningful, offline tests.
export function createWorker(loadPresets, { timeoutMs = 5000, now = Date.now, validateConfiguration = () => {} } = {}) {
  return {
    async fetch(request, env = {}, ctx = {}, cache = globalThis.caches?.default) {
      const url = new URL(request.url);
      if (!['/health', '/v1/presets'].includes(url.pathname)) return json({ error: 'not_found' }, 404);
      if (request.method !== 'GET') return json({ error: 'method_not_allowed' }, 405, { Allow: 'GET' });
      if (url.search || url.hash || request.body) return json({ error: 'invalid_request' }, 400);
      if (url.protocol !== 'https:' && !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname))
        return json({ error: 'https_required' }, 400);
      if (url.pathname === '/health') return json({ ok: true, schemaVersion: SCHEMA_VERSION });
      // An unconfigured deployment must never serve an old cached catalog.
      if (typeof env.DATABASE_URL !== 'string' || !env.DATABASE_URL) return unavailable();
      try { validateConfiguration(env); }
      catch { return unavailable(); }
      const cacheKey = new Request(url.origin + '/v1/presets', { method: 'GET' });
      if (cache) {
        try {
          const cached = await cache.match(cacheKey);
          if (cached) {
            const createdAt = Date.parse(cached.headers.get('Date') || '');
            const age = now() - createdAt;
            // Cache contents remain untrusted and must meet the current contract.
            if (Number.isFinite(age) && age >= 0 && age < CACHE_SECONDS * 1000) {
              const catalog = await readCatalogResponse(cached);
              return catalogResponse(catalog, createdAt);
            }
          }
        } catch { /* An unavailable/invalid cache never bypasses database validation. */ }
      }
      const controller = new AbortController();
      let timer;
      try {
        const timedOut = new Promise((_, reject) => {
          timer = setTimeout(() => { controller.abort(); reject(new Error('Database deadline exceeded.')); }, timeoutMs);
        });
        const rows = await Promise.race([loadPresets(env, controller.signal), timedOut]);
        const catalog = validateCatalog({ schemaVersion: SCHEMA_VERSION, presets: rows });
        const response = catalogResponse(catalog, now());
        if (cache) {
          const saving = Promise.resolve().then(() => cache.put(cacheKey, response.clone())).catch(() => {});
          if (ctx.waitUntil) ctx.waitUntil(saving);
          else await saving;
        }
        return response;
      } catch { return unavailable(); }
      finally { clearTimeout(timer); }
    }
  };
}
