import fs from 'node:fs';
import crypto from 'node:crypto';
import {page,initial} from '../ListeningCatalogImport/discover.mjs';
import {acquirePublicPanel} from './captions.mjs';
import {acquireMediaItem} from './media.mjs';
const state='.appdata/local/speaking-catalog-transfer',discovery='.appdata/local/reach-listening-import';
const file='data/speaking/reach-a1-c2-catalog.json';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const atomic=(file,value)=>{fs.writeFileSync(file+'.tmp',JSON.stringify(value,null,2)+'\n');fs.renameSync(file+'.tmp',file);};
const authorization='owner-chat-2026-11-13-replace-unavailable-with-same-level-transcript';
const manifest=JSON.parse(fs.readFileSync(file)),old=JSON.parse(fs.readFileSync('data/listening/reach-a1-c2-catalog.json'));
const blocked=fs.readFileSync(state+'/media-errors.jsonl','utf8').trim().split('\n').filter(Boolean).map(x=>JSON.parse(x)).filter(x=>x.error==='http_403');
const originals=[...new Set(blocked.map(x=>x.youtubeId))].filter(id=>old.items.some(x=>x.youtubeId===id));
const excluded=new Set([...old.items.map(x=>x.youtubeId),...manifest.items.map(x=>x.youtubeId),...fs.readFileSync(state+'/existing-youtube-ids.txt','utf8').split(/\r?\n/).filter(Boolean)]);
if(!fs.existsSync(state+'/manifest-before-replacements.json'))fs.copyFileSync(file,state+'/manifest-before-replacements.json');
manifest.replacements??=[];manifest.replacementAttempts??=[];
manifest.ownerCorrection={authorization,instruction:'Owner permits replacing inaccessible videos with other same-level videos having genuine transcripts; preserve original Listening withdrawals and audit mapping.',recordedAtUtc:new Date().toISOString()};
for(const originalId of originals){
 if(manifest.replacements.some(x=>x.originalYoutubeId===originalId)){console.log('REPLACEMENT-CACHED',originalId);continue;}
 const original=old.items.find(x=>x.youtubeId===originalId),level=original.level;
 const playlist=level==='C2'?null:JSON.parse(fs.readFileSync(`${discovery}/playlist-${level}.json`));
 let pool=(playlist?.items||[]).map(x=>({...x,playlistUrl:playlist.url}));
 if(['C1','C2'].includes(level))pool.push(...JSON.parse(fs.readFileSync(`${discovery}/channel-VietFCE-${level}.json`)).items);
 pool=[...new Map(pool.map(x=>[x.youtubeId,x])).values()].filter(x=>!excluded.has(x.youtubeId)&&new RegExp(`\\b${level}\\b`).test(x.sourceTitle)&&!/A1-A2|C1.C2|MÀY GAY/i.test(x.sourceTitle));
 let selected=false;
 for(const candidate of pool.slice(0,8)){
  if(manifest.replacementAttempts.some(x=>x.candidateYoutubeId===candidate.youtubeId))continue;
  const watchUrl=`https://www.youtube.com/watch?v=${candidate.youtubeId}`;
  try{
   const p=initial(await page(watchUrl),'ytInitialPlayerResponse'),v=p.videoDetails,m=p.microformat?.playerMicroformatRenderer;
   if(p.playabilityStatus?.status!=='OK'||p.playabilityStatus?.playableInEmbed!==true||v?.isPrivate||m?.isUnlisted||v?.isLiveContent||v?.videoId!==candidate.youtubeId||v?.channelId!==candidate.channel?.browseId||!new RegExp(`\\b${level}\\b`).test(v?.title||''))throw Error('public_identity_or_level_evidence_failed');
   const seconds=Number(v.lengthSeconds),reach=v.channelId==='UCkGHdvmCAvaJ033DpfXtCog';
   if(seconds<60||seconds>7200||!Number.isFinite(seconds)||!reach&&!new RegExp(`${level} (Advanced|Proficiency).*Listening`,'i').test(v.shortDescription||''))throw Error('duration_or_substantive_creator_level_evidence_failed');
   if(!reach&&!['C1','C2'].includes(level))throw Error('outside_authorized_fallback_levels');
   const captions=await acquirePublicPanel(v.videoId,AbortSignal.timeout(30000));
   if(captions.cues.length<3||captions.cues.at(-1).endTime<seconds*.9)throw Error('obviously_partial_caption_candidate');
   const title=v.title.replace(/^🎧\s*/u,'').replace(/\s*\| Listening English Podcast \|🎯?\s*$/u,'').trim();
   const item={youtubeId:v.videoId,watchUrl,level,sourceTitle:v.title,displayTitle:title.slice(0,255),topic:(reach?(title.split(/(?:A1|A2|B1|B2|C1)\s*-\s*/)[1]?.replace(/\s*-\s*$/,'').trim()||'Everyday English'):`${level==='C2'?'C2 Proficiency':'C1 Advanced'} exam practice`).slice(0,100),durationSeconds:seconds,duration:`${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`,thumbnailUrl:`https://img.youtube.com/vi/${v.videoId}/hqdefault.jpg`,channelId:v.channelId,channelName:v.author,channelUrl:m?.ownerProfileUrl?.replace('http:','https:')||`https://www.youtube.com/channel/${v.channelId}`,playlistUrl:candidate.playlistUrl||null,levelEvidence:{url:watchUrl,classification:'creator-labelled; not official CEFR certification',rationale:reach?`Creator title explicitly labels ${level}; verified in corresponding REACH playlist.`:`Creator title and description explicitly identify ${level} ${level==='C2'?'Proficiency (CPE)':'Advanced (CAE)'} exam practice; authorized fallback for unavailable REACH media.`,descriptionExcerpt:reach?null:v.shortDescription.split('\n')[0]},isReach:reach,availability:{status:'OK',isPublic:true,playableInEmbed:true,evidence:'Fresh public watch response plus successfully acquired media; audible quality review pending'},captionStatus:'timed-english-captions-acquired-quality-pending',observedAtUtc:new Date().toISOString(),selectionState:'selected',replacementOfYoutubeId:originalId,transfer:{destination:'Speaking',status:'timed-captions-acquired-quality-pending',captionReady:true,asrRequired:false,alignmentReady:false,qualityPassed:false,speakingPublished:false,listeningRetired:true,creatorTextAvailable:false,blockedReason:null,cueCount:captions.cues.length,captionSource:'youtube-public-transcript-panel',timingReviewRequired:true}};
   const media=await acquireMediaItem(item);
   atomic(`${state}/captions/${v.videoId}.json`,{youtubeId:v.videoId,source:'youtube-public-transcript-panel',observedAtUtc:new Date().toISOString(),...captions,cueSha256:hash(JSON.stringify(captions.cues))});
   item.transfer.audioMeasured=true;
   const index=manifest.items.findIndex(x=>(x.replacementOfYoutubeId||x.youtubeId)===originalId);if(index<0)throw Error('original_source_mapping_missing');
   manifest.items[index]=item;
   manifest.replacements.push({originalYoutubeId:originalId,replacementYoutubeId:v.videoId,level,reason:'audio-http-403',ownerAuthorization:authorization,evidenceUrl:watchUrl,observedAtUtc:new Date().toISOString(),measuredDurationSeconds:media.measuredDurationSeconds});
   excluded.add(v.videoId);atomic(file,manifest);selected=true;console.log('REPLACED',level,originalId,'=>',v.videoId,'duration='+media.measuredDurationSeconds);break;
  }catch(e){
   const error=/429/.test(e.details||e.message)?'http_429':/403/.test(e.details||e.message)?'http_403':e.message;
   manifest.replacementAttempts.push({originalYoutubeId:originalId,candidateYoutubeId:candidate.youtubeId,level,url:watchUrl,error,observedAtUtc:new Date().toISOString()});atomic(file,manifest);console.log('REPLACEMENT-REJECTED',watchUrl,error);
   if(error==='http_429'){process.exitCode=2;throw Error('Rate limited; preserve selection and stop discovery.');}
  }
 }
 if(!selected){process.exitCode=2;console.log('REPLACEMENT-BLOCKED',originalId,level);}
}
