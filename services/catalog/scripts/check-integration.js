import assert from 'node:assert/strict';
import { neon } from '@neondatabase/serverless';
import { loadPresets, validateDatabaseUrl } from '../src/database.js';
import { readCatalogResponse, validateCatalog } from '../src/contract.js';

// Deliberately absent from normal tests/CI. Does not migrate, seed, or write.
if (process.env.ALLOW_LIVE_CATALOG_TESTS !== '1') {
  console.error('Skipped: set ALLOW_LIVE_CATALOG_TESTS=1 and supply the private runtime DATABASE_URL to run live read-only checks.');
  process.exitCode = 2;
} else {
  try {
    const sql = neon(validateDatabaseUrl(process.env.DATABASE_URL));
    const [roles] = await sql.transaction([
      sql`SELECT current_user AS role,
        r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls AS elevated,
        EXISTS (SELECT 1 FROM pg_auth_members WHERE member = r.oid) AS has_memberships,
        has_table_privilege(current_user, 'miu_catalog.presets', 'SELECT') AS can_select,
        has_table_privilege(current_user, 'miu_catalog.presets', 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER') AS can_write,
        has_schema_privilege(current_user, 'miu_catalog', 'CREATE') OR has_schema_privilege(current_user, 'public', 'CREATE') AS can_create,
        has_database_privilege(current_user, current_database(), 'CREATE,TEMP') AS can_create_database_objects,
        current_setting('default_transaction_read_only') AS default_read_only
        FROM pg_roles r WHERE rolname = current_user`
    ], { readOnly: true, fetchOptions: { signal: AbortSignal.timeout(10000) } });
    assert.equal(roles.length, 1);
    const role = roles[0];
    assert.equal(role.role, 'miu_catalog_reader');
    assert.equal(role.elevated, false);
    assert.equal(role.has_memberships, false);
    assert.equal(role.can_select, true);
    assert.equal(role.can_write, false);
    assert.equal(role.can_create, false);
    assert.equal(role.can_create_database_objects, false);
    assert.equal(role.default_read_only, 'on');
    const rows = await loadPresets(process.env, AbortSignal.timeout(10000));
    const catalog = validateCatalog({ schemaVersion: 1, presets: rows });
    assert.ok(catalog.presets.length > 0, 'Seeded catalog must not be empty.');
    console.log('PASS: real Neon connection, runtime permissions, and public preset rows.');
    if (process.env.CATALOG_URL) {
      const url = new URL(process.env.CATALOG_URL);
      assert.equal(url.protocol, 'https:');
      assert.equal(url.pathname, '/v1/presets');
      assert.ok(!url.username && !url.password && !url.search && !url.hash);
      const response = await fetch(url, { redirect: 'error', signal: AbortSignal.timeout(10000) });
      const publicCatalog = await readCatalogResponse(response);
      assert.ok(publicCatalog.presets.length > 0);
      console.log('PASS: deployed HTTPS catalog response and strict public contract.');
    }
  } catch {
    // Driver errors can include the connection string or database details.
    console.error('FAIL: live catalog verification failed. Check the documented role, migrations, secret, and endpoint privately.');
    process.exitCode = 1;
  }
}
