# Contributing to Miu

Miu is an early native companion for Windows and Android. Contributions should make its small set of tasks more reliable, understandable, and economical to run. The local command assistant must remain usable without a paid account.

## Build locally

For **Android**, follow [android/README.md](android/README.md#build-from-source) and [device testing](android/TESTING.md). It uses Java 17 source, Android SDK 36 and the pinned Gradle wrapper, with no third-party runtime libraries. Android source lives under `android/`; credentials use Android Keystore. Gmail access is an optional bounded accessibility workflow, not the Windows Gmail API: it cannot send or delete mail, but opening a message may mark it read. Document actual phone and provider coverage; do not equate a compiled APK with a microphone or Gmail integration test.

The following build instructions are for **Windows**.

Use Windows x64 with .NET Framework 4.8; Windows 11 is the primary target. Run **Windows PowerShell 5.1** (`powershell.exe`, not `pwsh`). The desktop build uses the compiler included with .NET Framework; no npm, NuGet, Python, or .NET SDK installation is required. Source must remain compatible with **C# 5**. The optional catalog service has a separate Node.js toolchain described in its [README](https://github.com/KumarManuSaraswat/miu/tree/main/services/catalog).

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

This compiles the app and runs the offline application checks. Build output belongs in `dist`, which is not source. The setup scripts are optional network operations and are not needed to work on most interface or command changes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-voice.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-natural-voice.ps1
```

See [third-party components](docs/THIRD_PARTY.md) before working on voice dependencies. Downloaded engines, models, archives, and local credentials must not be committed.

## Propose a change

For a substantial feature, open an issue describing the user problem, proposed behavior, and memory or account requirements. A small bug fix can go straight to a pull request. Do not attach account secrets, mailbox screenshots, recordings of other people, or unredacted diagnostic files.

Keep pull requests focused. Describe the behavior before and after the change, how it was checked, and any remaining limitations. UI changes should include a clean screenshot using synthetic example text. Voice fixes should distinguish a synthetic WAV check from an actual microphone test; do not claim one proves the other.

Use the issue forms for reproducible bugs and feature requests. General setup help is in [SUPPORT.md](SUPPORT.md). Security reports follow [SECURITY.md](SECURITY.md), not a public bug report. Project decisions and conduct follow [GOVERNANCE.md](GOVERNANCE.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

Fork the repository, create a focused branch, and submit a pull request against `main`. Follow the existing style and avoid unrelated formatting changes. There is no CLA or mandatory commit sign-off process. You remain responsible for reviewing submitted work, including code or documentation generated with assistance. The [maintainer guide](docs/MAINTAINING.md) maps the code and explains review priorities.

## Design boundaries

- Idle operation must not keep a microphone, neural model, browser runtime, or polling loop running unnecessarily. Report app memory and temporary worker memory separately.
- Device actions must come from explicit, bounded commands. Model replies, email bodies, web content, and imported documents are data, not permission to run commands.
- Gmail supports reading only. Keep message data out of the optional cloud conversation path. Android's screen workflow can mark opened messages read, as its disclosure explains; it must never compose, send, delete or archive.
- Cloud conversation is optional and disabled by default. Adding another provider requires clear data-flow documentation and a current check of that provider's eligibility and billing terms.
- Credentials use the platform's protected store: Windows user-scoped DPAPI or Android Keystore. Never add a bundled API key, shared refresh token, personal OAuth JSON, signing keystore, or credential logging.
- Add timeouts, cancellation, and resource cleanup when introducing network requests or child processes.

Add or update focused checks when a behavior, boundary, or bug warrants them. Appearance-only changes normally need a rendered visual check rather than tests that mirror the drawing code. Tests should use synthetic data and avoid real Gmail, cloud keys, microphone access, audio playback, or device mutations unless explicitly run as a manual integration check.

`MIU_DATA_DIR` selects a separate settings directory for development. Choose a disposable test directory; do not point tests at your normal Miu profile. Explain how to reproduce your checks without sharing that directory.

## Licensing and assets

Miu's original source and documentation use the [MIT License](LICENSE). Submit only work you have the right to contribute under that license, and preserve existing attribution. Third-party components keep their own licenses; Miu's license does not replace them.

For new icons, models, illustrations, or other assets, document their origin and license. Do not copy another product's branding, distribute an unlicensed reference image, or describe Miu as affiliated with a provider without authorization. Public identity and visual provenance checks are part of the [release process](docs/RELEASE.md).

The project currently has no promised review turnaround, paid support agreement, or contributor reward scheme.

## Offline model changes

Read [the offline AI guide](docs/OFFLINE_AI.md) before changing inference. Keep model output separate from device execution, preserve explicit cloud selection and cancellation, and record measured memory and latency. Large model/runtime binaries belong in release assets, not Git. Model licenses are separate from Miu's MIT source. The ready package is built from checksum-pinned inputs; update the manifest only after reviewing the new files.
