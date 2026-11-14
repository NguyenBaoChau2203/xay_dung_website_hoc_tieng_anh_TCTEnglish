import fs from 'node:fs';
import {page,initial} from './discover.mjs';
const root='.appdata/local/reach-listening-import';
const read=n=>JSON.parse(fs.readFileSync(`${root}/${n}.json`));
const baseline=read('baseline-rechecked');const seen=new Set(baseline.PublicPublishedLessons.map(x=>x.YoutubeId));
const reach='UCkGHdvmCAvaJ033DpfXtCog';
const selected=[];
fs.mkdirSync(`${root}/metadata`,{recursive:true});
fs.mkdirSync('data/listening',{recursive:true});
for(const level of ['A1','A2','B1','B2','C1','C2']) {
 const list=level==='C2'?null:read('playlist-'+level);
 let pool=(list?.items||[]).map(x=>({...x,playlistUrl:list.url}));
 if(level==='C1')pool.push(...read('channel-REACH-l5p-C1').items);
 if(level==='C1'||level==='C2')pool.push(...read(`channel-VietFCE-${level}`).items);
 pool=[...new Map(pool.map(x=>[x.youtubeId,x])).values()];
 for(const candidate of pool) {
  if(selected.filter(x=>x.level===level).length===15)break;
  if(seen.has(candidate.youtubeId)||!new RegExp(`\\b${level}\\b`).test(candidate.sourceTitle)||/A1-A2|C1.C2|MÀY GAY/i.test(candidate.sourceTitle))continue;
  if(level==='C2'&&!/^C2 Proficiency \(CPE\) Listening Test \d+ with answers$/.test(candidate.sourceTitle))continue;
  const watchUrl=`https://www.youtube.com/watch?v=${candidate.youtubeId}`;
  try {
   const cache=`${root}/metadata/${candidate.youtubeId}.json`;
   let meta;
   if(fs.existsSync(cache))meta=JSON.parse(fs.readFileSync(cache));else {
    const h=await page(watchUrl);const p=initial(h,'ytInitialPlayerResponse');
    const v=p.videoDetails,m=p.microformat?.playerMicroformatRenderer;
    meta={observedAtUtc:new Date().toISOString(),status:p.playabilityStatus?.status,playableInEmbed:p.playabilityStatus?.playableInEmbed,
     youtubeId:v?.videoId,title:v?.title,durationSeconds:Number(v?.lengthSeconds),channelId:v?.channelId,channelName:v?.author,description:v?.shortDescription,
     isPrivate:v?.isPrivate,isUnlisted:m?.isUnlisted,isLive:v?.isLiveContent,channelUrl:m?.ownerProfileUrl?.replace('http:','https:'),
     captionTracks:p.captions?.playerCaptionsTracklistRenderer?.captionTracks?.map(t=>({languageCode:t.languageCode,kind:t.kind||'manual',name:t.name?.simpleText||t.name?.runs?.map(r=>r.text).join('')}))||[]};
    fs.writeFileSync(cache,JSON.stringify(meta,null,2)+'\n');
   }
   if(meta.status!=='OK'||meta.playableInEmbed!==true||meta.isPrivate||meta.isUnlisted||meta.isLive||meta.durationSeconds<60||meta.youtubeId!==candidate.youtubeId||meta.channelId!==candidate.channel?.browseId)throw Error('Availability/channel/duration check failed');
   if(!new RegExp(`\\b${level}\\b`).test(meta.title))throw Error('Watch title disagrees with selected level');
   const isReach=meta.channelId===reach;
   if(!isReach&&!new RegExp(`${level} (Advanced|Proficiency).*Listening`,'i').test(meta.description||''))throw Error('Missing substantive creator exam-level description');
   const duration=meta.durationSeconds>=3600?`${Math.floor(meta.durationSeconds/3600)}:${String(Math.floor(meta.durationSeconds/60)%60).padStart(2,'0')}:${String(meta.durationSeconds%60).padStart(2,'0')}`:`${Math.floor(meta.durationSeconds/60)}:${String(meta.durationSeconds%60).padStart(2,'0')}`;
   const title=meta.title.replace(/^🎧\s*/u,'').replace(/\s*\| Listening English Podcast \|🎯?\s*$/u,'').trim();
   const topic=isReach?(title.split(/(?:A1|A2|B1|B2|C1)\s*-\s*/)[1]?.replace(/\s*-\s*$/,'').trim()||'Everyday English'):`${level==='C2'?'C2 Proficiency':'C1 Advanced'} exam practice`;
   selected.push({youtubeId:meta.youtubeId,watchUrl,level,sourceTitle:meta.title,displayTitle:title.slice(0,255),topic:topic.slice(0,100),durationSeconds:meta.durationSeconds,duration,thumbnailUrl:`https://img.youtube.com/vi/${meta.youtubeId}/hqdefault.jpg`,channelId:meta.channelId,channelName:meta.channelName,channelUrl:meta.channelUrl||`https://www.youtube.com/channel/${meta.channelId}`,playlistUrl:candidate.playlistUrl||null,
    levelEvidence:{url:watchUrl,classification:'creator-labelled; not official CEFR certification',rationale:isReach?`Creator title explicitly labels ${level}${candidate.playlistUrl?'; also in the corresponding REACH level playlist':''}.`:`Creator title and description identify a ${level} ${level==='C2'?'Proficiency (CPE)':'Advanced (CAE)'} listening exam with four listening task parts; external source fills verified REACH shortage.`,descriptionExcerpt:isReach?null:meta.description.split('\n')[0]},isReach,
    availability:{status:meta.status,isPublic:true,playableInEmbed:true,evidence:'YouTube watch-page player response; playback is a separate browser check'},captionStatus:meta.captionTracks.some(t=>t.languageCode.startsWith('en'))?'english-track-listed-not-yet-retrieved':'no-english-track-listed',captionTracks:meta.captionTracks,observedAtUtc:meta.observedAtUtc,selectionState:'selected'});
   seen.add(candidate.youtubeId);
   fs.writeFileSync(`${root}/selected-progress.json`,JSON.stringify({schemaVersion:1,batch:'reach-a1-c2-2026-11',items:selected},null,2)+'\n');
   console.log(level,selected.filter(x=>x.level===level).length,candidate.youtubeId,meta.channelName);
  } catch(e) {fs.appendFileSync(`${root}/rejected.jsonl`,JSON.stringify({url:watchUrl,level,error:e.message,observedAtUtc:new Date().toISOString()})+'\n');console.error('REJECT',watchUrl,e.message);}
 }
 console.log('SAVED',level,'selected',selected.filter(x=>x.level===level).length);
}
if(selected.length!==90 || new Set(selected.map(x=>x.youtubeId)).size!==90)
 throw Error('Incomplete selection; progress saved without replacing the manifest.');
if(!fs.existsSync('data/listening/reach-a1-c2-catalog.json'))
 fs.copyFileSync(`${root}/selected-progress.json`,'data/listening/reach-a1-c2-catalog.json');
else console.log('Existing curated manifest preserved; review saved progress before replacement.');
