# Neon sponsorship preparation

Status: **draft only; not submitted; no sponsorship awarded or claimed.** Reviewed against the [official Neon Open Source Program](https://neon.com/programs/open-source) on **12 November 2026**. That page was updated on 23 October 2026; check it again before applying.

## Program fit

Neon currently asks for a public repository under an OSI-approved open-source license, active development, and an active GitHub Sponsors profile. Using Neon or Postgres is not required. Eligibility does not guarantee selection or a particular amount. Its current cash sponsorship terms run for six months, with renewal reviewed separately. An accepted project agrees to an identified sponsor-logo placement for the term. These details come from the [program's eligibility and terms](https://neon.com/programs/open-source), not a commitment made to Miu.

Miu's core desktop tasks work locally. Its optional community catalog stores curated face, motion, and voice-speed presets in Neon and exposes them through a read-only Cloudflare Worker API. Users explicitly load a catalog and choose a preset; no chat, email, or audio is uploaded to it. Three built-in presets remain available offline. This integration is separate from sponsorship eligibility, and no shared hosted endpoint is bundled yet.

## Readiness checklist

| Item | Current preparation | Remaining maintainer action |
| --- | --- | --- |
| OSI-approved project license | Original Miu source is publicly released under [MIT](https://github.com/KumarManuSaraswat/miu/blob/v0.1.0/LICENSE), with third-party notices | Maintain attribution and review provenance/licenses when adding contributions or dependencies. |
| Public GitHub repository | Reviewed source and [v0.1.0 release](https://github.com/KumarManuSaraswat/miu/releases/tag/v0.1.0) are public in [KumarManuSaraswat/miu](https://github.com/KumarManuSaraswat/miu) | Keep source, release notes, checksums, and support/privacy information current. |
| Active development | Public commit history, the initial release, and passing [build/validation CI](https://github.com/KumarManuSaraswat/miu/actions/workflows/windows.yml) are available | Continue publishing real maintenance activity; an initial release does not establish long-term activity or adoption. |
| GitHub Sponsors | Not established by this work | Enable an eligible profile and complete GitHub's required payout/tax setup. |
| Website/project page | The public [repository project page](https://github.com/KumarManuSaraswat/miu#readme) includes setup, privacy, and security links | Use the actual project-page URL in an application; no separate website is claimed. |
| Optional Neon integration | The [public catalog](https://miu-preset-catalog.kumar-saraswat-s-125.workers.dev/v1/presets) is live on Neon Free and Workers Free; all six profiles were verified with the desktop client | Monitor quotas and maintain read-only access. Users enter the address manually; v0.1.0 still bundles no endpoint and its release assets remain unchanged. |
| Contact email | Choose a monitored project email when applying | Review the contact address in the final application before sharing it. |
| Community evidence | No public adoption statistics verified | Supply only measured, public usage or contributor evidence when it exists. |
| Sponsor branding | None added | Add a clearly labeled placement only after an actual agreement. |

## Draft project description

Miu is an early open-source Windows desktop companion designed for people who want a small on-screen helper without a browser runtime or a resident large language model. It provides explicit local commands, an expressive code-drawn mascot, and optional on-demand offline speech. Gmail reading requires the user's own read-only authorization. Optional cloud conversation is separate from fetched mailbox data and is disabled by default.

The first release prioritizes clear permission boundaries, bounded resource use, accessible setup instructions, and reproducible checks. An optional Neon-backed community catalog supplies curated appearance and voice-speed presets through a read-only Cloudflare API; local profiles need no service. Downloaded voice engines and models are excluded from the lean release package and retain their upstream licenses when installed separately. The project is at an early stage; we are not claiming established adoption or a mature support operation.

## Proposed use of support

If selected, sponsorship could support maintenance time, microphone and Windows compatibility testing, clearer onboarding, dependency/license upkeep, accessibility improvements, and release security work such as evaluating signing options. The maintainer should replace this proposed allocation with an honest plan they can deliver. No expenditure, staffing level, or funding amount is committed here.

## Application worksheet

The current form requests a website URL, GitHub project link, and contact email. Enter the actual published values; do not submit placeholders or this local folder path. Recheck the eligibility items, confirm the maintainer's consent to share the contact address, and review the complete application before submitting it manually or explicitly authorizing submission.

Do not use a “Sponsored by Neon” badge, imply acceptance, invent a maintainer identity, or manufacture stars, users, downloads, contributors, or revenue. This file prepares a possible application; it does not send one.
