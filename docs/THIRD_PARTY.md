# Third-party components used by Miu

Miu's original code and documentation use the root MIT license. Upstream dependencies retain their own licenses. The 1.0 ready-to-use download includes Gemma 3 and native inference; Windows also includes speech components. See [model terms](MODEL_TERMS.md), [speech redistribution details](SPEECH_BUNDLE.md), and the supplied `licenses/` directory. The source and lean CI archives omit model/runtime binaries, Node packages and generated Worker bundles.

Miu's natural voice runs locally. The ready-to-use Windows archive contains the pinned runtime and voice in `dist/natural-voice`; developers can also install them with the setup script. Miu does not require a paid speech service or Python. The following upstream references describe these components; the release-specific file and source inventory is documented in [SPEECH_BUNDLE.md](SPEECH_BUNDLE.md).

## Natural speech engine

The installed executable is the official **Piper 2023.11.14-2 Windows AMD64** release, downloaded from [rhasspy/piper](https://github.com/rhasspy/piper/releases/tag/2023.11.14-2). This is the older standalone native distribution; that repository is archived. Ongoing Piper development is at [OHF-Voice/piper1-gpl](https://github.com/OHF-Voice/piper1-gpl), whose current distribution and license differ.

The older Piper source is MIT-licensed, copyright Michael Hansen. Its native distribution includes additional components with their own licenses; the entire package should not be described as exclusively MIT. The installer retains every file from the official engine archive and adds principal upstream license files under `natural-voice/licenses`.

| Component | Upstream license/source |
| --- | --- |
| Piper 2023.11.14-2 | [MIT license and source](https://github.com/rhasspy/piper/tree/2023.11.14-2) |
| Piper phonemization wrapper | [MIT license and source](https://github.com/rhasspy/piper-phonemize) |
| eSpeak NG phonemizer | [GPL version 3 license and source](https://github.com/rhasspy/espeak-ng/tree/0f65aa301e0d6bae5e172cc74197d32a6182200f) |
| ONNX Runtime | [MIT license and source](https://github.com/microsoft/onnxruntime/tree/v1.14.1) |
| fmt | [MIT license and source](https://github.com/fmtlib/fmt/tree/10.0.0) |
| spdlog | [MIT license and source](https://github.com/gabime/spdlog/tree/v1.12.0) |
| nlohmann/json | [MIT license and source](https://github.com/nlohmann/json/tree/v3.11.2) |

The upstream [Piper build configuration](https://github.com/rhasspy/piper/blob/2023.11.14-2/CMakeLists.txt) describes its dependencies. Keep the included copyright notices, license texts, and source references when sharing the downloaded runtime. The setup script downloads upstream artifacts rather than claiming these components as Miu's own work.

## English neural voice

Miu uses **en_US-ljspeech-medium**, a single-speaker English voice with a 22,050 Hz sample rate. Its [model card](https://huggingface.co/rhasspy/piper-voices/blob/bae641dcb5a608cff81ccaf7ff018baaca1a33e1/en/en_US/ljspeech/medium/MODEL_CARD) identifies the [LJ Speech dataset](https://keithito.com/LJ-Speech-Dataset/) as public domain. The dataset credits recordings by Linda Johnson, with alignment and annotation by Keith Ito. The Piper voices repository labels its model collection MIT; the individual model card is retained beside the downloaded model.

The installer pins the voice files to repository revision:

```text
bae641dcb5a608cff81ccaf7ff018baaca1a33e1
```

The model's published SHA-256, checked by setup, is:

```text
6f52a751e2349abe7a76735eb09dc1875298c77ea2342ffd2fef79ff81b87f22
```

The [upstream file page](https://huggingface.co/rhasspy/piper-voices/blob/v1.0.0/en/en_US/ljspeech/medium/en_US-ljspeech-medium.onnx) publishes this digest. The engine archive is pinned to its release URL. Setup verifies GitHub's release-asset digest when GitHub supplies one; older assets can lack that metadata. `install-manifest.json` records downloaded file hashes and whether a published engine digest was verified. A locally calculated hash alone is not a publisher signature.

The engine archive is about 21.4 MB and the model about 63.5 MB, plus a small configuration and notices. The installer preserves the download archive and any prior installation in the workspace's `.cache` directory.

## Runtime behavior

Piper starts only to synthesize a requested reply, receives text through standard input, and returns raw PCM through standard output. Miu bounds that audio to 16 MiB, wraps it as WAV in memory, and plays it with Windows. It does not save spoken text or generated speech files. The native model process exits before playback begins; no neural speech model stays loaded between replies. Windows speech remains available as a fallback if the natural voice is missing or cannot generate a reply.

## Offline recognition

The optional `scripts/setup-voice.ps1` downloads **Vosk API 0.3.45 for Windows x64** and **vosk-model-small-en-us-0.15** into `dist/voice`. The API source uses [Apache License 2.0](https://github.com/alphacep/vosk-api/blob/v0.3.45/COPYING); Alpha Cephei's [model table](https://alphacephei.com/vosk/models) lists Apache 2.0 for this particular small English model. Different Vosk models can have different licenses.

The [native release](https://github.com/alphacep/vosk-api/releases/tag/v0.3.45) also contains third-party native libraries. Its Vosk license alone is not a complete statement of every bundled library's terms. The installer preserves the full native archive contents and model files, adds `MIU-DOWNLOAD-NOTICE.txt` with source references, and records `SHA256SUMS.local.txt`. It stages installation under `.cache/voice`, checks the model using Miu's worker when available, and preserves any previous installation before replacement. Those locally computed hashes record the downloads; setup does not verify them against an independently authenticated publisher digest. Keep the original archives and obtain the exact libraries' required notices/source before separately redistributing an installed runtime. Neither runtime nor model is included in the lean CI archive. The ready-to-use 1.0 bundle includes the reviewed runtime and model, with the notices and corresponding sources described in SPEECH_BUNDLE.md.

## Optional catalog service

The optional service under `services/catalog` has its own Node.js dependency tree, pinned by `package-lock.json`. It is not part of `Miu.exe`, and desktop users do not install Node.js.

| Direct dependency | Role | Upstream license/source |
| --- | --- | --- |
| `@neondatabase/serverless` 1.2.0 | Runtime HTTP database driver in the optional Worker | [MIT, Neon Inc.](https://github.com/neondatabase/serverless); installed package includes `LICENSE`. |
| `wrangler` 4.161.0 | Development, checking, and deployment tool | [MIT OR Apache-2.0, Cloudflare workers-sdk](https://github.com/cloudflare/workers-sdk/tree/main/packages/wrangler); see the installed package's license files. |

The Neon package prebundles additional code, including node-postgres, buffer, and ieee754. These retain their upstream terms; the direct package's MIT label does not replace their notices. The service retains identified runtime license texts in [the preserved source banners](https://github.com/KumarManuSaraswat/miu/blob/main/services/catalog/src/index.js), including Neon/node-postgres/buffer MIT notices and ieee754's BSD-3-Clause notice. Wrangler's bundle must retain these banners. This is not an exhaustive audit of upstream provenance. Wrangler's toolchain has additional transitive and optional packages under their own licenses. Do not describe the complete development dependency tree as exclusively MIT, and do not copy `node_modules` into desktop releases.

When changing these versions, review the installed packages, generated bundle, and transitive dependency notices again. A lockfile fixes a dependency selection but is not a license-compliance certificate.

## Windows components

Windows speech recognition, Windows speech synthesis, and .NET Framework are system components supplied by Microsoft under their applicable terms. They are prerequisites, not redistributed Miu source or downloaded voice bundles.
