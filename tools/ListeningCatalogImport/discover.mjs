import fs from 'node:fs';
const root = '.appdata/local/reach-listening-import';
export function walk(o, key, out = []) { if (!o || typeof o !== 'object') return out; if (o[key]) out.push(o[key]); for (const v of Object.values(o)) walk(v,key,out); return out; }
export async function page(url) { const r = await fetch(url,{signal:AbortSignal.timeout(30000),headers:{'Accept-Language':'en-US,en;q=0.9'}}); if(!r.ok)throw Error(`${r.status} ${url}`); return r.text(); }
export function initial(html, name='ytInitialData') {const m=html.match(new RegExp(`var ${name} = (.*?);<\\/script>`,'s'));if(!m)throw Error(`Missing ${name}`);return JSON.parse(m[1]);}
export function extract(d) {
 const modern=walk(d,'lockupViewModel').filter(x=>x.contentType==='LOCKUP_CONTENT_TYPE_VIDEO').map(x=>({youtubeId:x.contentId,sourceTitle:x.metadata?.lockupMetadataViewModel?.title?.content,duration:walk(x.contentImage,'thumbnailBadgeViewModel').map(b=>b.text).find(t=>/^\d+(:\d+)+$/.test(t||'')),channel:walk(x.metadata,'browseEndpoint')[0]}));
 const old=walk(d,'playlistVideoRenderer').concat(walk(d,'videoRenderer')).map(x=>({youtubeId:x.videoId,sourceTitle:x.title?.runs?.map(r=>r.text).join('')||x.title?.simpleText,duration:x.lengthText?.simpleText,channel:walk(x.shortBylineText||x.ownerText,'browseEndpoint')[0]}));
 return [...new Map([...modern,...old].filter(x=>x.youtubeId&&x.sourceTitle).map(x=>[x.youtubeId,x])).values()];
}
const lists={A1:'PLqYGyORvIQiz2lBQGAGxKcNoW6UAmmD7S',A2:'PLqYGyORvIQizDwGeH6ImlP_I5PIgC6Gr8',B1:'PLqYGyORvIQiznS2WzCWZVlXW6Bb75hUfb',B2:'PLqYGyORvIQiz0717speyC2jyKn_lprEPT',C1:'PLqYGyORvIQiwucjWd4dYCTAiozl8qqSg0'};
if(process.argv[2]==='reach') for(const [level,id] of Object.entries(lists)) {
 const path=`${root}/playlist-${level}.json`;if(fs.existsSync(path)){console.log(level,'cached');continue;}
 const url=`https://www.youtube.com/playlist?list=${id}`;const html=await page(url);const d=initial(html);const items=extract(d);
 const result={observedAtUtc:new Date().toISOString(),url,title:html.match(/<title>(.*?)<\/title>/s)?.[1],items};
 fs.writeFileSync(path,JSON.stringify(result,null,2)+'\n');console.log(level,result.title,items.length);
}
if(process.argv[2]==='search') {const q=process.argv.slice(3).join(' ');const url='https://www.youtube.com/results?search_query='+encodeURIComponent(q);const d=initial(await page(url));const items=extract(d);fs.writeFileSync(`${root}/search-${q.replace(/[^a-zA-Z0-9]/g,'-')}.json`,JSON.stringify({url,observedAtUtc:new Date().toISOString(),items},null,2));console.log(JSON.stringify(items,null,2));}
if(process.argv[2]==='channel') {const channel=process.argv[3],q=process.argv[4];const url=`https://www.youtube.com/@${channel}/search?query=${q}`;const items=extract(initial(await page(url)));fs.writeFileSync(`${root}/channel-${channel}-${q}.json`,JSON.stringify({url,observedAtUtc:new Date().toISOString(),items},null,2));console.log(channel,q,items.length,items.map(x=>x.sourceTitle));}
