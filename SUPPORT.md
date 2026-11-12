# Getting help with Miu

Miu is an early Windows x64 application requiring .NET Framework 4.8. Support is best effort through [GitHub issues](https://github.com/KumarManuSaraswat/miu/issues); there is no response-time guarantee, paid help desk, or remote access service.

## Start with the relevant guide

| Problem | First step |
| --- | --- |
| Commands or expressions | Read the [command guide](docs/COMMANDS.md). Try the same request by typing. |
| Microphone or speech | Check Windows microphone permission, the selected input, and Miu's input meter. Install the optional voice components as described in the README. An English Windows recognizer or the optional Vosk download is needed for recognition. |
| Gmail connection | Follow [Gmail setup](docs/GMAIL_SETUP.md). Check the selected test account and Desktop OAuth client type. Never share the downloaded credentials JSON. |
| Optional AI chat | Check the Settings enable switch, model, and your provider project's eligibility/quota. Local commands work while cloud chat is unavailable. Do not share your API key. |
| Preset catalog | Built-in presets work offline. A custom catalog is run by its operator; loading it is optional. No shared service endpoint is included. |
| Data or permissions | See [PRIVACY.md](PRIVACY.md) for local files, optional services, disconnection, and removal. |
| Builds or distribution | See [CONTRIBUTING.md](CONTRIBUTING.md) and [release instructions](docs/RELEASE.md). |

## Open a useful issue

Search existing issues, then choose a [bug or feature form](https://github.com/KumarManuSaraswat/miu/issues/new/choose). For a general setup question, a short issue with the relevant guide and the step that failed is enough. Include the Miu version or commit, Windows version, exact harmless reproduction, expected result, and visible error after redacting personal details. For voice problems, say which engine is installed, whether the meter moves, and whether typed requests work. A microphone recording is not required.

Issues and attachments are public. Do not upload account identifiers, credentials, mailbox contents, personal notes, raw diagnostic logs, or recordings of others. Synthetic examples are usually enough. No maintainer needs your password, API key, OAuth token, database connection string, payment, or remote control of your computer to read a bug report.

Report a security or privacy defect through [SECURITY.md](SECURITY.md), not a public issue. Conduct concerns follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Google, Microsoft, Neon, Cloudflare, and upstream voice projects control their own accounts and services; Miu's maintainer cannot reset those accounts or guarantee their availability.
