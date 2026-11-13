# Android verification

## Automated checks

```sh
cd android
./gradlew :app:testDebugUnitTest :app:lintDebug :app:assembleDebug :app:assembleDebugAndroidTest
```

Unit tests cover local routing (including speech fillers), bounded timers, preserved notes, expression validation, and rejection of arbitrary or embedded commands. Lint checks the supported API range and manifest. No Gmail account or AI key is needed.

The optional custom instrumentation runner launches only Miu's separate `.dev` app, checks local command responses, draws every expression, checks encrypted key round trips with a synthetic value, and validates synthetic catalog data including rejected unsafe values. It does not read Gmail, enable permissions, record audio, or alter other apps. It resets the debug app's AI settings and uses synthetic test data; do not use this debug app as your personal installation. Add `-e online true` before the runner name to also fetch the public preset catalog; that check requires a working internet connection on the phone.

With a phone/emulator you authorize for testing:

```sh
adb install -r app/build/outputs/apk/debug/app-debug.apk
adb install -r app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk
adb shell am instrument -w io.github.kumarmanusaraswat.miu.dev.test/io.github.kumarmanusaraswat.miu.MiuInstrumentation
```

The runner must report `Miu device checks passed`. Add `-r` for raw output with `INSTRUMENTATION_CODE: -1`; a process crash is a failure even if the shell exits successfully. A successful install or unit test alone does not prove microphone or Gmail behavior.

## Manual release checks

- Install the signed release APK; launch, rotate, reopen, and check text at a larger system font size.
- Try typed time/date/battery, notes, an installed app, a timer, and cancellation.
- Grant microphone access through Android, say “what time is it,” and verify both recognition and audible speech. Deny permission and verify typing still works. Leave Miu mid-listening and confirm recording stops.
- Enable Gmail access yourself. Open a synthetic test message in Gmail, return to Miu and ask to read it. Check body content, not a notification/list preview. Test search/latest, unsupported controls, lock-screen cancellation, timeouts, long/collapsed messages, and Clear. Never upload real mail screenshots or text.
- Enable the overlay yourself; drag, tap, long-press, use the notification stop action, and lock/unlock. Confirm no microphone indicator while the mascot is idle.
- Enable WhatsApp access yourself and use a self-chat or explicitly authorized test recipient. Check visible-text reading, canceling a review, an existing draft, changed chat/text, expired approval and unavailable controls. An actual Send requires explicit authorization for that recipient and text; never send a test message to another person without it. Offline queued delivery is not successful delivery.
- Observe blinking, a sideways glance, a reaction burst and speech motion. Verify Reduced motion makes the mascot still and background windows stop animation work.
- Preview the compact assistant. Check the black panel, readable replies, typing with the keyboard, Close/outside dismissal and explicit Open app action. Select Miu through Android's assistant picker yourself, then test the phone's supported assistant gesture. Confirm the full app does not open for `ACTION_ASSIST`. Test both tap-to-talk and the optional invocation-listening setting, including rotation, lock and cancellation.
- Load community presets with and without connectivity. Applying a preset must only change expression, speed and motion.
- Verify optional cloud chat with an eligible personal free-tier key only if explicitly enabled; do not use mail as a prompt. Never bundle test credentials.
- Measure `adb shell dumpsys meminfo io.github.kumarmanusaraswat.miu` after settling idle. Report Android version, whether the overlay is active, and whether speech providers are included. Memory varies by phone; APK size is not RAM use.

## Release checklist

Use the same backed-up private signing key for updates, increase `versionCode`, verify the APK with `apksigner verify --verbose --print-certs`, and publish SHA-256 checksums. Confirm no debug flag, test runner, key, account data, build cache, logs, or local settings are in release assets. Include actual test coverage and limitations in release notes. The Android tag is separate from the Windows release.
