import { neon } from '@neondatabase/serverless';
import { MAX_PRESETS } from './contract.js';

export function validateDatabaseUrl(value) {
  if (typeof value !== 'string' || value.length > 4096) throw new Error('Database configuration unavailable.');
  const url = new URL(value);
  if (!['postgres:', 'postgresql:'].includes(url.protocol) ||
      !/^ep-[a-z0-9-]+\.[a-z0-9.-]+\.neon\.tech$/i.test(url.hostname) ||
      decodeURIComponent(url.username) !== 'miu_catalog_reader' || !url.password ||
      url.hash || (url.port && url.port !== '5432') || url.pathname.length < 2 ||
      url.searchParams.get('sslmode') !== 'require') throw new Error('Database configuration unavailable.');
  for (const [key, option] of url.searchParams) {
    if (!['sslmode', 'channel_binding'].includes(key) || option !== 'require' || url.searchParams.getAll(key).length !== 1)
      throw new Error('Unexpected database option.');
  }
  return value;
}

// One fixed HTTP query; no client-supplied URL, header, SQL or filter reaches Neon.
export async function loadPresets(env, signal) {
  const sql = neon(validateDatabaseUrl(env.DATABASE_URL));
  const [rows] = await sql.transaction([
    sql`SELECT id, name, description, mood, speech_rate AS "speechRate", reduced_motion AS "reducedMotion"
        FROM miu_catalog.presets WHERE published = true ORDER BY sort_order, id LIMIT ${MAX_PRESETS + 1}`
  ], { readOnly: true, fetchOptions: { signal } });
  return rows;
}
