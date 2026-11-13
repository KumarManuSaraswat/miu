# Security policy

Miu is an early native companion for Windows and Android, not a security-reviewed system administrator. Its supported actions are intentionally limited. No independent security audit or guaranteed response time is claimed.

## Report a vulnerability privately

Use the repository's [private vulnerability report form](https://github.com/KumarManuSaraswat/miu/security/advisories/new). Include the affected release or commit, operating-system version, a minimal reproduction with synthetic data, and the expected impact. Do not include live tokens, API keys, private email contents, or personal recordings.

If the private form is unavailable, use a public issue only to request a private reporting channel, without posting exploits or sensitive details. No public project email, bounty program, or response-time commitment is established.

Maintainers should enable and test private vulnerability reporting before promoting a public binary release. [GitHub's setup instructions](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository).

## Supported versions

Security fixes target the latest **0.1.x** release and the current development branch. Earlier experimental builds and modified third-party copies have no maintenance commitment. Consult the repository's Releases page for available versions; this policy does not promise a release or a support deadline.

## Security boundaries

- Email contents and AI responses cannot directly execute device commands. Only the explicit command router can select supported actions.
- Windows Gmail requests only `gmail.readonly`; OAuth uses a temporary loopback callback, PKCE, and request-state checking. Android uses a separately enabled, Gmail-only Accessibility service with bounded user-requested navigation and screen reading. Opening an Android Gmail message may mark it read; the service does not send, delete or archive mail.
- Saved API credentials and OAuth tokens use Windows DPAPI for the current user. This protects the stored files from straightforward use by another account; it does not protect secrets from software already running as that user or an administrator.
- Android encrypts its optional API key with an Android Keystore AES-GCM key, disables app-data backup, and keeps mail results only in process memory. No cloud API accepts screen or email results from the reader. Accessibility events are ignored between explicit requests. These boundaries do not protect a compromised/rooted device.
- Android's assistant activity ignores system-provided screen/context extras and externally supplied commands. The user chooses the default assistant in Android. Invocation listening is optional, off by default, stops when leaving the panel, and cannot start while locked. The assistant panel requires no overlay access.
- Android's optional WhatsApp integration reads only the open chat's accessible visible text. Sending requires the direct user's message plus a separate recipient/text review, expires after one minute, rechecks chat title and draft, and never retries Send. It never sends from model output or received screen text. Treat any bypass of that review, package filter, draft check or cancellation as a security-sensitive change.
- Miu does not run arbitrary shell commands supplied by chat and does not offer mail sending, file deletion, payments, or unattended administration.
- Voice workers run on demand. Stop, timeout, and shutdown paths should release microphone and model resources. Recognition uncertainty requires user review rather than executing a guessed instruction.

Treat any new path that sends Gmail content to a cloud service, executes model output, escapes the note directory, exposes a secret, or leaves a microphone running after cancellation as a security-sensitive change.

## Downloads and release integrity

The lean release excludes downloaded voice engines and models. Optional setup scripts retrieve specific upstream artifacts; their licenses and published verification information are documented in [THIRD_PARTY.md](docs/THIRD_PARTY.md).

Release SHA-256 files detect accidental changes when obtained from a trusted source. A hash delivered beside an untrusted executable does not independently establish who built it. Windows executables are currently unsigned. Official Android APKs are signed with the project's private release key and publish its certificate fingerprint; debug builds use a separate application ID. Never commit signing keys or passwords. Do not advise users to disable Windows security, Play Protect or enterprise restrictions to run Miu.

No telemetry or automatic crash upload is implemented. Local diagnostics and startup error files can contain environment details and file paths; review and redact them before sharing. See [PRIVACY.md](PRIVACY.md).
