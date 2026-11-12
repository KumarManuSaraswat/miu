# Optional public preset catalog

This independent Cloudflare Worker serves a small curated catalog from Neon Postgres. Miu can work without it. Deployment and database provisioning are manual maintainer steps; no shared endpoint is bundled with the desktop, and no sponsorship is promised. Hosting requires the maintainer's own free accounts. The desktop needs only a public HTTPS endpoint; it must never receive `DATABASE_URL`.

Presets change only a supported mascot expression, speech rate, and reduced motion after an explicit Apply action. They contain no prompts, commands, scripts, assets, HTML, or external links. The database has no users, emails, chat history, recordings, tokens, analytics, or per-device settings. The Worker accepts no writes and does not record request data. Cloudflare and Neon still operate their normal platform infrastructure; this is not a promise that their infrastructure keeps no operational logs.

## API

`GET /v1/presets` returns JSON:

```json
{
  "schemaVersion": 1,
  "presets": [{
    "id": "focus",
    "name": "Focus",
    "description": "A content face, slower speech, and less motion for quiet work.",
    "mood": "content",
    "speechRate": -1,
    "reducedMotion": true
  }]
}
```

Exactly these fields are allowed. Limits: 64 presets, 64 KiB UTF-8 response, lowercase alphanumeric/hyphen IDs up to 64 characters, names up to 80, descriptions up to 240, integer speech rates -5 through 5, and actual JSON booleans. Moods are the supported IDs from `ExpressionCatalog`, including legacy IDs. Display strings reject control/format characters, HTML delimiters, and URL syntax. Empty catalogs are valid. Duplicate IDs, missing fields, unknown fields or invalid rows fail the whole response with a generic uncached `503`.

`GET /health` returns `{"ok":true,"schemaVersion":1}` without connecting to Neon. It proves the Worker is running, not that database configuration is valid. Other paths return 404; other methods return 405. Query parameters are rejected. HTTPS is required except for loopback local development. There is no authentication or write endpoint; all catalog data is intentionally public.

The desktop should fetch only on an explicit request, use exactly the configured HTTPS `/v1/presets` URL, reject redirects and extra fields, validate its own size/field limits, and show a preview before applying those three preferences. It should keep the user's current settings when a fetch fails.

## Local checks

Use Node 22.12 or newer; CI uses Node 24. The npm lockfile fixes the exact dependency tree. From this directory:

```powershell
npm ci --ignore-scripts --no-audit --no-fund
npm test
npm run check:bundle
```

Tests mock database/HTTP boundaries, including the actual Neon driver's wire response, timeout cancellation, malformed data, cache failure, unsupported routes, and allowlist consistency with the desktop and SQL. `check:bundle` is a Wrangler dry run and does not deploy. No account or secret is needed for these checks. The driver is pinned to `@neondatabase/serverless` 1.2.0 and Wrangler to 4.161.0.

Retained runtime copyright and license notices are in the preserved `/*! */` banners in `src/index.js`; Wrangler includes them in the deployed bundle. They cover the Neon MIT license and identified prebundled buffer, node-postgres, and ieee754 notices. Keep upstream legal comments and notices when changing the build or upgrading the driver. Wrangler and its optional native development dependencies are tooling, not included in the desktop release. The repository's third-party document explains that distinction.

## Optional maintainer setup

