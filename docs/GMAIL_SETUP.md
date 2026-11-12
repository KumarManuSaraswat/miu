# Connect your Gmail to Miu

Miu reads Gmail only when you ask. It can find the latest inbox message, search for a sender or subject, and read a selected message aloud. Google requires a one-time OAuth setup before a personal desktop app can read your mailbox. Your Google password is entered only on Google's sign-in page, never into Miu.

Standard Gmail API usage has no additional charge within Google's quotas. Use a project without paid billing or quota increases to keep this setup free. Google's current quota page also describes planned charges above its daily threshold; Miu does not enable billing or request increased quotas. [Google's Gmail usage limits](https://developers.google.com/workspace/gmail/api/reference/quota).

## 1. Create your personal Google project

1. Open [Google Cloud Console](https://console.cloud.google.com/) in your browser and sign in.
2. Use the project selector to create a project named **Miu Personal**.
3. Open **APIs & Services > Library**, find **Gmail API**, and click **Enable**. This app needs only Gmail API; you do not need to buy Google Workspace or create a service account. [Google's Gmail quickstart](https://developers.google.com/workspace/gmail/api/quickstart/nodejs).

## 2. Configure Google sign-in

1. Open **Google Auth platform > Branding** (sometimes labelled **OAuth consent screen**). Click **Get started** if prompted.
2. Enter **Miu Personal** as the app name and your own email for support/contact.
3. For a normal personal Gmail account, choose the **External** audience. Keep the app in **Testing**.
4. In **Audience > Test users**, add the exact Gmail address you want Miu to read.
5. In **Data Access > Add or remove scopes**, add this single scope and save:

   ```text
   https://www.googleapis.com/auth/gmail.readonly
   ```

The Google Auth platform labels can vary by account. The [official consent-screen guide](https://developers.google.com/workspace/guides/configure-oauth-consent) covers Branding, Audience, Data Access, and test users. The [Gmail scope reference](https://developers.google.com/workspace/gmail/api/auth/scopes) confirms that this scope grants read access. It does not let Miu send, delete, or mark emails as read.

## 3. Download the Desktop app credentials

1. Open **Google Auth platform > Clients > Create client**.
2. Set **Application type** to **Desktop app**, give it a name, and create it.
3. Download its JSON file. This is an OAuth client file, not an API key or a service-account key.
4. In Miu's **Settings**, use **Import Google credentials** and select that JSON file.
5. Click **Connect Gmail**. Your usual browser opens Google's sign-in screen.
6. Select the test-user account and grant Gmail read access. If Google displays an unverified-app warning, continue only if the project and app are the ones you just created for yourself.
7. Finish within five minutes, then return to Miu. A successful connection reports the account's email address.

Keep Miu Settings open while signing in. If you close the browser accidentally, use **Open sign-in again**. To use another browser, expand **Google sign-in link** and copy the complete link into that browser. Use the original link from Miu, not a later Google consent-page address. **Cancel sign-in** ends the current attempt; **Connect Gmail** starts a fresh one.

Miu uses Google's [Desktop app authorization flow](https://developers.google.com/identity/protocols/oauth2/native-app), with PKCE and a temporary local callback. No redirect URL needs to be manually added for a Desktop app client. The callback listens only on `127.0.0.1` and closes after sign-in.

## Try it

Ask Miu for your latest email, then ask it to read the first result. For precise searches, use Gmail's own search expressions, for example:

```text
from:amazon
subject:invoice newer_than:30d
in:inbox is:unread
from:person@example.com
```

Search returns at most ten newest matching messages. Empty searches use the inbox. Ordinary file attachments are not downloaded. Large messages are shortened to keep memory use low. Gmail supports its normal search syntax through the API. [Google's message-list guide](https://developers.google.com/workspace/gmail/api/guides/list-messages).

## Common fixes

| What happened | What to do |
| --- | --- |
| Access blocked or user not allowed | Add the same Gmail address to **Audience > Test users**. A work/school administrator may also restrict access. |
| Wrong credentials file | Create a **Desktop app** OAuth client and import its JSON. Web-client and service-account JSON files do not work. |
| Gmail API not enabled | Enable it in the project that owns the imported client; wait a few minutes, then retry. |
| Sign-in expired after about a week | Reconnect. Google's external apps in Testing normally receive refresh tokens that expire after seven days. [Google's token-expiration documentation](https://developers.google.com/identity/protocols/oauth2). |
| Browser says the local page cannot be reached | Keep Miu open during sign-in. Restart the connection after the five-minute timeout. |
| Closed the browser, or Google reports a malformed request | In Settings, cancel sign-in and click **Connect Gmail** for a fresh attempt. If using a different browser, open the complete **Google sign-in link** from Miu rather than transferring an intermediate consent-page URL. |
| No matches | Try a broader search or check Gmail in your browser. Miu does not invent results. |

## Your data and disconnecting

OAuth client credentials and tokens are encrypted for your Windows user with Windows DPAPI under `%LOCALAPPDATA%\Miu`. Miu does not save email bodies to disk or send them to an AI provider. Search responses are treated as message data, never as device instructions. The downloaded client JSON remains wherever your browser saved it; Miu imports an encrypted copy.

Use **Disconnect Gmail** in Settings to remove the saved token and request revocation at Google. If offline, the local token is still removed and Miu tells you to revoke access through [Google Account connections](https://myaccount.google.com/connections). The imported client configuration stays available for a later connection. This is a personal test app; publishing it for other users involves Google's additional OAuth verification requirements.
