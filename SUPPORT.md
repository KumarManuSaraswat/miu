# Getting help with Miu

Miu has early native releases for Windows x64 (.NET Framework 4.8) and Android 8.0+. Support is best effort through [GitHub issues](https://github.com/KumarManuSaraswat/miu/issues); there is no response-time guarantee, paid help desk, or remote access service.

## Start with the relevant guide

| Problem | First step |
| --- | --- |
| Android installation, voice or permissions | Start with the [Android guide](android/README.md). Install the signed APK, grant only the optional access you want, and try a typed command first. For missing English recognition, use Settings → Check offline English / Download offline English. Models need an initial provider download; everyday local commands do not. |
| Android Gmail / WhatsApp screen reading | Enable each service through Miu's disclosure and Android Accessibility, open a harmless message/chat, then ask Miu to read it. Interfaces and hidden content vary. Never attach private chat screenshots. For WhatsApp, check delivery before retrying a send. |
| Commands or expressions | Read the [command guide](docs/COMMANDS.md). Try the same request by typing. |
| Windows microphone or speech | Check Windows microphone permission, the selected input, and Miu's input meter. Install the optional voice components as described in the README. An English Windows recognizer or the optional Vosk download is needed for recognition. |
| Windows Gmail connection | Follow [Gmail setup](docs/GMAIL_SETUP.md). Check the selected test account and Desktop OAuth client type. Never share the downloaded credentials JSON. Android uses screen access instead of OAuth. |
| Optional AI chat | Check the Settings enable switch, model, and your provider project's eligibility/quota. Local commands work while cloud chat is unavailable. Do not share your API key. |
| Preset catalog | Built-in expressions work offline. Loading the shared Neon-backed catalog is optional. Android includes its public address behind an explicit Load button; Windows accepts a catalog address you choose. No credentials are sent by either client. |
| Data or permissions | See [PRIVACY.md](PRIVACY.md) for local files, optional services, disconnection, and removal. |
| Builds or distribution | See [CONTRIBUTING.md](CONTRIBUTING.md) and [release instructions](docs/RELEASE.md). |

## Open a useful issue

Search existing issues, then choose a [bug or feature form](https://github.com/KumarManuSaraswat/miu/issues/new/choose). For a general setup question, a short issue with the relevant guide and the step that failed is enough. Include the Miu version or commit, operating-system version and phone model if relevant, exact harmless reproduction, expected result, and visible error after redacting personal details. For voice problems, say which engine/language pack is installed and whether typed requests work. A microphone recording or device serial number is not required.

Issues and attachments are public. Do not upload account identifiers, credentials, mailbox contents, personal notes, raw diagnostic logs, or recordings of others. Synthetic examples are usually enough. No maintainer needs your password, API key, OAuth token, database connection string, payment, or remote control of your computer to read a bug report.

Report a security or privacy defect through [SECURITY.md](SECURITY.md), not a public issue. Conduct concerns follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Google, Microsoft, Neon, Cloudflare, and upstream voice projects control their own accounts and services; Miu's maintainer cannot reset those accounts or guarantee their availability.
