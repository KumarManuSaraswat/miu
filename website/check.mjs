import {readFile, access} from 'node:fs/promises';
import {resolve} from 'node:path';
const html = await readFile('dist/index.html', 'utf8');
const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
if (new Set(ids).size !== ids.length) throw new Error('Duplicate element IDs');
for (const [, target] of html.matchAll(/href="#([^"]+)"/g)) {
  if (!ids.includes(target)) throw new Error('Missing anchor: ' + target);
}
for (const [, file] of html.matchAll(/(?:src|href)="((?:assets\/|app\.|styles\.)[^"]+)"/g)) await access(resolve('dist', file));
for (const file of ['assets/social.png', 'robots.txt', 'sitemap.xml']) await access(resolve('dist', file));
for (const text of ['miu-1.0.1-android.apk', 'miu-1.0.1-windows-x64.zip', 'Ancient Dragon', 'prefers-reduced-motion']) {
  const content = html + await readFile('dist/styles.css', 'utf8');
  if (!content.includes(text)) throw new Error('Missing required content: ' + text);
}
if (/https?:\/\/(?:fonts\.googleapis|fonts\.gstatic|www\.googletagmanager)/.test(html)) throw new Error('Unexpected external font or tracking request');
const canonical = html.match(/<link rel="canonical" href="([^"]+)"/)[1];
const openGraph = html.match(/<meta property="og:url" content="([^"]+)"/)[1];
const app = JSON.parse(html.match(/<script type="application\/ld\+json">(.*?)<\/script>/s)[1]);
if (canonical !== openGraph || canonical !== app.url) throw new Error('Website metadata URLs disagree');
for (const file of ['robots.txt', 'sitemap.xml']) {
  if (!(await readFile('dist/' + file, 'utf8')).includes(canonical)) throw new Error('Outdated website URL in ' + file);
}
console.log('PASS: anchors, local assets, download targets, attribution, and reduced-motion support.');
