# Maintainer guide

This is a small project with a single initial maintainer. Keep review proportionate to the change and record evidence that another contributor can reproduce. [GOVERNANCE.md](../GOVERNANCE.md) describes decisions; [RELEASE.md](RELEASE.md) describes distribution.

## Find the right code

| Area | Main files and boundary |
| --- | --- |
| Startup and application lifecycle | `src/Program.cs`, `src/AppController.cs`; UI coordination, cancellation, shutdown. |
| Desktop UI | `src/ChatWindow.cs`, `src/MascotWindow.cs`, `src/SettingsWindow.cs`; marshal background events to the UI thread. |
| Expressions | `src/MascotFace.cs`, `src/ExpressionCatalog.cs`, `src/ReactionsWindow.cs`; native drawing and supported expression IDs. |
| Commands and conversation | `src/AssistantEngine.cs`; explicit local routing and optional cloud conversation stay separate. |
| Device actions | `src/DeviceActions.cs`, `src/Native.cs`; bounded actions and Windows integration, never arbitrary model-generated commands. |
| Settings and secrets | `src/Configuration.cs`; ordinary preferences versus Windows user-scoped protected credentials. |
| Gmail | `src/GmailService.cs`; user-initiated read-only access and bounded local message processing. |
| Voice | `src/SpeechService.cs`, `src/VoskSpeech.cs`, `src/PiperSpeech.cs`; microphone/synthesis coordination and short-lived native workers. |
| Presets | `src/CommunityCatalog.cs`, `src/CommunityWindow.cs`; validated public data and explicit application of three preferences. |
| Optional catalog service | `services/catalog`; independent read-only Worker/Neon integration, migrations, and tests. |
| Validation and releases | `tests`, `build.ps1`, `scripts/package-release.ps1`, `scripts/verify-release.ps1`, `.github/workflows`; no provider deployment is part of a desktop build. |

Follow the [contributor build instructions](../CONTRIBUTING.md). The optional service has its own [setup and checks](https://github.com/KumarManuSaraswat/miu/tree/main/services/catalog). Use a separate `MIU_DATA_DIR` with synthetic data for experiments. Never use a maintainer's real mailbox, keys, or settings as test fixtures.

## Review a contribution

1. Establish the user-visible behavior and scope. Ask for a minimal reproduction when needed; close duplicates with a link and explain out-of-scope requests.
2. Review the actual diff, including generated files and lockfile changes. Check cancellation, cleanup, bounds, and idle work where relevant. Imported text, model output, and catalog data must not gain command-execution authority.
3. Run the relevant offline checks and inspect the results. UI-only changes need a clean visual check. Separate synthetic audio replay from a microphone test, mocked provider checks from a live connection, and worker memory from app memory.
4. Check docs, privacy/data flow, and third-party notices when behavior or dependencies change. Confirm screenshots and examples contain only synthetic content.
5. Record the validation and remaining limitations in the pull request. Merge only changes the maintainer understands and can maintain; keep the changelog focused on user-visible changes.

Review is best effort. Do not auto-close an unanswered report merely to imply that a problem was fixed. If work is deferred, explain what information or capacity is missing. A reproducible privacy, credential, microphone-lifecycle, or arbitrary-execution problem takes priority over a new feature and may need the private process in [SECURITY.md](../SECURITY.md).

## Dependency and service changes

Pin optional downloads and backend package versions intentionally. Read the exact version's release notes, license, and source/notices requirements; update [THIRD_PARTY.md](THIRD_PARTY.md) and the lockfile together. An archived upstream dependency remains a maintenance risk even when a local test passes. Do not silently replace the older Piper archive with a differently licensed successor. Preserve generated license notices when building or distributing a Worker bundle.

A model, API, or provider update can alter memory, eligibility, quotas, privacy, and behavior. Verify those changes against primary provider documentation and distinguish measurements from estimates. Test parser and failure paths without live secrets first. Deployments, OAuth configuration, sponsor applications, and paid billing are separate explicit maintainer actions; do not add them to a normal build or CI test.

The catalog contains public curated presets only. Keep the runtime database role read-only and separate from the owner, use reviewed migrations, and never add user uploads or private data without a new design/privacy review. Local presets must continue working when the service is unavailable. See the service README for deployment and actual-permission checks.

## Priorities for the next contributions

These are review priorities, not promised milestones or dates:

1. Repeatable Windows installation, startup, voice cancellation, and microphone behavior across real devices.
2. Keyboard/navigation, screen-reader, contrast, and reduced-motion checks with actionable accessibility fixes.
3. Clear OAuth/provider onboarding and useful offline errors without expanding account access.
4. Dependency upkeep, release provenance, and evaluation of practical signing options.

Propose larger automation, background listening, synchronization, or paid-service features in an issue first. They change the project's resource and trust boundaries.
