export const SCHEMA_VERSION = 1;
export const MAX_PRESETS = 64;
export const MAX_RESPONSE_BYTES = 64 * 1024;
// Stable renderer IDs from src/ExpressionCatalog.cs, including legacy aliases.
export const SUPPORTED_MOODS = Object.freeze([
  'idle', 'happy', 'curious', 'excited', 'love', 'sleepy', 'angry', 'confused',
  'thinking', 'listening', 'speaking', 'wink', 'surprised',
  'content', 'kiss', 'crying', 'neutral', 'playful', 'wailing', 'mischievous',
  'smug_kiss', 'sassy', 'wide_eyed', 'surprised_kiss', 'shocked', 'joyful', 'sad',
  'annoyed', 'scrunched', 'dizzy', 'blushing', 'cheerful', 'drooling',
  'delighted', 'skeptical', 'furious', 'amazed'
]);
const moods = new Set(SUPPORTED_MOODS);
const fields = ['id', 'name', 'description', 'mood', 'speechRate', 'reducedMotion'];
const forbiddenText = /[<>\p{Cc}\p{Cf}\p{Cs}\p{Zl}\p{Zp}]/u;
const urlText = /(?:[a-z][a-z\d+.-]*:|www\.|\b[a-z\d](?:[a-z\d-]*\.)+[a-z]{2,}\b)/i;

function exactKeys(value, expected) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const keys = Object.keys(value);
  return keys.length === expected.length && expected.every(key => Object.hasOwn(value, key));
}

function displayText(value, maximum) {
  return typeof value === 'string' && value.length >= 1 && value.length <= maximum &&
    value.trim() === value && !forbiddenText.test(value) && !urlText.test(value);
}

export function validateCatalog(value) {
  if (!exactKeys(value, ['schemaVersion', 'presets']) || value.schemaVersion !== SCHEMA_VERSION ||
      !Array.isArray(value.presets) || value.presets.length > MAX_PRESETS) {
    throw new Error('Invalid catalog.');
  }
  const ids = new Set();
  const presets = value.presets.map(row => {
    if (!exactKeys(row, fields) || typeof row.id !== 'string' || !/^[a-z0-9][a-z0-9-]{0,63}$/.test(row.id) ||
        ids.has(row.id) || !displayText(row.name, 80) || !displayText(row.description, 240) ||
        !moods.has(row.mood) || !Number.isInteger(row.speechRate) || row.speechRate < -5 || row.speechRate > 5 ||
        typeof row.reducedMotion !== 'boolean') throw new Error('Invalid catalog preset.');
    ids.add(row.id);
    // Construct a new object. No database property can implicitly become an app setting.
    return { id: row.id, name: row.name, description: row.description, mood: row.mood,
      speechRate: row.speechRate, reducedMotion: row.reducedMotion };
  });
  const catalog = { schemaVersion: SCHEMA_VERSION, presets };
  if (new TextEncoder().encode(JSON.stringify(catalog)).byteLength > MAX_RESPONSE_BYTES)
    throw new Error('Catalog is too large.');
  return catalog;
}

export async function readCatalogResponse(response) {
  if (!response.ok || !/^application\/json(?:\s*;|$)/i.test(response.headers.get('content-type') || '') || !response.body)
    throw new Error('Invalid catalog response.');
  const advertised = response.headers.get('content-length');
  if (advertised && (!/^\d+$/.test(advertised) || Number(advertised) > MAX_RESPONSE_BYTES))
    throw new Error('Catalog is too large.');
  const reader = response.body.getReader();
  const chunks = [];
  let length = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      length += value.byteLength;
      if (length > MAX_RESPONSE_BYTES) throw new Error('Catalog is too large.');
      chunks.push(value);
    }
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
  }
  const bytes = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  return validateCatalog(JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)));
}
