# Changelog

## Unreleased — initial 0.1.0 release candidate

This entry describes the prepared implementation. No public release date, download count, or production support status is implied.

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
