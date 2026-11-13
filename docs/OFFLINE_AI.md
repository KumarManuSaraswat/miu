# Offline AI in Miu 1.0

Miu bundles **Gemma 3 1B instruction-tuned, Q4_K_M** for Windows x64 and Android
ARM64. No account, API key, inference server or GPU is needed for text chat. The
model is Google's work, not a model trained by Miu. Miu's source is MIT licensed;
the weights use the [Gemma terms](MODEL_TERMS.md).

## Download and resources

The model alone is 806,058,400 bytes (769 MiB). Allow roughly **3 GB of free
storage** for downloading, installing and preparing the Android APK. Android 9+
and a 64-bit ARM device are required by the bundled native runtime; 4 GB or more
RAM is recommended. Windows needs x64 Windows with .NET Framework 4.8; 8 GB or
more RAM is recommended. These are practical recommendations, not a guarantee
that every device in that class will perform equally.

The app extracts and verifies its included model on first Android use. That
preparation needs no connection. Windows reads the included model directly.
The model subprocess exits after each answer, Stop, timeout, or app exit; Android
also cancels generation when its UI leaves the foreground. Keeping Miu available
does not keep the language model loaded. The Windows UI memory counter covers
the app itself, not its separate model and speech helpers.

Local commands run before AI. Model output is displayed and spoken as text; it
cannot invoke the command router. Voice recognition and speech are separate from
Gemma. Android uses the phone's installed speech services and English language
pack. Microphone permission, offline speech-pack installation, default-assistant
selection and optional screen access remain Android-managed setup steps.

## Conversational faces

Miu 1.0.1 selects a reaction from the typed or recognized request and the reply's
text. Good news can produce a delighted face, distress an empathetic face, jokes
a playful face, and explanations a curious face. These are small local rules,
not a second AI model or analysis of your voice's emotional tone. They can miss
nuance, sarcasm and unfamiliar wording. Unmatched conversation uses a calm face.

The reaction stays visible while the mouth animates during speech and after the
answer finishes. Android keeps it across rotation too. Listening and thinking
temporarily show their own activity faces. Clearing the conversation or choosing
a resting face resets the expression. Reduced motion still disables movement.
Reaction selection does not change the answer, persist an emotion profile, send
text to a service, or give generated text access to device commands.

## Measurements, 13 November 2026

CPU-only checks used an i5-11400H Windows laptop with 16 GB RAM and an Android 16
SM4450 phone with approximately 5.3 GiB usable RAM. No GPU acceleration was used.
Five short synthetic prompts covered identity, explanation, study planning,
arithmetic and summarization. These are small engineering samples, not a formal
quality ranking or a battery/thermal test.

| Model and platform | Model file | Answer timing | Peak model-process memory |
| --- | --- | --- | --- |
| Gemma 3 1B Q4_K_M, Windows | 806 MB | 2.0–4.1 s including a fresh process/load | 0.91 GiB working set |
| Gemma 3 1B Q4_K_M, Android | 806 MB | 4.0–9.3 s including a fresh process/load | 1.29 GiB RSS |
| Gemma 4 E2B LiteRT-LM, Windows | 2.59 GB | 1.7–6.1 s after 3.5 s initialization | 1.29 GiB working set; 2.44 GiB private commit |
| Gemma 4 E2B LiteRT-LM, Android | 2.59 GB | 3.4–13.4 s after 7.6 s initialization | 1.40 GiB RSS |

RSS/working set are not proportional memory (PSS), and exclude the Miu UI and
speech services. The Gemma 4 run also created a roughly 788 MB CPU cache on
Windows. Timings across engines have different initialization boundaries, shown
above. Gemma 3 decoded approximately 24–28 tokens/s on Windows and 8–16 on the
phone; Gemma 4 approximately 15–17 and 8–11 respectively in these samples.

Both models made imperfect replies. The first Gemma 3 system prompt produced an
acknowledgment instead of a requested summary; a revised prompt produced a useful
summary in the integrated Windows engine. Gemma 4 called itself Gemma rather
than Miu in the initial harness. A 96-token benchmark limit truncated a study
plan. Miu's integration instead bounds replies to about 800 characters, with
sentence trimming, a 2,048-token context, 1,200-character question limit and a
90-second generation deadline. It retains only two short prior chat turns.

Gemma 3 was selected for its much smaller download and practical CPU performance
on these two devices. Gemma 4 remains a possible future option. Neither is a
replacement for a large model's reasoning, reliable factual research, or a
complete autonomous device controller. Long conversations, complex coding and
multilingual accuracy have not been broadly evaluated.

## Privacy and providers

Offline chat passes the submitted text and bounded chat history through a local
pipe. No prompts, responses or audio are written by the model engine to a log.
Fetched email, screen text, notes and credentials are not automatically included.
The model file is checksum-verified before use. A missing or damaged local model
reports an error; it does not silently switch to cloud chat.

Gemini remains optional. To use it, explicitly disable Offline AI and enable cloud
chat with your own key. The Neon catalog remains a separate optional preset
service; neither Neon nor Cloudflare hosts Miu's inference.

## Build or replace the engine

Run `scripts/prepare-local-ai.ps1` to fetch the pinned model and native runtime.
It verifies full SHA-256 digests and sizes. Windows then uses `build.ps1 -Test`;
Android bundle builds pass `-PwithLocalAi`. Plain Android CI builds omit the model
and cannot be presented as ready-to-use offline-AI downloads.

`src/LocalChat.cs` and Android's `LocalChat.java` isolate inference from device
actions. A future model needs an adapted chat template, provenance and license
review, memory/cancellation tests and device evaluations. Changing only a model
name is insufficient. Keep weights and credentials out of Git.

Upstreams: [Gemma 3 distribution](https://ollama.com/library/gemma3:1b),
[llama.cpp b12110](https://github.com/ggml-org/llama.cpp/releases/tag/b12110),
[LiteRT-LM](https://developers.google.com/edge/litert-lm/overview).