1. Create a **dedicated** Neon Free project/database and use its smallest compute with scale to zero. Apply `migrations/001_catalog.sql` once as the database owner, then `002_seed_presets.sql`, using the SQL Editor. The first migration deliberately fails if its schema/role already exist. Keep subsequent catalog changes in new reviewed SQL migrations; rerunning the seed file restores the six named starter profiles.
2. The migration creates `miu_catalog_reader` without login and grants only schema usage and SELECT on the public catalog table. It removes inherited PUBLIC create/temp permissions in this dedicated database. In the private SQL Editor, enable login using `ALTER ROLE miu_catalog_reader LOGIN PASSWORD '<NEW_RANDOM_PASSWORD>';` with a password-manager-generated password. Never commit that command with a real password. Create the role using SQL as supplied, **not** the console's Add role button: console-created roles can inherit `neon_superuser` membership. Keep the owner credential separate. [Neon role documentation](https://neon.com/docs/manage/roles)
3. Copy the connection string for `miu_catalog_reader`, using its Neon host and `?sslmode=require` (optional `channel_binding=require` is accepted). The Worker refuses an owner username or non-Neon host. Percent-encode special password characters when constructing a URL manually. For local development only, copy `.dev.vars.example` to ignored `.dev.vars` and replace its angle placeholders. Never put the secret in `wrangler.jsonc`, desktop settings, public source, CI logs, or a command-line argument.
4. Run `npm run dev` for a local Worker. This command starts a loopback server and uses the configured real database when `/v1/presets` is requested. Normal unit tests do not contact Neon. The official HTTP driver is suitable for this single bounded read and needs no persistent socket or Hyperdrive binding. [Neon HTTP driver](https://neon.com/docs/serverless/serverless-driver), [configuration and timeouts](https://neon.com/docs/serverless/serverless-driver-configuration)
5. To publish intentionally, first review `wrangler.jsonc` and choose a unique Worker name. Run `npx wrangler login`, then `npm run deploy`. The initial unconfigured catalog returns 503. Run `npx wrangler secret put DATABASE_URL` and paste the runtime credential at the private prompt; then deploy again. These are manual maintainer actions, never desktop behavior or part of tests. No account ID, routes, or secrets are stored in the checked-in config. [Cloudflare secrets](https://developers.cloudflare.com/workers/configuration/secrets/)
6. Validate the real runtime permissions and optionally the deployed endpoint before sharing its URL. With the runtime secret in ignored `.dev.vars`, run:

```powershell
$env:ALLOW_LIVE_CATALOG_TESTS = '1'
# Optional non-secret endpoint, after a real deployment exists:
# $env:CATALOG_URL = 'https://<YOUR_WORKER>.<YOUR_SUBDOMAIN>.workers.dev/v1/presets'
node --env-file=.dev.vars scripts/check-integration.js
```

This explicit live check performs read-only queries and tests effective privileges, seeded row validity, and the optional HTTPS response. It does not create resources or write data. A skipped check exits with code 2; failed checks print a generic message to avoid exposing credentials. Live database permissions and deployment cannot be certified by mocked tests.

## Limits and maintenance

Only published rows are queried, in stable order, with a limit of 65 so accidental overpopulation fails closed rather than silently truncating. HTTP requests abort after five seconds; the runtime role has a four-second database statement limit. There are no background jobs or polling. Successful responses are cached for five minutes using the Workers Cache API; cache bodies and their original creation time are revalidated before use. Cached responses preserve the original Date so downstream clients cannot renew their freshness. Invalid configuration fails before a cache lookup. Cache failures fall back to the database, and database errors are never cached. Edge caches are local to a Cloudflare location, so a globally distributed workload can still wake Neon. [Cloudflare Cache API](https://developers.cloudflare.com/workers/runtime-apis/cache/)

As checked on 2026-11-12, Neon advertises a no-card Free plan with 100 CU-hours and 1 GB storage per project; Workers Free lists 100,000 requests/day and a 10 ms CPU limit. Stay on Free plans; do not attach a paid upgrade to this optional catalog. These are service quotas, not guaranteed availability or unlimited hosting. Public traffic can exhaust free capacity; desktop local presets remain usable when the catalog is unavailable. No Neon funding is required or assumed. [Neon pricing](https://neon.com/pricing), [Workers limits](https://developers.cloudflare.com/workers/platform/limits/)

To curate, review SQL changes as the owner, keep fewer than 65 published rows, run the live verifier, and allow caches to expire. Never grant the runtime role extra memberships or privileges. Rotate a compromised runtime password and replace the Worker secret; revoke the old role's login if retiring the service. Review package updates and rerun the test/dry-run checks before changing the lockfile. Worker request logging and Wrangler metrics are disabled in checked-in configuration; there is no application telemetry code.
