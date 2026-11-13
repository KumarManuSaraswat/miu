# Offline model and credits

Miu includes **Gemma 3 1B**, a Google model quantized to Q4_K_M and distributed
through Ollama's public registry. It is not Miu's own trained model. The native
inference runtime is llama.cpp. No account, API key or inference server is needed.

Gemma is provided under and subject to the Gemma Terms of Use found at ai.google.dev/gemma/terms

Using or redistributing the bundled model is subject to the [Gemma Terms](../licenses/GEMMA-TERMS.txt)
and [Gemma Prohibited Use Policy](../licenses/GEMMA-PROHIBITED-USE.txt), including
the restrictions in section 3.2. These restrictions are incorporated into the terms
governing the bundled model's use and distribution. The application's source remains
MIT licensed; the model weights have their own terms.

Copies of all supplied notices are in [licenses](../licenses/NOTICE.txt).
The model file is redistributed unchanged. Its pinned source and SHA-256 checksum
are recorded in the notice and preparation script.

The small model can answer incorrectly or incompletely. It receives your submitted
chat text and at most two short previous chat turns in memory. It receives no email,
screen text, notes or credentials automatically, and its output cannot execute device
commands. The model process exits after each reply, on Stop, or on timeout. Android
also cancels it when Miu leaves the foreground.

Android prepares a private model copy from the APK on first use. Allow about 900 MB
of free storage in addition to the installed APK. Windows reads the model included
beside the app. Both platforms verify its checksum before use. Model memory varies
with the device, context and runtime; see the measured results in OFFLINE_AI.md.
