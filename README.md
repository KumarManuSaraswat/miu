# Miu

A small companion for Windows and Android with an expressive mascot, push-to-talk voice, optional Gmail reading, and a short list of useful device commands. Each app uses its platform's native UI without an embedded browser runtime.

Built by a group of developers led by **Ancient Dragon**. Miu's included offline AI uses Google's Gemma model.

![Miu's calm speaking expressions](assets/miu-speaking-preview.png)

**Miu 1.0 · Windows x64 and Android 9+ ARM64 · Offline Gemma 3 included.** Download, extract or install, and chat without an account, API key, server or GPU. Original application code is MIT licensed; bundled model weights have [their own terms](docs/MODEL_TERMS.md).

[Downloads](https://github.com/KumarManuSaraswat/miu/releases) · [Build status](https://github.com/KumarManuSaraswat/miu/actions/workflows/windows.yml) · [Commands](docs/COMMANDS.md) · [Support](SUPPORT.md) · [Contribute](CONTRIBUTING.md)

## Android

Download the signed **[Miu Android APK](https://github.com/KumarManuSaraswat/miu/releases/tag/v1.0.1)** on a phone and open it to install. The native Android app includes Gemma 3 1B for offline chat, 24 animated reactions, tap-to-talk using Android speech services, local phone commands, a floating mascot, and the optional Neon-backed preset catalog. Allow about 3 GB free storage for installation and first-use preparation. A 64-bit ARM phone with Android 9+ and at least 4 GB RAM is recommended. Voice requires microphone permission and a suitable installed English speech pack; typing works independently.

Choose Miu through **Settings → Use Miu as phone assistant** to open a compact black mascot panel with your phone's assistant gesture. It supports voice, typing, animated replies and an explicit Open app button. The full app has a refreshed Talk, Looks and Settings layout. Power-button behavior depends on the phone; Miu does not intercept hardware keys or need overlay permission for this panel.

Android Gmail support reads opened message bodies through optional Gmail-only accessibility access. It uses the account selected in the phone's Gmail app, without OAuth credentials or notification-preview substitution. Search and latest-message navigation depend on Gmail's interface; opening a message may mark it read. Email text stays on the phone and is excluded from AI requests. See the **[Android guide, privacy details and build instructions](android/README.md)** before enabling permissions.

Optional WhatsApp access reads visible chat text and prepares explicitly requested messages for review before a single Send action. Screen integrations are experimental and depend on the installed apps' controls. See the [Android verification record](android/VERIFICATION.md) for what has and has not been tested.

The remaining sections describe the **Windows** app. Its Gmail API integration and voice setup differ from Android.

## Windows: start

Download `miu-1.0.1-windows-x64.zip` from [Releases](https://github.com/KumarManuSaraswat/miu/releases/tag/v1.0.1), extract it into a writable folder, and double-click **Start Miu.cmd**. Keep its folders together. Windows with .NET Framework 4.8 is required; Windows 11 x64 and 8 GB RAM are recommended. The executable is currently unsigned. Releases include SHA-256 checksums.

The ready-to-use download includes Gemma, offline English recognition and the natural English voice. No setup scripts or separate AI downloads are needed. Allow about 3 GB free space for the ZIP and extraction. Dependency licenses and speech sources travel with the download; see [speech bundle details](docs/SPEECH_BUNDLE.md). Developer/CI archives omit downloaded runtimes and are not the end-user bundle.

- Click the mascot to open chat; drag it to move it.
- Tap the microphone or press **Ctrl+Alt+Space**. Wait for **Listening offline**, speak one request, then pause.
- The meter shows input activity. Uncertain recognition is put in the message box for review before anything runs.
- Tap the mic during a reply to interrupt and start listening. Press **Esc** to cancel.
- Open **Reactions** for 24 faces. Open **Settings → Choose a companion preset** for face, motion, and voice-speed profiles.
- Close chat to keep the mascot. Use its right-click or tray menu to sleep or quit.

The microphone is off when idle. Recognition currently supports English, with accuracy depending on the microphone, accent, and background noise. The app is a small command assistant; it cannot perform arbitrary computer tasks.

## What it can do

| Feature | Example | Connection |
| --- | --- | --- |
| Local facts | “What time is it?”, “Check my battery” | None |
| Open supported apps | “Open calculator”, “Open downloads” | None |
| Notes and volume | “Take a note buy milk”, “Volume down” | None |
| Expressive companion | “Look playful”, “Show reactions” | None |
| Voice conversation | One spoken turn at a time | Included offline Windows speech |
| Read and search Gmail | “Latest email”, “Emails from Alex” | Your Google Desktop OAuth setup |
| Open-ended AI chat | Ask a general question | Included Gemma 3, offline |
| Community presets | Load curated companion profiles | Optional Neon-backed catalog |

See the [command guide](docs/COMMANDS.md). Commands use an explicit allowlist. Neither model replies, email text, nor downloaded presets can execute arbitrary commands.

## Optional connections

### Gmail

Follow [Gmail setup](docs/GMAIL_SETUP.md), import your own Google Desktop OAuth JSON, and sign in. Only `gmail.readonly` is requested. Miu does not send, delete, or mark messages read. Gmail results are processed locally and are not added to AI conversation context. A shared public OAuth client has additional Google verification requirements; the release does not ship the maintainer's credentials.

### AI chat

Offline Gemma 3 chat is enabled by default. The small model can make mistakes; see [measurements, limits and resource requirements](docs/OFFLINE_AI.md). Its output cannot execute commands. To use optional Gemini instead, explicitly turn Offline AI off and cloud chat on. Gemini sends your chat text and bounded context to Google, requires your own eligible API project, and has provider quotas and terms. Miu cannot verify your project's billing status.

Use **Settings** to configure a key and model. Consult [Gemini pricing](https://ai.google.dev/gemini-api/docs/pricing), [terms](https://ai.google.dev/gemini-api/terms), and [privacy details](PRIVACY.md) before enabling it. No API key is included in a release.

### Neon community catalog

The optional service in [services/catalog](https://github.com/KumarManuSaraswat/miu/tree/main/services/catalog) stores curated presets in **Neon Postgres** and exposes a small read-only API through a **Cloudflare Worker**. Profiles such as Focus and Cheerful contain only a known face, speaking speed, and reduced-motion preference.

```text
Miu -- explicit public GET --> Cloudflare Worker -- read-only query --> Neon Postgres
```

Database credentials stay in the Worker secret store. The desktop app receives no database password and sends no email, chat, audio, or account credentials to the catalog. A fetch shares ordinary connection metadata, including IP address, with the host. There is no background refresh or user-upload endpoint.

Three built-in profiles work offline. The [public Miu catalog](https://miu-preset-catalog.kumar-saraswat-s-125.workers.dev/v1/presets) is live with six curated profiles. To try it, open **Companion presets → Community catalog**, paste this address, and click **Load community presets**:

```text
https://miu-preset-catalog.kumar-saraswat-s-125.workers.dev/v1/presets
```

Apply a profile explicitly after loading. The published **v0.1.0** app still has a blank catalog address by default; no endpoint was added to its release assets. Loading the catalog is a manual, optional connection.

The service runs on Neon Free and Workers Free. Its live response and all six profiles have been checked with the desktop catalog client. Hosting quotas still apply; this is not a promise of unlimited availability. Built-in presets remain available if the service is unavailable. Deployment instructions include quota checks and read-only credentials.

## Memory and privacy

The language model loads only while answering. CPU benchmarks measured about 0.91 GiB for its Windows process and 1.29 GiB RSS on the tested Android phone, in addition to UI and speech memory. The small native UI stays available between turns without retaining that model process. These are sample measurements, not fixed limits; the UI memory figure excludes helper processes. See [the benchmark record](docs/OFFLINE_AI.md).

Vosk loads for each listening turn; Piper loads to generate each spoken reply. Both exit when finished. Chat stays in memory for the session. Notes and Windows-encrypted account secrets stay in `%LOCALAPPDATA%\Miu`. No app telemetry or automatic mail polling is implemented. See [PRIVACY.md](PRIVACY.md) for optional network services and data removal.

## Develop and validate

The desktop build uses the .NET Framework compiler supplied with Windows. It needs no .NET SDK, NuGet, npm, or Python. Node.js is needed only for the optional catalog service.

```powershell
git clone https://github.com/KumarManuSaraswat/miu.git
cd miu
.\build.ps1 -Test
.\scripts\verify-release.ps1 -SelfTest
.\scripts\package-release.ps1
```

The desktop suites cover commands, safety, Gmail parsing, speech handling, and the catalog client. They make no live Gmail or AI calls. The catalog has separate tests and an opt-in live database check described in its README. GitHub Actions builds the app, runs checks, and produces verified artifacts; it does not publish or deploy automatically.

Public packages use explicit file allowlists and include SHA-256 checksums. Local configuration, accounts and credentials are excluded. The ready-to-use downloads include verified model/runtime files; the source ZIP and CI artifacts omit them. Read [release instructions](docs/RELEASE.md) before distributing a build. Microphone compatibility and optional hosted services need their own checks.

## Contribute and support

Start with [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports, documentation improvements, accessibility feedback, and reproducible speech issues are welcome. Use [SUPPORT.md](SUPPORT.md) for help and [SECURITY.md](SECURITY.md) for private vulnerability reports. See the [code of conduct](CODE_OF_CONDUCT.md), [governance](GOVERNANCE.md), and [changelog](CHANGELOG.md).

Miu is an independent project maintained by its repository owner and contributors. Original Miu source is under the [MIT license](LICENSE); upstream components retain their own licenses. It is not affiliated with the services it optionally connects to.

The [Neon sponsorship preparation notes](docs/NEON_SPONSORSHIP.md) describe eligibility and the remaining application steps. Neon does not require a project to use its database to apply. No sponsorship or funding is currently claimed.
