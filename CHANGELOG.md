# Changelog

## Android 0.1.0 preview

- Native Android 8.0+ app with 24 vector expressions, a gentle speaking face, tap-to-talk, local phone commands, optional floating companion and Quick Settings shortcut.
- On-demand Android speech recognition, offline English text-to-speech, uncertainty review, cancellation and microphone cleanup when leaving the app.
- Optional Gmail-only accessibility access for opened message bodies and best-effort Gmail search/latest-message navigation. No notification-preview substitution, mailbox credentials or email upload. Opening messages may mark them read.
- Optional user-key Gemini chat isolated from email/notes, Keystore encryption, disabled app backups, and explicit loads from the free Neon-backed public preset catalog.
- Offline greetings, moods, jokes, ideas and bounded arithmetic without a model/server; sparse animated blinks, glances, head motion and reaction bursts with reduced-motion support.
- Optional WhatsApp visible-chat reading and a reviewed message flow that rechecks the recipient title/text and attempts Send only once; no Business API or AI-generated sends.
- Separate signed APK releases, pinned Android build tooling, CI, command tests, device instrumentation, privacy disclosures and contributor instructions. Device/provider limitations are documented in the Android guide and each release's test report.

## Windows 0.1.0 preview

The initial Windows preview is published under tag `v0.1.0`. The Android app has a separate release tag and installation process.

### Added

- Native Windows x64 desktop companion with a draggable charcoal mascot, tray controls, chat panel, keyboard shortcut, reduced-motion option, and 24 selectable reactions.
- English push-to-talk, input-level feedback, partial captions, cancellation, and review of uncertain recognition before sending.
- Optional local Vosk recognition and Piper natural speech, loaded on demand, with Windows speech fallback when available.
- Read-only Gmail OAuth connection, inbox lookup, sender/subject search, and local message readout using a user's own Desktop app OAuth client.
- Explicit local commands for selected apps and folders, notes, volume, time/date, battery, and browser search.
- Optional user-configured Gemini conversation, disabled by default, separated from fetched Gmail data and device execution.
- Three offline companion presets and an optional curated catalog backed by Neon through a read-only Cloudflare Worker API; no shared hosted endpoint is bundled.
- MIT licensing for original project source, contribution and security guidance, privacy documentation, and lean release packaging with checksum verification.
- Contributor support, conduct and governance policies, a source map and maintainer guide, and issue/pull-request templates.

### Changed and fixed during development

- Replaced the ordinary happy/speaking face with softer pill eyes and compact mouth animation.
- Preserved expression selection during speech and added accessible names for the reaction gallery.
- Improved Gmail sign-in recovery, callback handling, message cleanup, and actionable setup errors.
- Prevented late Gmail results from restoring cleared conversation data; canceled or obsolete searches and message reads are discarded.
- Preferred the installed offline recognizer, corrected live audio buffer order, increased recording headroom, and retained uncertain transcripts for review.
- Added bounds, cancellation, and lifecycle handling for optional native voice processes.

### Release limitations

- Windows x64 and .NET Framework 4.8 are required. Voice support is currently English; accuracy varies by microphone, accent, and background noise.
- Lean release archives omit third-party voice binaries and models. Users install these separately if wanted.
- Gmail requires a separate account setup. A personal OAuth testing setup is not a verified public shared sign-in service.
- Gemini eligibility, availability, quotas, privacy, and billing follow Google's current terms. Broad public free-tier availability is not promised.
- No signed binary, independent security audit, automatic updater, or arbitrary computer-control agent is claimed.
- Automated and synthetic checks do not establish successful sign-in for every account, microphone performance on every device, or production readiness.
