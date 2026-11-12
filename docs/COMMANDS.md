# Talking to Miu

Click the microphone or press **Ctrl+Alt+Space**, wait for the listening status, say one request, and pause. The input meter shows microphone activity, and a live caption shows recognized words when using Vosk. You can always type instead. The microphone only runs when you activate it. English recognition uses the optional Vosk download or an installed Windows recognizer; the lean release includes neither a Vosk engine nor its model. Spoken replies use the optional natural offline Piper voice, with Windows speech as a fallback; choose **Settings → Save & try voice** to hear it. See the README for voice installation.

Press the microphone during a reply to interrupt and begin listening. If recognition is uncertain, Miu asks you to review the words and press Send before doing anything. Use **Settings → Choose or test my microphone** if the input meter stays flat while you speak.

These commands work without an AI key. Gmail and web searches still need internet access.

| What you want | Try saying |
| --- | --- |
| See the commands | “Help” or “What can you do?” |
| Explore all 24 faces | “Show reactions” or “Show all faces” |
| Choose an expression | “Make a playful face”, “Look amazed”, “Show a wide-eyed face” |
| Check your inbox | “Check my email”, “Latest email”, “Unread emails” |
| Find a sender | “Emails from Alice” |
| Find a topic | “Emails about my order” |
| Use Gmail search operators | “Search Gmail subject:invoice newer_than:7d” |
| Read a result | “Read email 1” or “Read email number 2” |
| Read the newest message | “Read my latest email” |
| Set up Gmail | “Connect Gmail” |
| Open an app | “Open Calculator”, “Open Notepad”, “Open File Explorer” |
| Open a folder | “Open Downloads”, “Open Documents”, “Open notes” |
| Open a website | “Open Gmail”, “Open browser” |
| Open settings | “Open Windows settings” or “Open Miu settings” |
| Search in your browser | “Search the web for easy pasta recipes” |
| Save a text note | “Take a note buy milk tomorrow” |
| Check the time | “What time is it?” |
| Check the date | “What's today's date?” |
| Check the battery | “Check my battery” |
| Change volume | “Volume up”, “Volume down”, “Toggle system mute” |
| Turn voice replies off/on | “Mute yourself”, “Unmute yourself” |
| Hide the panel | “Go to sleep” |
| Return | Click Miu or press the hotkey |
| Clear temporary context | “Clear chat” |

Notes are plain text files in `%LOCALAPPDATA%\Miu\Notes` (or the app's configured data directory). Their filenames are generated automatically. Saying “open notes” opens that folder.

Miu can greet you, respond to thanks, react to a few feelings, and tell a little joke locally. Thanks make it cheerful, jokes make it playful, and affection makes it blush. When you say you feel sad or frustrated, it uses a sympathetic sad expression. The local command parser is deliberately limited; it is not a local language model.

## All 24 face reactions

Open the **Reactions** button in the chat header, choose **Reactions** from the mascot's right-click menu or tray menu, or use **Settings → Explore all 24 reactions**. Saying “show reactions” opens the same gallery. Click a tile to try its face on the floating mascot.

The gallery is arranged from left to right, top to bottom:

| Left | Middle | Right |
| --- | --- | --- |
| Content | Kiss | Crying |
| Neutral | Playful | Wailing |
| Mischievous | Smug kiss | Sassy |
| Wide-eyed | Surprised kiss | Shocked |
| Joyful | Sad | Annoyed |
| Scrunched | Dizzy | Blushing |
| Cheerful | Drooling | Delighted |
| Skeptical | Furious | Amazed |

Every name works with **“show [name] face”**, **“look [name]”**, or **“make a [name] face”**. For example, say “show a surprised kiss face”, “look amazed”, or “make a playful face”. Older names such as “happy”, “curious”, “sleepy”, and “love” still work.

Miu retains its emotion while speaking, with mouth movement layered onto that expression. When speaking ends, the face remains until another response or a selected reaction changes it. Reduced animation in Settings stops the talking motion and periodic blinking.

## Optional AI conversation

Enable cloud chat in Settings and add a Gemini API key from your own eligible Google AI Studio project. The default model is `gemini-3.5-flash-lite`. Saved settings that still use the former `gemini-2.5-flash-lite` default are upgraded on load, because Google restricts that older model to prior active users. Other model choices are preserved. Do not enable billing if you need to remain entirely on the free tier. The app cannot determine whether a supplied key belongs to a paid project, so billing status must be checked in Google AI Studio. Free availability is not universal: review Google's age, region, intended-use, and billing conditions in the [current API terms](https://ai.google.dev/gemini-api/terms) before enabling this optional feature.

Google lists free input and output for that model, subject to eligibility and usage limits. Availability and quotas can change. When Google refuses a request or the free quota is exhausted, Miu shows an explanation; local commands continue working. See [Google's pricing page](https://ai.google.dev/gemini-api/docs/pricing) for current terms and [the content generation API](https://ai.google.dev/api/generate-content) for the implementation contract.

Some imported free-tier projects require phone verification. If Miu says **Verify project**, open **Google AI Studio → Projects**, choose your project, and follow **Verify project** to scan the QR code with your phone. This does not require paid billing. Miu shows fixed guidance and does not display raw Google error payloads or account identifiers. See [Google's verification guide](https://ai.google.dev/gemini-api/docs/verification).

AI conversation sends your chat text and up to six preceding general-chat exchanges to Google. Conversation context is held in memory and cleared by “clear chat” or quitting the app. Google's unpaid-service terms describe use of submitted content and responses for product improvement, with regional distinctions. Read [PRIVACY.md](../PRIVACY.md) before enabling cloud chat. Do not enter secrets or sensitive personal information into optional cloud chat. Gmail results and note contents are handled locally and are not added to AI conversation history. If you manually paste private material into general chat, that text is sent as part of your request.

The AI has no device tools and cannot run commands. Miu supports only the listed device actions and reads Gmail with read-only permission. It cannot send or delete emails, delete files, install software, shut down the PC, make purchases, or run arbitrary command lines. Text inside an email is only displayed and spoken; it is never treated as instructions.

## Small details

- “Latest email” means the newest message in your **inbox**, excluding spam and trash. Gmail searches use Gmail's search syntax and return up to five recent matches.
- “Read email 2” refers to the second message in your last results. A long email is clipped for speech; open Gmail for the full message.
- “Mute” disables Miu's voice replies. “Toggle system mute” sends Windows' mute-toggle key; use the Windows sound indicator to see the resulting state.
- Commands are single actions. Say “open calculator” and then your next request. Chained shell commands and arbitrary executable paths are not supported.
- Follow-up questions about email stay out of cloud chat. Say “clear chat” to begin a separate topic.
