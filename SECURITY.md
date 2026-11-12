# Security policy

Miu is an early desktop application, not a security-reviewed system administrator. Its supported actions are intentionally limited. No independent security audit or guaranteed response time is claimed.

## Report a vulnerability privately

Use the repository's [private vulnerability report form](https://github.com/KumarManuSaraswat/miu/security/advisories/new). Include the affected release or commit, Windows version, a minimal reproduction with synthetic data, and the expected impact. Do not include live tokens, API keys, private email contents, or personal recordings.

If the private form is unavailable, use a public issue only to request a private reporting channel, without posting exploits or sensitive details. No public project email, bounty program, or response-time commitment is established.

Maintainers should enable and test private vulnerability reporting before promoting a public binary release. [GitHub's setup instructions](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository).

## Supported versions

Security fixes target the latest **0.1.x** release and the current development branch. Earlier experimental builds and modified third-party copies have no maintenance commitment. Consult the repository's Releases page for available versions; this policy does not promise a release or a support deadline.

## Security boundaries

- Email contents and AI responses cannot directly execute device commands. Only the explicit command router can select supported actions.
- Gmail requests only `gmail.readonly`; OAuth uses a temporary loopback callback, PKCE, and request-state checking.
- Saved API credentials and OAuth tokens use Windows DPAPI for the current user. This protects the stored files from straightforward use by another account; it does not protect secrets from software already running as that user or an administrator.
- Miu does not run arbitrary shell commands supplied by chat and does not offer mail sending, file deletion, payments, or unattended administration.
- Voice workers run on demand. Stop, timeout, and shutdown paths should release microphone and model resources. Recognition uncertainty requires user review rather than executing a guessed instruction.

Treat any new path that sends Gmail content to a cloud service, executes model output, escapes the note directory, exposes a secret, or leaves a microphone running after cancellation as a security-sensitive change.

## Downloads and release integrity

The lean release excludes downloaded voice engines and models. Optional setup scripts retrieve specific upstream artifacts; their licenses and published verification information are documented in [THIRD_PARTY.md](docs/THIRD_PARTY.md).

Release SHA-256 files detect accidental changes when obtained from a trusted source. A hash delivered beside an untrusted executable does not independently establish who built it. Code signing is not yet configured. Do not advise users to disable Windows security features to run Miu.

No telemetry or automatic crash upload is implemented. Local diagnostics and startup error files can contain environment details and file paths; review and redact them before sharing. See [PRIVACY.md](PRIVACY.md).
