# Android 0.1.0 preview verification

This record separates automated checks from real service testing. It is not a guarantee of compatibility with every Android phone.

| Area | Evidence and limits |
| --- | --- |
| Local unit tests | 17 tests pass: command routing and bounds, local responses, WhatsApp draft parsing, animation timing, long speech splitting and installed English selection. |
| Android lint | Debug and release lint pass with no errors. Warnings remain for English-only UI strings, newer tooling availability and accessibility metadata ignored on older Android versions. |
| Device UI checks | 40 checks passed on an Android 16 phone before the final English-selection and speech-lifecycle fixes. These cover local commands, layouts, expression drawing, synthetic encrypted-key round trips and catalog validation; they do not exercise a real microphone or third-party screen service. |
| Release signing | The APK is signed with the project's dedicated Android certificate and verified with Android Build Tools. The release includes its certificate fingerprint and APK SHA-256 checksum. |
| Installation | A signed preview installed and opened on the Android 16 test phone. USB installation required the phone owner's approval. The final build remains available for another device pass. |
| Speech recognition | The first device attempt failed because Miu requested en-US while the phone had en-IN installed. The updated app successfully queried that provider, detected en-IN and selected it. A spoken-turn retest and audible-reply confirmation are still pending. |
| Gmail and WhatsApp | Implemented with separate optional, package-filtered accessibility services. End-to-end tests against the real apps are pending user-enabled access. No live WhatsApp test message was sent. Treat navigation, reading and sending as experimental. |
| Preset catalog | Synthetic parsing and rejection checks pass. The phone's live request encountered a connectivity failure; successful live loading on that phone is not claimed. |
| Memory observation | One signed-preview idle sample on Android 16 reported about 20 MB total PSS, with one activity, no overlay and no active speech turn. This excludes separate speech-provider processes and is not a device-wide RAM total or a maximum. The final build has not been remeasured. |

The APK contains no speech model or local general-purpose language model. Offline speech requires an installed compatible Android provider and English packs. The local conversation engine is a small set of deterministic skills. General AI chat is optional and requires the user's own eligible provider account.

Before promoting this preview to a stable release, complete the [manual checks](TESTING.md), including real speech, accessibility cancellation, verified recipient review, denied permissions, lock-screen behavior and older supported Android versions. Report results without uploading personal messages, keys, account details or device identifiers.
