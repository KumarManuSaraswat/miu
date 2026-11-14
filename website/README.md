# Miu website

A product landing page for Miu, the offline companion for Windows and Android.
The site is static HTML, CSS and JavaScript. No framework, build step, account,
tracking, or paid API is required. Download links point to the official signed
Android APK and portable Windows ZIP in the Miu GitHub releases.

## Work locally

Run `npm run dev` with Node.js 18 or newer, then open
`http://127.0.0.1:4173`. Run `npm run check` before publishing. Deploy the contents
of `dist/` on a static web host. Keep the canonical URL, Open Graph URL,
`robots.txt` and `sitemap.xml` consistent if moving the site.

For a new app release, update the version, download filenames, sizes, requirements,
and structured data in `dist/index.html`, then check the actual release assets.
Do not assume all app features work offline: the FAQ explains the distinctions.

## Mascot

The vector mascot and six preview expressions live in the SVG symbols in
`dist/index.html`. Pointer tracking moves only the eyes, independently of blinking.
It uses one scheduled animation frame per pointer update, skips offscreen mascots,
resets when the page loses focus, and respects reduced-motion preferences.
Touching the mascot gives a brief gaze response without blocking page scrolling.
The preview changes expressions on click and with keyboard-accessible buttons;
it is not a browser chatbot and does not request microphone access.

## Assets and licenses

- Original website and mascot code: MIT; see `LICENSE`.
- Manrope: self-hosted variable font from Google Fonts, SIL Open Font License;
  see `dist/assets/manrope-OFL.txt`.
- Android panel screenshot: Miu project, from `assets/miu-android-assistant.png`.
  It contains no email or chat content.
- `tools/render-social.ps1` renders the original social card using Windows
  System.Drawing. Run it from this directory to regenerate `dist/assets/social.png`.
- Gemma and the app's other bundled dependencies keep their own terms, linked
  from the download page. This website does not bundle or run a language model.

The site makes no analytics calls or third-party font requests. Hosting providers
may retain ordinary connection logs. Following a GitHub link leaves the site.
