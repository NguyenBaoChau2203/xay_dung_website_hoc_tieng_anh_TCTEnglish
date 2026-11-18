import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {pathToFileURL} from 'node:url';

const state='.appdata/local/speaking-catalog-transfer';
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
export function planChunks(durationSeconds,maxSeconds=300,overlapSeconds=1.5){
 if(!Number.isFinite(durationSeconds)||durationSeconds<=0||durationSeconds>7200||!Number.isFinite(maxSeconds)||!Number.isFinite(overlapSeconds)||maxSeconds<=overlapSeconds||overlapSeconds<0)throw Error('invalid_chunk_bounds');
 const chunks=[];let ownedStart=0;
 while(ownedStart<durationSeconds){const ownedEnd=Math.min(durationSeconds,ownedStart+maxSeconds);
  chunks.push({index:chunks.length,startSeconds:Math.max(0,ownedStart-overlapSeconds),endSeconds:ownedEnd,ownedStartSeconds:ownedStart,ownedEndSeconds:ownedEnd});ownedStart=ownedEnd;}
 return chunks;
}
export function verifyMeasuredDuration(measured,expected){
 if(!Number.isFinite(measured)||measured<=0||measured<expected-2||measured>expected+2)throw Error('source_truncated_or_duration_mismatch');
}
export function originalTime(chunk,relativeTime){if(!Number.isFinite(relativeTime)||relativeTime<0||relativeTime>chunk.endSeconds-chunk.startSeconds)throw Error('invalid_relative_time');return chunk.startSeconds+relativeTime;}
export function run(command,args,timeoutMs=300000){return new Promise((resolve,reject)=>{
 const p=spawn(command,args,{windowsHide:true,stdio:['ignore','pipe','pipe']});let out='',err='';
 p.stdout.on('data',b=>{if(out.length<2e6)out+=b});p.stderr.on('data',b=>{if(err.length<2e6)err+=b});
 const timer=setTimeout(()=>{p.kill();reject(Error('process_timeout'));},timeoutMs);
 p.on('error',e=>{clearTimeout(timer);reject(e)});p.on('exit',code=>{clearTimeout(timer);if(code===0)resolve({stdout:out,stderr:err});else reject(Object.assign(Error('process_exit_'+code),{details:err}));});
});}
function atomic(file,data){fs.writeFileSync(file+'.tmp',JSON.stringify(data,null,2)+'\n');fs.renameSync(file+'.tmp',file);}
export async function acquireMediaItem(item){
 if(!/^[A-Za-z0-9_-]{11}$/.test(item.youtubeId)||item.watchUrl!==`https://www.youtube.com/watch?v=${item.youtubeId}`||!Number.isFinite(item.durationSeconds)||item.durationSeconds<60||item.durationSeconds>7200)throw Error('invalid_media_identity');
 const folder=path.resolve(state,'media',item.youtubeId);fs.mkdirSync(folder,{recursive:true});
 const file=path.join(folder,item.youtubeId+'.m4a'),receiptPath=path.join(folder,'media.json');
 if(fs.existsSync(receiptPath)){
  const cached=JSON.parse(fs.readFileSync(receiptPath));
  if(cached.youtubeId!==item.youtubeId||!fs.existsSync(file)||cached.sourceSha256!==hash(fs.readFileSync(file)))throw Error('media_cache_conflict_preserved_'+item.youtubeId);
  verifyMeasuredDuration(cached.measuredDurationSeconds,item.durationSeconds);return {...cached,cached:true};
 }
 const exe=path.resolve(state,'bin/yt-dlp.exe');
 if(hash(fs.readFileSync(exe))!=='f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d')throw Error('unverified_acquisition_binary');
 if(!fs.existsSync(file))await run(exe,['--no-playlist','--no-cache-dir','--js-runtimes','node','--retries','1','--fragment-retries','1','--max-filesize','80M','--no-progress','-f','bestaudio[language^=en][ext=m4a]/bestaudio[ext=m4a]','--paths',folder,'--output','%(id)s.%(ext)s',item.watchUrl]);
 const probe=await run('ffprobe',['-v','error','-show_entries','format=duration:stream=codec_type,sample_rate,channels','-of','json',file]);
 const metadata=JSON.parse(probe.stdout),duration=Number(metadata.format.duration);verifyMeasuredDuration(duration,item.durationSeconds);
 const receipt={youtubeId:item.youtubeId,sourceSha256:hash(fs.readFileSync(file)),bytes:fs.statSync(file).size,measuredDurationSeconds:duration,streams:metadata.streams,observedAtUtc:new Date().toISOString(),chunks:planChunks(duration),paidRequests:0};
 atomic(receiptPath,receipt);return {...receipt,cached:false};
}
async function main(){
 const manifest=JSON.parse(fs.readFileSync('data/speaking/reach-a1-c2-catalog.json'));
 const pilot=process.argv.includes('--pilot'),one=process.argv.find(x=>x.startsWith('--id='))?.slice(5);
 const level=process.argv.find(x=>x.startsWith('--level='))?.slice(8);
 if(level&&!['A1','A2','B1','B2','C1','C2'].includes(level))throw Error('invalid_level');
 if(one&&!manifest.items.some(x=>x.youtubeId===one))throw Error('unselected_video');
 const items=one?manifest.items.filter(x=>x.youtubeId===one):pilot?['A1','A2','B1','B2','C1','C2'].map(l=>manifest.items.find(x=>x.level===l)):manifest.items.filter(x=>!level||x.level===level);
 const attemptedFailures=process.argv.includes('--resume-untried')&&fs.existsSync(state+'/media-errors.jsonl')
  ?new Set(fs.readFileSync(state+'/media-errors.jsonl','utf8').trim().split('\n').filter(Boolean).map(x=>JSON.parse(x).youtubeId)):new Set();
 let consecutiveForbidden=0;
 for(const item of items){
  if(attemptedFailures.has(item.youtubeId)){console.log('MEDIA-KNOWN-BLOCKED',item.watchUrl);continue;}
  try{
   const receipt=await acquireMediaItem(item);consecutiveForbidden=0;console.log(receipt.cached?'MEDIA-CACHED':'MEDIA-ACQUIRED',item.level,item.youtubeId,'measured='+receipt.measuredDurationSeconds,'chunks='+receipt.chunks.length);
  }catch(e){const code=/429/.test(e.details||'')?'http_429':/403/.test(e.details||'')?'http_403':e.message;
   if(code.startsWith('media_cache_conflict')||code==='unverified_acquisition_binary')throw e;
   fs.appendFileSync(state+'/media-errors.jsonl',JSON.stringify({youtubeId:item.youtubeId,url:item.watchUrl,error:code,observedAtUtc:new Date().toISOString()})+'\n');console.log('MEDIA-BLOCKED',item.watchUrl,code);process.exitCode=2;
   if(code==='http_403')consecutiveForbidden++;else consecutiveForbidden=0;
   // Each URL is attempted once. Isolated forbidden videos are preserved/skipped;
   // rate limiting or consecutive access failures stop the whole acquisition run.
   if(code==='http_429'||consecutiveForbidden>=3||code==='http_403'&&!process.argv.includes('--resume-untried'))break;
   if(code==='http_403')await new Promise(resolve=>setTimeout(resolve,5000));}
 }
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)await main();
