import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createWorker } from '../src/worker.js';
import { MAX_RESPONSE_BYTES, SUPPORTED_MOODS, validateCatalog, readCatalogResponse } from '../src/contract.js';

const preset = { id: 'focus', name: 'Focus', description: 'Calm speech and less movement.',
  mood: 'content', speechRate: -1, reducedMotion: true };
const fixture = () => ({ schemaVersion: 1, presets: [{ ...preset }] });
const request = (path = '/v1/presets', method = 'GET') => new Request('https://catalog.example' + path, { method });
const configured = { DATABASE_URL: 'test-only-private-binding' };
const response = value => new Response(JSON.stringify(value), {
  headers: { 'Content-Type': 'application/json', Date: new Date().toUTCString() }
});
class MemoryCache {
  entries = new Map();
  async match(key) { return this.entries.get(key.url)?.clone(); }
  async put(key, value) { this.entries.set(key.url, value.clone()); }
}

test('public response includes exactly the appearance and speech preferences', async () => {
  const worker = createWorker(async () => fixture().presets);
  const result = await worker.fetch(request(), configured);
  assert.equal(result.status, 200);
  assert.deepEqual(await result.json(), fixture());
  assert.match(result.headers.get('cache-control'), /max-age=300/);
  assert.equal(result.headers.get('x-content-type-options'), 'nosniff');
  assert.equal(result.headers.get('set-cookie'), null);
});

test('health checks do not wake the database and writes cannot reach it', async () => {
  let calls = 0;
  const worker = createWorker(async () => { calls++; throw new Error('should not call'); });
  assert.deepEqual(await (await worker.fetch(request('/health'))).json(), { ok: true, schemaVersion: 1 });
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'HEAD']) {
    const result = await worker.fetch(request('/v1/presets', method), configured);
    assert.equal(result.status, 405);
    assert.equal(result.headers.get('allow'), 'GET');
  }
  assert.equal((await worker.fetch(request('/v1/presets?email=private'), configured)).status, 400);
  assert.equal((await worker.fetch(request('/v1/presets/'), configured)).status, 404);
  assert.equal((await worker.fetch(request('/admin'), configured)).status, 404);
  assert.equal((await worker.fetch(new Request('http://catalog.example/v1/presets'), configured)).status, 400);
  assert.equal(calls, 0);
});

test('database failures never expose diagnostics or cache error bodies', async () => {
  let puts = 0;
  const cache = { match: async () => undefined, put: async () => puts++ };
  const worker = createWorker(async () => { throw new Error('password=secret private SQL'); });
  const result = await worker.fetch(request(), configured, {}, cache);
  assert.equal(result.status, 503);
  assert.equal(await result.text(), '{"error":"catalog_unavailable"}');
  assert.equal(result.headers.get('cache-control'), 'no-store');
  assert.equal(puts, 0);
});

test('unconfigured worker fails closed even if cache contains data', async () => {
  const cache = new MemoryCache();
  await cache.put(request(), response(fixture()));
  const result = await createWorker(async () => fixture().presets).fetch(request(), {}, {}, cache);
  assert.equal(result.status, 503);
});

test('cache hit skips Neon and cache storage failures do not break valid responses', async () => {
  let calls = 0;
  const worker = createWorker(async () => { calls++; return fixture().presets; });
  const cache = new MemoryCache();
  await worker.fetch(request(), configured, {}, cache);
  assert.deepEqual(await (await worker.fetch(request(), configured, {}, cache)).json(), fixture());
  assert.equal(calls, 1);
  const broken = { match: async () => { throw new Error(); }, put: async () => { throw new Error(); } };
  assert.equal((await worker.fetch(request(), configured, {}, broken)).status, 200);
});

test('cached responses preserve their original freshness and expired/future dates are rejected', async () => {
  let clock = Date.UTC(2026, 10, 12, 10, 0, 0);
  let calls = 0;
  const worker = createWorker(async () => { calls++; return fixture().presets; }, { now: () => clock });
  const cache = new MemoryCache();
  const original = await worker.fetch(request(), configured, {}, cache);
  clock += 240000;
  const cached = await worker.fetch(request(), configured, {}, cache);
  assert.equal(cached.headers.get('date'), original.headers.get('date'));
  assert.equal(calls, 1);
  clock += 61000;
  await worker.fetch(request(), configured, {}, cache);
  assert.equal(calls, 2);
  clock -= 1000;
  await worker.fetch(request(), configured, {}, cache);
  assert.equal(calls, 3);
});

test('invalid production configuration cannot be concealed by a cached success', async () => {
  let calls = 0;
  const cache = new MemoryCache();
  const cached = response(fixture());
  cached.headers.set('Date', new Date().toUTCString());
  await cache.put(request(), cached);
  const worker = createWorker(async () => { calls++; return fixture().presets; }, {
    validateConfiguration: () => { throw new Error('Invalid private database URL.'); }
  });
  const result = await worker.fetch(request(), configured, {}, cache);
  assert.equal(result.status, 503);
  assert.deepEqual(await result.json(), { error: 'catalog_unavailable' });
  assert.equal(calls, 0);
});

