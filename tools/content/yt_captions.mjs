// Caption smoke test / fetch for one video: lists caption tracks and (with --save dir) stores the English one as json.
// Usage: node tools/content/yt_captions.mjs <videoId> [--save <dir>]
import fs from 'node:fs';
const [id, flag, dir] = process.argv.slice(2);
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36';
const r = await fetch('https://www.youtube.com/watch?v=' + id, { headers: { 'User-Agent': UA, 'Accept-Language': 'en-US,en;q=0.9' }, signal: AbortSignal.timeout(30000) });
const html = await r.text();
const m = html.match(/ytInitialPlayerResponse\s*=\s*(\{.*?\});(?:var|<\/script>)/s);
if (!m) { console.log('no player response', r.status); process.exit(1); }
const pr = JSON.parse(m[1]);
const vd = pr.videoDetails ?? {};
console.log('title:', vd.title, '| seconds:', vd.lengthSeconds, '| author:', vd.author, '| live:', vd.isLiveContent, '| playability:', pr.playabilityStatus?.status, '| embeddable:', pr.playabilityStatus?.playableInEmbed);
const tracks = pr.captions?.playerCaptionsTracklistRenderer?.captionTracks ?? [];
for (const t of tracks) console.log('track', t.languageCode, t.kind ?? 'manual', t.name?.simpleText);
const en = tracks.find(t => t.languageCode?.startsWith('en') && t.kind !== 'asr') ?? tracks.find(t => t.languageCode?.startsWith('en'));
if (!en) { console.log('NO ENGLISH TRACK'); process.exit(2); }
const cr = await fetch(en.baseUrl + '&fmt=json3', { headers: { 'User-Agent': UA }, signal: AbortSignal.timeout(30000) });
const body = await cr.text();
console.log('caption http', cr.status, 'bytes', body.length);
if (!body.length) process.exit(3);
const j = JSON.parse(body);
const events = (j.events ?? []).filter(e => e.segs);
console.log('events', events.length, 'first:', events[0]?.segs?.map(s => s.utf8).join('').slice(0, 80));
if (flag === '--save') {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(`${dir}/${id}.json`, JSON.stringify({ id, track: { lang: en.languageCode, kind: en.kind ?? 'manual' }, title: vd.title, seconds: Number(vd.lengthSeconds), author: vd.author, events: events.map(e => ({ s: e.tStartMs / 1000, d: (e.dDurationMs ?? 0) / 1000, t: e.segs.map(s => s.utf8).join('') })) }, null, 1));
}
