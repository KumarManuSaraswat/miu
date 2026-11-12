import test from 'node:test';
import assert from 'node:assert/strict';
import { neonConfig } from '@neondatabase/serverless';
import { loadPresets, validateDatabaseUrl } from '../src/database.js';
import worker from '../src/index.js';

// Deliberately invalid placeholder password; HTTP is intercepted, never sent.
const connection = 'postgresql://miu_catalog_reader:<TEST_ONLY>@ep-unit-test.us-east-2.aws.neon.tech/neondb?sslmode=require';

test('database configuration accepts only a Neon TLS connection for the runtime role', () => {
  assert.equal(validateDatabaseUrl(connection), connection);
  for (const value of [undefined, '', 'https://example.com',
    connection.replace('miu_catalog_reader', 'neondb_owner'),
    connection.replace('aws.neon.tech', 'attacker.example'),
    connection.replace('sslmode=require', 'sslmode=disable'),
    connection + '&options=unsafe', connection + '#fragment',
    connection + '&sslmode=disable', connection + '&sslmode=require',
    connection + '&channel_binding=disable']) {
    assert.throws(() => validateDatabaseUrl(value));
  }
});

test('production entrypoint validates private configuration before reading cached data', async () => {
  let cacheReads = 0;
  const cache = { match: async () => {
    cacheReads++;
    return Response.json({ schemaVersion: 1, presets: [] }, { headers: { Date: new Date().toUTCString() } });
  } };
  const result = await worker.fetch(new Request('https://catalog.example/v1/presets'),
    { DATABASE_URL: connection.replace('miu_catalog_reader', 'neondb_owner') }, {}, cache);
  assert.equal(result.status, 503);
  assert.equal(cacheReads, 0);
});

test('official Neon driver sends one bounded, read-only HTTP transaction and preserves typed fields', async () => {
  const previous = neonConfig.fetchFunction;
  let captured;
  const controller = new AbortController();
  neonConfig.fetchFunction = async (url, options) => {
    captured = { url, options };
    return Response.json({ results: [{
      fields: [
        { name: 'id', dataTypeID: 25 }, { name: 'name', dataTypeID: 25 },
        { name: 'description', dataTypeID: 25 }, { name: 'mood', dataTypeID: 25 },
        { name: 'speechRate', dataTypeID: 21 }, { name: 'reducedMotion', dataTypeID: 16 }
      ], rows: [['focus', 'Focus', 'A quiet companion.', 'content', '-1', 't']]
    }] });
  };
  try {
    const rows = await loadPresets({ DATABASE_URL: connection }, controller.signal);
    assert.deepEqual(rows, [{ id: 'focus', name: 'Focus', description: 'A quiet companion.',
      mood: 'content', speechRate: -1, reducedMotion: true }]);
    assert.equal(new URL(captured.url).protocol, 'https:');
    assert.equal(captured.options.method, 'POST');
    assert.equal(captured.options.headers['Neon-Batch-Read-Only'], 'true');
    assert.equal(captured.options.signal, controller.signal);
    const body = JSON.parse(captured.options.body);
    assert.equal(body.queries.length, 1);
    assert.match(body.queries[0].query, /FROM miu_catalog\.presets WHERE published = true/);
    assert.match(body.queries[0].query, /ORDER BY sort_order, id LIMIT \$1/);
    assert.deepEqual(body.queries[0].params, ['65']);
    assert.doesNotMatch(body.queries[0].query, /INSERT|UPDATE|DELETE|DROP/i);
  } finally { neonConfig.fetchFunction = previous; }
});

test('Neon receives the abort signal and transport failure stays catchable', async () => {
  const previous = neonConfig.fetchFunction;
  const controller = new AbortController();
  controller.abort();
  neonConfig.fetchFunction = async (_url, options) => {
    assert.equal(options.signal.aborted, true);
    throw new DOMException('Aborted', 'AbortError');
  };
  try { await assert.rejects(loadPresets({ DATABASE_URL: connection }, controller.signal)); }
  finally { neonConfig.fetchFunction = previous; }
});
