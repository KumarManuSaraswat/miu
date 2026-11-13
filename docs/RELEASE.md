# Preparing and releasing Miu

The **1.0.0** release bundles offline AI on Windows x64 and Android ARM64. Keep source version, Android version code, tag and tested binaries aligned; reuse the existing Android signing key for updates. Preparing artifacts does not publish them.

## Ready-to-use release

```powershell
./scripts/prepare-local-ai.ps1 -Platform all
./scripts/prepare-windows-speech.ps1
./build.ps1 -Test
./scripts/package-ready-release.ps1 -Version 1.0.0
./scripts/build-android-release.ps1 -Version 1.0.0
```

The Android step needs the SDK/JDK and `MIU_KEYSTORE_PATH` / `MIU_STORE_PASSWORD` supplied privately. Never commit or log signing secrets. It runs unit/lint checks, verifies the embedded model hash and notices, and verifies the signed APK. Windows packaging streams its large ZIP and verifies every entry. It uses the public document allowlist and pinned `windows-bundle.sha256` inputs; user settings and installed voice directories are not sources. Change that reviewed manifest only when intentionally changing dependencies.

Run model-enabled Android instrumentation (`-e model true`) with `-PwithLocalAi`, then test the signed APK. Test the extracted Windows bundle, including real inference and cancellation. Preserve measurements and distinguish automated checks from owner-confirmed microphone/accessibility behavior. One device does not establish broad compatibility or battery performance.

Publish the ready Windows ZIP, signed APK, clean source ZIP, signing-certificate fingerprint and consolidated SHA-256 list under **v1.0.0** after checks pass. CI's lean Windows ZIP must not replace the end-user download. Keep speech source archives and license notices in the bundle. The 806 MB model is excluded from Git.

## Lean source/CI packaging

The commands below validate source/build inputs without downloading models. Their source ZIP is suitable for publication; their lean Windows ZIP is a CI artifact, not the ready-to-use release.

## Distribution policy

The source package contains Miu's original source, tests, build/release scripts, documentation, project assets, and the optional community-catalog service. The portable package contains the app, its recognition-worker wrapper, launch script, documentation, and optional voice download scripts. Deploying the catalog service is a separate maintainer operation; its source is not included in the portable app ZIP.

**Neither release package contains downloaded Vosk/Piper engines, native dependency DLLs, or voice models.** Windows and .NET Framework are prerequisites, not redistributed project files. The optional setup scripts download upstream voice components after the user chooses to install them.

The [MIT License](../LICENSE) applies to Miu's original contributions. It does not relicense upstream software or models. In particular, the optional older Piper native archive includes components with different licenses, including the eSpeak NG GPL component; the archive is not an MIT-only bundle. See [THIRD_PARTY.md](THIRD_PARTY.md). Do not add downloaded voice folders to a release ZIP without first reviewing the exact versions' license notices, redistribution and source requirements. A separate process or download script is not, by itself, a legal conclusion about every combination of software.

## Build, package, and verify

Run these from a clean project root on Windows x64 with .NET Framework 4.8 and **Windows PowerShell 5.1** (`powershell.exe`, not `pwsh`):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 1.0.0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-release.ps1 -Version 1.0.0
```

The packager uses already-built binaries and an explicit file allowlist. It validates its output and refuses to overwrite a populated release directory. For a second review build, pass the same new `-OutputDirectory`, such as `artifacts\releases\1.0.0-review2`, to both packaging and verification commands.

The voice installer's offline guards can be checked with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-voice.ps1 -SelfTest`. Its `-CheckCache` option inventories existing cached archives without installing or downloading. A normal install stages files, preserves upstream archive contents, validates the model when the worker is available, and retains a previous installation in `.cache/voice` before replacement. These guard checks do not prove a fresh network download or a real microphone works; test those separately when changing the installer.

Expected output under `artifacts\releases\1.0.0`:

```text
miu-1.0.0-source.zip
miu-1.0.0-windows-x64.zip
SHA256SUMS.txt
```

