# Android verification record

## 1.0.1 reaction fix — 13 November 2026

The model-enabled development build passed **93 device checks** on the same
Android 16 ARM64 phone. The new checks exercise real Gemma replies with sad,
celebratory and informational messages, then simulate speech-start and
speech-finish callbacks to verify that Talk and the compact assistant retain the
response's expression while toggling mouth animation. No real microphone audio,
email, WhatsApp messages or cloud services were used for these checks.

The signed version 1.0.1 / version code 3 APK was built with the existing signing
key. Debug unit tests (including 18 matched Windows/Android text-reaction cases)
and release lint passed. These text-cue rules are not a general sentiment model;
nuance, sarcasm and unfamiliar wording can produce a calm fallback expression.

The signed 1.0.1 update was installed on the test phone. Its owner then confirmed
that the face changes with the reply and continues animating during speech.

## 1.0 offline model checks — 13 November 2026

The model-enabled development APK passed **75 device checks** on the same Android
16 ARM64 phone. This includes extraction/checksum verification of the bundled
Gemma 3 model, a capital-city answer, a contextual follow-up, Stop, clock routing
after cancellation, a real answer in the compact black assistant panel and model
process cleanup after replying or closing the panel. Synthetic final speech callbacks also verify automatic submission in both surfaces, optional uncertain-speech review, and Ancient Dragon creator credit. Actual microphone/provider behavior still needs the separate owner voice check. No cloud key or live catalog
was used in this run. Debug unit tests and lint passed. Model resource measurements
and the Gemma 4 comparison are in [OFFLINE_AI.md](../docs/OFFLINE_AI.md).

This does not establish universal phone compatibility or new live Gmail/WhatsApp
coverage. Those optional screen integrations remain experimental. Existing
owner-confirmed voice and screen results below are historical evidence, not a new
claim that every integration was re-tested with every model update.

## Historical 0.1 preview checks

Checked on 13 November 2026. This record separates automated checks from real service testing. It is not a guarantee of compatibility with every Android phone.

| Area | Evidence and limits |
| --- | --- |
| Local unit tests | 18 tests pass: command routing and bounds, greetings addressed to Miu, preserved note/message text, local responses, WhatsApp draft parsing, animation timing, long speech splitting and installed English selection. |
| Android lint | Debug and release lint pass with no errors. Warnings remain for English-only UI strings, newer tooling availability and accessibility metadata ignored on older Android versions. |
| Device UI checks | 60 checks passed on an Android 16 phone. These cover local commands, full and compact layouts, all expressions, actual keyboard visibility/focus, synthetic encrypted-key round trips, catalog validation, WhatsApp placeholder/real/whitespace draft regression cases and both cold/warm launches addressed to the old assistant component. These automated checks do not record microphone audio or operate third-party apps. |
| Release signing | The APK is signed with the project's dedicated Android certificate and verified with Android Build Tools. The release includes its certificate fingerprint and APK SHA-256 checksum. |
| Installation | The final signed preview installed and opened on the Android 16 test phone through normal owner-approved installation. The temporary development app and test runner were removed after checks. |
| Speech recognition and synthesis | The phone owner confirmed microphone recognition works. The installed provider exposes en-IN; Miu now discovers and selects it. The owner also confirmed that the requested Gmail body appeared and was spoken. This does not establish recognition accuracy or voice availability on other phones. |
| Assistant invocation | The owner selected Miu in Android's default-assistant picker and confirmed the long power-button press opens the black mascot panel. Android still retained an older MainActivity target on this device; Miu forwards that action to the compact panel. Keyboard layout was also visually checked. No hardware-button setting or permission was granted by the app. |
| Gmail | With owner-enabled Gmail-only accessibility access, the owner confirmed opened-message body display and speech. Search entry and a no-match result were inspected. The current Gmail conversation-row selector was updated, but automatic latest/search-result opening, long/collapsed messages and other Gmail versions still need broader manual coverage. |
| WhatsApp | With separately enabled access, the owner confirmed visible-text reading in their self-chat. An unflagged empty “Message” placeholder initially caused a false draft warning; the fix checks the voice control and absence of Send while retaining real drafts. The owner subsequently reported that review appears and sending works after their approval. No automated live Send was performed, and delivery was not independently verified. Live Cancel/expiry/chat-change coverage remains incomplete. |
| Preset catalog | Synthetic parsing/rejection checks and the opt-in live public catalog request passed on the phone. The catalog contains public presets only and receives no chat, mail or audio. |
| Memory observation | Final-build samples on Android 16 reported 22–33 MiB app PSS and 116–126 MiB app RSS, with one/two activity objects before/after opening the panel and keyboard, both optional screen services enabled, no floating overlay and no active speech turn. These are different accounting measures, exclude separate speech/keyboard-provider processes, and are neither device-wide totals nor peak-memory guarantees. |

The APK contains no speech model or local general-purpose language model. Offline speech requires an installed compatible Android provider and English packs. The local conversation engine is a small set of deterministic skills. General AI chat is optional and requires the user's own eligible provider account.

Before promoting this preview to a stable release, expand the [manual checks](TESTING.md), particularly accessibility cancellation/expiry, changed recipients/drafts, denied permissions, lock-screen behavior, larger fonts and older supported Android versions. Optional general AI was not part of the phone's offline/device tests. Report results without uploading personal messages, keys, account details or device identifiers.