test('an invalid cached row cannot bypass validation', async () => {
  const cache = new MemoryCache();
  await cache.put(request(), response({ schemaVersion: 1, presets: [{ ...preset, command: 'open.exe' }] }));
  let calls = 0;
  const worker = createWorker(async () => { calls++; return fixture().presets; });
  assert.deepEqual(await (await worker.fetch(request(), configured, {}, cache)).json(), fixture());
  assert.equal(calls, 1);
});

test('database timeout aborts the real request and remains bounded if the adapter hangs', async () => {
  let observed;
  const worker = createWorker(async (_env, signal) => {
    observed = signal;
    return new Promise(() => {});
  }, { timeoutMs: 20 });
  const start = performance.now();
  const result = await worker.fetch(request(), configured);
  assert.equal(result.status, 503);
  assert.equal(observed.aborted, true);
  assert.ok(performance.now() - start < 1000);
});

const invalidRows = [
  { speechRate: 0.5 }, { speechRate: '1' }, { speechRate: -6 }, { speechRate: 6 },
  { reducedMotion: 0 }, { reducedMotion: 'false' }, { mood: 'unknown' }, { mood: 'Wide eyed' },
  { id: '../focus' }, { id: 'UPPER' }, { id: 'x'.repeat(65) },
  { name: '' }, { name: 'x'.repeat(81) }, { name: ' Focus' }, { name: '<script>' },
  { name: 'Hi\u202eexe' }, { name: 'Hi\u200b' }, { name: 'a\nb' },
  { description: 'x'.repeat(241) }, { description: 'https://example.com' },
  { description: 'visit example.com now' }, { command: 'run' }, { url: 'https://example.com' }
];
for (const change of invalidRows) {
  test('rejects malformed or active preset field ' + JSON.stringify(change).slice(0, 90), async () => {
    const worker = createWorker(async () => [{ ...preset, ...change }]);
    assert.equal((await worker.fetch(request(), configured)).status, 503);
  });
}

test('unknown root keys, missing fields, duplicates and oversized lists are rejected', () => {
  assert.throws(() => validateCatalog({ ...fixture(), instructions: 'run' }));
  const missing = { ...preset }; delete missing.reducedMotion;
  assert.throws(() => validateCatalog({ schemaVersion: 1, presets: [missing] }));
  assert.throws(() => validateCatalog({ schemaVersion: '1', presets: [] }));
  assert.throws(() => validateCatalog({ schemaVersion: 1, presets: [preset, preset] }));
  assert.throws(() => validateCatalog({ schemaVersion: 1, presets: Array.from({ length: 65 }, (_, i) => ({ ...preset, id: 'p' + i })) }));
  assert.deepEqual(validateCatalog({ schemaVersion: 1, presets: [] }), { schemaVersion: 1, presets: [] });
});

test('both rate limits and all supported moods are accepted without coercion', () => {
  for (const mood of SUPPORTED_MOODS)
    for (const speechRate of [-5, 5])
      assert.equal(validateCatalog({ schemaVersion: 1, presets: [{ ...preset, mood, speechRate }] }).presets[0].speechRate, speechRate);
});

test('bounded reader rejects oversized streams, invalid UTF8, nonJSON and malformed JSON', async () => {
  assert.deepEqual(await readCatalogResponse(response(fixture())), fixture());
  await assert.rejects(readCatalogResponse(new Response('x', { headers: { 'content-type': 'text/html' } })));
  await assert.rejects(readCatalogResponse(new Response('x'.repeat(MAX_RESPONSE_BYTES + 1), { headers: { 'content-type': 'application/json' } })));
  await assert.rejects(readCatalogResponse(new Response('[]', { headers: { 'content-type': 'application/json', 'content-length': String(MAX_RESPONSE_BYTES + 1) } })));
  await assert.rejects(readCatalogResponse(new Response(new Uint8Array([0xff]), { headers: { 'content-type': 'application/json' } })));
  await assert.rejects(readCatalogResponse(response({ schemaVersion: 999, presets: [] })));
});

test('mood allowlist stays synchronized with the actual desktop renderer and SQL constraint', async () => {
  const source = await readFile(new URL('../../../src/ExpressionCatalog.cs', import.meta.url), 'utf8');
  const arrays = [...source.matchAll(/(?:referenceMoods|legacyMoods)\s*=\s*new\[\]\s*\{([\s\S]*?)\}/g)];
  const desktop = arrays.flatMap(match => [...match[1].matchAll(/"([a-z_]+)"/g)].map(item => item[1]));
  assert.equal(arrays.length, 2);
  assert.deepEqual([...SUPPORTED_MOODS].sort(), desktop.sort());
  const migration = await readFile(new URL('../migrations/001_catalog.sql', import.meta.url), 'utf8');
  const constraint = /mood IN \(([\s\S]*?)\)\)/.exec(migration)[1];
  assert.deepEqual([...constraint.matchAll(/'([a-z_]+)'/g)].map(item => item[1]).sort(), desktop.sort());
});