Do not zip the entire working directory or copy its existing `dist` directory wholesale. A developer's workspace may contain downloaded runtimes, caches, diagnostics, recordings, and private account data that are intentionally excluded from release packages.

## Review before uploading

- Verify release version metadata, changelog, archive names, and the exact source commit all agree. Record the successful checks for that build.
- Extract the portable ZIP into a fresh directory. Confirm startup without optional voices, typed local commands, settings, quit, and the no-credentials experience. Install optional voice files there only when deliberately testing those integrations.
- Distinguish parser tests, synthetic audio replay, real microphone checks, real Gmail authorization, and cloud API checks. Mark unperformed checks accurately.
- Inspect both ZIP inventories and run the verifier. Confirm no OAuth JSON, `.protected` files, API keys, mailbox exports, recordings, personal notes, diagnostic logs, local caches, or downloaded native models are included.
- Review the mascot, icon, project name, screenshots, and other artwork for documented provenance and publication rights. Do not distribute reference images without publication rights or claim endorsement by another product. Code-drawn assets do not establish trademark clearance; no name/mark clearance is claimed.
- Confirm the reviewed source commit is publicly accessible and the linked CI run passed for that commit. Retain the check results; a local success is not a completed GitHub Actions run.
- Verify the private vulnerability-report form remains enabled and reachable without submitting a test advisory. Confirm support/security links and the privacy notice match the distribution and any service operator.
- Decide how to handle binary signing. This preparation has no signing certificate or publisher identity. Describe unsigned downloads accurately; never tell users to disable Windows protection. A checksum is not a publisher signature, and a signature alone does not guarantee reputation. [Microsoft's SmartScreen overview](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/) explains its file and certificate reputation checks.

## Provider-specific public-launch gates

### Gmail

`gmail.readonly` is a **restricted scope**. A personal Desktop OAuth project with explicitly added testers can support development and eligible personal-use scenarios; it is not a substitute for a broadly distributed, verified shared OAuth client. Before offering a shared public sign-in, assess Google's current verification requirements and any documented exception. Server access to restricted data can introduce an additional security assessment requirement. [Google scope classification](https://developers.google.com/workspace/gmail/api/auth/scopes), [verification and exceptions](https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification).

For the initial lean preview, document bring-your-own personal OAuth setup and keep Gmail optional. Do not bundle a maintainer's client JSON or tokens or advertise Google verification that has not occurred. A shared public client needs appropriate branding, support/contact information, a public home page and privacy policy, and Google's review where applicable.

### Gemini

Review Google's current API terms before marketing the integration to a public audience. They include developer professional/business use, age and region requirements, and paid-service conditions for API clients made available in the EEA, Switzerland, and the UK. A user-supplied key does not remove those conditions. [Gemini API terms](https://ai.google.dev/gemini-api/terms).

Keep cloud chat optional and disabled by default. Do not market Miu as universally free, unlimited cloud AI, or direct the current API integration at children. If the intended audience or budget cannot satisfy the provider's requirements, release the local features without promoting that integration until an appropriate supported arrangement exists.

## Publication and sponsorship are separate steps

The optional Neon/Cloudflare catalog also needs a separately reviewed deployment: configure its read-only database access, check current free-plan limits, publish the operator's relevant notice, and test its actual endpoint. Do not embed database credentials in the desktop app or treat a local mock as proof of live service operation. No shared service address is configured by this release preparation.

Upload only reviewed artifacts after confirming the target repository, source commit, release notes, and account settings. Publish checksums with the release, download the assets again and verify their hashes, and link the matching source commit and build evidence. Mark a preview release accurately and describe outstanding manual checks. The Windows executable is unsigned. Android APKs use the retained project signing key; checksums establish file integrity, not independent trust in the publisher.

Neon eligibility and an application draft are in [NEON_SPONSORSHIP.md](NEON_SPONSORSHIP.md). Repository publication does not automatically enable GitHub Sponsors, guarantee a sponsorship, or authorize a sponsor badge.
