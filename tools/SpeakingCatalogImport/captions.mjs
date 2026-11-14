import fs from 'node:fs';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';

function walk(o,key,out=[]){if(!o||typeof o!=='object')return out;if(o[key])out.push(o[key]);for(const v of Object.values(o))if(v&&typeof v==='object')walk(v,key,out);return out;}
export async function readJsonResponse(response){
 if(!response.ok)throw Error(`http_${response.status}`);
 const text=await response.text();if(!text.trim())throw Error('empty_200');
 try{return JSON.parse(text);}catch{throw Error('malformed_json');}
}
export function parsePanel(data){
 const content=data.content?.engagementPanelSectionListRenderer?.content?.sectionListRenderer?.contents;
 const entities=walk(data.frameworkUpdates,'timedMarkersListSyncEntity');
 if(!content||!entities.length)throw Error('missing_transcript');
 const timed=new Map(entities.flatMap(x=>x.timedListData?.sections||[]).map(x=>[x.sectionId,x]));
 const cues=[];
 for(const block of content){
  const section=block.itemSectionRenderer;if(!section)continue;
  const items=(section.contents||[]).map(x=>x.macroMarkersPanelItemViewModel).filter(x=>x?.item?.timelineItemViewModel?.contentItems?.some(y=>y.transcriptSegmentViewModel));
  if(!items.length)continue;
  const stamps=(timed.get(section.sectionIdentifier)?.timedSyncDataList||[]).filter(x=>x.endTimeMs!==undefined);
  if(stamps.length!==items.length)throw Error('missing_or_mismatched_timing');
  for(let i=0;i<items.length;i++){
   const row=items[i],stamp=stamps[i],start=Number(stamp.videoTimeMs)/1000,end=Number(stamp.endTimeMs)/1000;
   const seek=row.onTap?.innertubeCommand?.watchEndpoint?.startTimeSeconds;
   const text=row.item.timelineItemViewModel.contentItems.map(x=>x.transcriptSegmentViewModel?.simpleText||'').join(' ').trim();
   if(!Number.isFinite(start)||!Number.isFinite(end)||!Number.isFinite(seek)||start<0||end<=start||Math.floor(start)!==seek||!text)throw Error('invalid_source_timing');
   cues.push({startTime:start,endTime:end,text});
  }
 }
 if(!cues.length)throw Error('missing_transcript');
 const words=cues.map(x=>x.text).join(' ').toLowerCase().match(/[a-z]+/g)||[];
 if(words.filter(x=>['the','and','to','of','is','you','for','are','a','in','it','we'].includes(x)).length<3)throw Error('non_English_transcript');
 for(let i=1;i<cues.length;i++)if(cues[i].startTime<cues[i-1].startTime)throw Error('reversed_source_timing');
 return cues;
}
export async function acquirePublicPanel(youtubeId,signal){
 if(!/^[a-zA-Z0-9_-]{11}$/.test(youtubeId))throw Error('invalid_id');
 const watchUrl=`https://www.youtube.com/watch?v=${youtubeId}`;
 const response=await fetch(watchUrl,{signal,headers:{'Accept-Language':'en-US,en;q=0.9'}});
 if(!response.ok)throw Error(`watch_http_${response.status}`);
 const html=await response.text();
 const configs=[...html.matchAll(/ytcfg\.set\((\{.*?\})\);/gs)].map(m=>{try{return JSON.parse(m[1]);}catch{return{}}});
 const config=Object.assign({},...configs);
 if(!config.INNERTUBE_CONTEXT)throw Error('missing_public_client_context');
 // The Show transcript UI's public request params contain video identity and
 // a transcript flag. No credentials, cookies, signed media URL or PO token.
 const template=Buffer.from('qgkPCgtGNVF5SDM2TW5WbxgB','base64');
 Buffer.from(youtubeId).copy(template,template.indexOf(Buffer.from('F5QyH36MnVo')));
 const r=await fetch('https://www.youtube.com/youtubei/v1/get_panel?prettyPrint=false',{method:'POST',signal,
  headers:{'Content-Type':'application/json'},body:JSON.stringify({context:config.INNERTUBE_CONTEXT,panelId:'PAmodern_transcript_view',params:template.toString('base64')})});
 const json=await readJsonResponse(r);
 return {cues:parsePanel(json),responseStatus:r.status,responseBytes:Buffer.byteLength(JSON.stringify(json))};
}

async function main(){
 const path='data/speaking/reach-a1-c2-catalog.json',manifest=JSON.parse(fs.readFileSync(path));
 const state='.appdata/local/speaking-catalog-transfer';fs.mkdirSync(`${state}/captions`,{recursive:true});
 const checkpoint=()=>{fs.writeFileSync(path+'.tmp',JSON.stringify(manifest,null,2)+'\n');fs.renameSync(path+'.tmp',path);};
 const pilot=process.argv.includes('--pilot');
 const items=pilot?['A1','A2','B1','B2','C1','C2'].map(l=>manifest.items.find(x=>x.level===l)):manifest.items;
 for(const item of items){
  const file=`${state}/captions/${item.youtubeId}.json`;
  if(fs.existsSync(file)){
   const cached=JSON.parse(fs.readFileSync(file));
   if(cached.youtubeId!==item.youtubeId||cached.cueSha256!==crypto.createHash('sha256').update(JSON.stringify(cached.cues)).digest('hex'))throw Error('caption_cache_identity_or_hash_conflict_'+item.youtubeId);
   item.transfer.captionReady=true;item.transfer.asrRequired=false;item.transfer.cueCount=cached.cues.length;item.transfer.captionSource=cached.source;
   checkpoint();console.log('CACHED',item.level,item.youtubeId);continue;
  }
  try{
   const result=await acquirePublicPanel(item.youtubeId,AbortSignal.timeout(30000));
   const source={youtubeId:item.youtubeId,source:'youtube-public-transcript-panel',observedAtUtc:new Date().toISOString(),...result};
   source.cueSha256=crypto.createHash('sha256').update(JSON.stringify(result.cues)).digest('hex');
   // Network response context/tracking/cookies are not retained.
   fs.writeFileSync(file+'.tmp',JSON.stringify(source,null,2)+'\n');fs.renameSync(file+'.tmp',file);
   item.transfer.captionReady=true;item.transfer.asrRequired=false;item.transfer.status='timed-captions-acquired-quality-pending';
   item.transfer.cueCount=result.cues.length;item.transfer.captionSource=source.source;item.transfer.blockedReason=null;
   item.transfer.timingReviewRequired=true;
   console.log(item.level,item.youtubeId,'cues='+result.cues.length,'tail='+result.cues.at(-1).endTime);
  }catch(error){
   item.transfer.status='acquisition-blocked';item.transfer.blockedReason=error.message;
   fs.appendFileSync(`${state}/acquisition-errors.jsonl`,JSON.stringify({youtubeId:item.youtubeId,url:item.watchUrl,error:error.message,observedAtUtc:new Date().toISOString()})+'\n');
   console.log('BLOCKED',item.watchUrl,error.message);
   if(/429|403/.test(error.message)){checkpoint();break;}
  }
  checkpoint();
  await new Promise(resolve=>setTimeout(resolve,1200));
 }
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)await main();
