# Android third-party notices

Miu's Android application source, original code-drawn mascot and documentation use the repository's [MIT License](../LICENSE). User-supplied visual references are not bundled. The ready-to-use 1.0 APK includes Gemma 3 1B Q4_K_M and the ARM64 llama.cpp CPU runtime. Their terms are separate from the app license: see [model terms](../docs/MODEL_TERMS.md) and [preserved notices](../licenses/NOTICE.txt), also included in the APK's legal assets. Settings exposes the model terms and credits. Plain developer/CI APKs omit those model/runtime binaries.

Build and test tooling retains its own licenses:

- [Gradle](https://github.com/gradle/gradle), including the checked-in wrapper: Apache License 2.0. The wrapper scripts retain their upstream license headers. See [LICENSE-GRADLE.txt](LICENSE-GRADLE.txt).
- [Android Gradle Plugin and Android build tools](https://source.android.com/docs/setup/about/licenses): their respective Android project and SDK terms apply. SDK components are installed separately, not redistributed in Miu's release assets.
- [JUnit 4](https://github.com/junit-team/junit4): Eclipse Public License 1.0, test-only.
- [Hamcrest](https://github.com/hamcrest/JavaHamcrest): BSD 3-Clause, transitive test-only dependency of JUnit.

Android's speech-recognition and text-to-speech services are supplied by the phone or separately installed providers. Their licenses, language packs, processing policies and permissions apply independently of Miu's source license.

Gemini, Gmail, Android, Neon and Cloudflare are third-party services or marks. This project does not claim their endorsement or funding.
