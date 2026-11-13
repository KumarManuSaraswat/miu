# Windows speech bundle

The 1.0 Windows download includes Vosk 0.3.45 and its small US English 0.15 model,
plus Piper 2023.11.14-2 with the LJSpeech medium voice. They run locally in separate
short-lived helpers. Normal voice use needs no download, account or Python.

The exact distribution inputs are pinned in `scripts/prepare-windows-speech.ps1`;
the extracted file inventory is `scripts/windows-bundle.sha256`. Hashes identify
the reviewed bytes, not a publisher code signature. Miu redistributes these
upstream binaries unchanged. Notices are in `licenses/`, with the voice model
card beside the ONNX file. The original app code's MIT license does not relicense
these dependencies.

Vosk and this recognition model use Apache 2.0. Its native distribution includes
Kaldi, OpenFst, OpenBLAS, and MinGW runtime libraries with their accompanying
notices. GCC libraries carry the GCC Runtime Library Exception; winpthreads has
its separate notices.

Piper's older source and piper-phonemize are MIT, while the included eSpeak NG
component is GPL-3.0. The Piper executable links these components; redistribution
must respect the GPL component's terms as well as the individual MIT notices.
Corresponding speech sources and build scripts are included in the Windows
download's `third-party-source/` directory, including Piper, piper-phonemize,
eSpeak NG, ONNX Runtime, fmt and spdlog. Their license and third-party notices are
retained in those source archives as well. No installation information or keys
are needed to run rebuilt speech executables from a writable extracted folder.

The release's Piper source uses piper-phonemize from master as of its release;
the archived source snapshot is `fccd4f335aa68ac0b72600822f34d84363daa2bf`.
That source uses eSpeak NG `0f65aa301e0d6bae5e172cc74197d32a6182200f`, ONNX Runtime
1.14.1, fmt 10.0.0 and spdlog 1.12.0. The source archives include their upstream
CMake/build instructions. The release preparation script replaces floating
download references with these pinned redistribution inputs; when rebuilding
dependencies, select the documented versions rather than today's master.

LJSpeech contains public-domain recordings by Linda Johnson, aligned and
annotated by Keith Ito. Keep the included model card when redistributing it.
See [third-party components](THIRD_PARTY.md) for upstream links and the
[privacy notice](../PRIVACY.md) for runtime behavior.
