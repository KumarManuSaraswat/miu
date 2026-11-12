# Miu privacy notice

Last reviewed: **12 November 2026**. This notice describes the 0.1.0 desktop implementation and lean distribution. It does not assert approval by Google or another provider.

Miu has no project-operated user-account server, analytics service, or automatic crash upload. Local commands work without a cloud account. Optional Gmail and Gemini features communicate directly with Google when used; optional voice setup downloads files from their upstream hosts. The optional community catalog is described below and has no shared hosted address configured by default.

## Information used by each feature

| Feature | Information processed | Destination and retention |
| --- | --- | --- |
| Local commands and chat interface | Your typed or recognized command and the displayed reply | Process memory. The app does not save a conversation transcript. Explicit notes are an exception below. |
| Push-to-talk | Microphone audio, recognized words, and input level during the requested turn | Local Windows recognition or the optional Vosk worker. Normal operation does not upload or save recordings. The microphone is off while idle. |
| Spoken replies | Reply text, which can include an email you asked to hear | Local Windows synthesis or optional Piper. Piper receives text through a pipe and returns audio held in memory; normal operation does not save generated speech. |
| Gmail | OAuth credentials, search queries, message identifiers, headers, snippets, and requested message text | Google sign-in and Gmail API over HTTPS, then local process memory. No background mailbox polling or ordinary attachment download. |
| Optional Gemini chat | Your current general-chat message, up to six preceding general-chat exchanges, the app's conversation instructions, and your API key for authentication | Google's Gemini API over HTTPS. Cloud chat is disabled by default and requires your own key and explicit enablement. |
| Web search or browser commands | The URL or search phrase you request | Your default browser and the chosen destination. Browser history and provider handling are outside Miu's storage controls. |
| Optional community presets | A public GET request to the catalog address you enter, with normal connection metadata such as your IP address | The catalog operator's Cloudflare Worker; curated presets are read from Neon. The app sends no chat, email, audio, or account credentials to this catalog. Applying a preset changes only the documented local preferences. |
| Voice installation | Requests for engine/model files and normal network metadata such as your IP address | Upstream GitHub, Hugging Face, and Alpha Cephei download services, as applicable to the selected setup script. These scripts do not send conversation or mailbox contents. |

Miu's Gmail results are not automatically included in Gemini prompts or its conversation history. General chat still sends the text you choose to type or dictate: manually pasting private information into that chat can send it to Google. Do not treat the email separation as a filter that removes sensitive material from your own messages.

Windows voice typing activated with **Win+H** is a separate Windows feature. Its permissions, network use, and privacy settings are controlled by Windows, not by Miu's offline recognition path.

## Local files

The default data folder is `%LOCALAPPDATA%\Miu`. Developers can override it with `MIU_DATA_DIR`.

- `settings.json` stores ordinary preferences such as voice, cloud enablement, model selection, speech rate, mascot position, and any community catalog address you explicitly load. It is not encrypted.
- `gemini-key.protected`, `gmail-client.protected`, and `gmail-token.protected` hold Windows user-scoped DPAPI-encrypted secrets. The running app must decrypt them to make a request. This protection is not a barrier against software already running with your account's privileges.
- `Notes` contains plain-text files only when you ask Miu to save a note. Notes remain until you remove them.
- Downloaded voice engines and models are stored under the app's `dist\voice` and `dist\natural-voice` directories. Setup scripts may retain archives, previous installations, hashes, and notices in the project/package's `.cache` directory.
- Explicit diagnostics and self-tests write `diagnostics.txt` and `self-test-results.txt` beside the executable. A startup failure can write `startup-error.txt`. These are not uploaded automatically; review them for file paths or other private information before sharing.

Your original Google credentials JSON stays wherever you downloaded it. Importing it creates an encrypted local copy; Miu does not remove the original file.

Chat and recent email results remain in memory until cleared or the app exits. Clearing references is not a secure memory-erasure guarantee. Windows may independently retain data in its paging, crash, backup, or system facilities.

## Google services and optional cloud chat

Google controls its own account, API, and service-side data handling. Its current unpaid Gemini terms describe use of submitted content and responses for product improvement and possible human review, with regional distinctions. Review the [Gemini terms](https://ai.google.dev/gemini-api/terms) and [Google Privacy Policy](https://policies.google.com/privacy) before enabling cloud chat. Miu cannot remove provider-side records by clearing its local conversation.

Provider access, age, region, billing, and acceptable-use requirements still apply to a user-supplied key. Miu neither enables paid billing nor checks your project's billing status. The optional integration is not a promise of free or eligible access for every user.

For a community catalog you choose to load, its operator and hosting providers control any service-side request logs and retention. Read that operator's notice before connecting. The desktop app neither registers a catalog account nor uploads your chosen preset for synchronization. Built-in presets need no network connection.

The project does not sell Gmail information, use it for advertising, or send fetched Gmail content to an AI training service. Its implemented use is to display or locally speak the messages you request. Changes to that behavior require an updated notice and a review of Google's applicable requirements.

## Clear data or disconnect

1. Use **Clear conversation** to remove the app's current chat and recent email list from memory.
2. Use **Disconnect Gmail** to remove the saved token and request revocation. If Google cannot confirm revocation, remove the connection in [Google Account connections](https://myaccount.google.com/connections) when online. The imported client configuration remains for possible reconnection.
3. To stop optional cloud chat, disable it in Settings. To remove the saved Gemini key, clear its field and save. Revoke the key in Google AI Studio too if you no longer want it usable anywhere.
4. To remove all local Miu data, quit the app, preserve any notes you want, and remove the Miu data folder. Removing the portable app folder also removes its optional downloaded voice files and package-local diagnostics. It does not revoke provider access or delete files saved elsewhere by your browser.

For general privacy questions, use the repository's [issues page](https://github.com/KumarManuSaraswat/miu/issues). Issues are public: do not include private information, credentials, email contents, or recordings. For a privacy defect, follow the private reporting procedure in [SECURITY.md](SECURITY.md). A maintainer offering a hosted service must review this desktop notice against that service's actual data handling and identity before using it as a production policy.
