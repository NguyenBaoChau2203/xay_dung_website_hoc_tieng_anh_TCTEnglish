import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {run} from './media.mjs';
const state='.appdata/local/speaking-catalog-transfer';
const digest=b=>crypto.createHash('sha256').update(b).digest('hex');
export const runtime={image:'mmcauliffe/montreal-forced-aligner@sha256:a4fa1c8004712be22ae6290720c412722fc1f6876bcb7d142a762a73d90796b3',version:'3.3.11.dev0+g01b9664b3.d20260706',commit:'01b9664b3288390a8a9c4596a6858502a110ccaf',acousticSha256:'a5c26c360b741e9a8e692416d942dae1fb2a8957dccc0826c4d6a87c98945f73',dictionarySha256:'30da2ad9168290da124369edb8011ebca5e9a6f0d4b0ad5e0fc33fa0fe12ca77'};
export function alignmentWindows(cues,duration,padding=1.5){
 if(!Number.isFinite(duration)||duration<=0||!Number.isFinite(padding)||padding<0)throw Error('invalid_alignment_window');
 return cues.map((cue,index)=>{
  if(!Number.isFinite(cue.startTime)||!Number.isFinite(cue.endTime)||cue.startTime<0||cue.endTime<=cue.startTime||cue.startTime>=duration||!cue.text?.trim())throw Error('invalid_alignment_cue');
  return {index,start:Math.max(0,cue.startTime-padding),end:Math.min(duration,cue.endTime+padding),text:cue.text};
 });
}
export function groupedWindows(cues,duration,size=10,context=0){
 if(!Number.isInteger(size)||size<1||size>20||!Number.isInteger(context)||context<0||context>2)throw Error('invalid_group_size');
 const individual=alignmentWindows(cues,duration),groups=[];
 for(let i=0;i<individual.length;){
  const members=[individual[i++]];
  while(i<individual.length&&members.length<size&&individual[i].end-members[0].start<=240)members.push(individual[i++]);
  const ownedStart=members[0].index,ownedEnd=members.at(-1).index;
  const extended=individual.slice(Math.max(0,ownedStart-context),Math.min(individual.length,ownedEnd+context+1)).map(x=>({...x,owned:x.index>=ownedStart&&x.index<=ownedEnd}));
  if(extended.at(-1).end-extended[0].start>300)throw Error('alignment_context_exceeds_bound');
  groups.push({index:groups.length,start:extended[0].start,end:extended.at(-1).end,text:extended.map(x=>x.text).join(' '),members:extended});
 }
 return groups;
}
function atomic(file,data){fs.writeFileSync(file+'.tmp',JSON.stringify(data,null,2)+'\n');fs.renameSync(file+'.tmp',file);}
async function main(){
 const id=process.argv.find(x=>x.startsWith('--id='))?.slice(5);if(!/^[A-Za-z0-9_-]{11}$/.test(id||''))throw Error('Require --id with exact selected video');
 const manifest=JSON.parse(fs.readFileSync('data/speaking/reach-a1-c2-catalog.json'));
 if(!manifest.items.some(x=>x.youtubeId===id))throw Error('unselected_video');
 const media=JSON.parse(fs.readFileSync(`${state}/media/${id}/media.json`));
 const audio=path.resolve(`${state}/media/${id}/${id}.m4a`);
 if(digest(fs.readFileSync(audio))!==media.sourceSha256)throw Error('media_cache_hash_mismatch');
 const captions=JSON.parse(fs.readFileSync(`${state}/captions/${id}.json`));
 if(captions.youtubeId!==id||captions.source!=='youtube-public-transcript-panel'||captions.cueSha256!==digest(JSON.stringify(captions.cues)))throw Error('caption_cache_hash_mismatch');
 const groupSize=Number(process.argv.find(x=>x.startsWith('--group='))?.slice(8)||1);
 const context=Number(process.argv.find(x=>x.startsWith('--context='))?.slice(10)||0);
 const windows=groupSize===1?alignmentWindows(captions.cues,media.measuredDurationSeconds):groupedWindows(captions.cues,media.measuredDurationSeconds,groupSize,context);
 const job=id+(groupSize===1?'':`-g${groupSize}`)+(context?`c${context}`:'');
 const folder=path.resolve(`${state}/alignment/${job}`),corpus=path.join(folder,'corpus');fs.mkdirSync(corpus,{recursive:true});
 const ffmpeg=path.resolve('.appdata/local/media/ffmpeg.exe');
 if(digest(fs.readFileSync(ffmpeg))!=='228d7a8556258de907fdb55f36850078ebc7680b84ec30d84ea02e99bec1d1eb')throw Error('unverified_decoder_binary');
 for(const w of windows){
  const key=String(w.index).padStart(5,'0'),wav=path.join(corpus,key+'.wav'),text=path.join(corpus,key+'.lab'),receipt=path.join(corpus,key+'.json');
  const fingerprint=digest(JSON.stringify({source:media.sourceSha256,window:w}));
  if(fs.existsSync(receipt)){
   const prior=JSON.parse(fs.readFileSync(receipt));if(prior.fingerprint!==fingerprint||!fs.existsSync(wav)||prior.wavSha256!==digest(fs.readFileSync(wav)))throw Error('alignment_cache_conflict');continue;
  }
  await run(ffmpeg,['-hide_banner','-loglevel','error','-nostdin','-ss',String(w.start),'-i',audio,'-t',String(w.end-w.start),'-ac','1','-ar','16000','-c:a','pcm_s16le','-y',wav]);
  fs.writeFileSync(text,w.text+'\n');atomic(receipt,{...w,fingerprint,wavSha256:digest(fs.readFileSync(wav)),qualityPassed:false});
  console.log('PREPARED',id,w.index);
 }
 atomic(path.join(folder,'input.json'),{youtubeId:id,groupSize,context,mediaSha256:media.sourceSha256,measuredDurationSeconds:media.measuredDurationSeconds,windows,runtime,qualityPassed:false});
 for(const [file,expected] of [['english_mfa.zip',runtime.acousticSha256],['english_mfa.dict',runtime.dictionarySha256]])if(digest(fs.readFileSync(`${state}/models/${file}`))!==expected)throw Error('model_hash_mismatch');
 const mount=`type=bind,source=${path.resolve(state).replaceAll('\\','/')},target=/data`;
 // SQLite WAL locks are unsupported on the Windows bind mount. Transient DB/model
 // extraction uses container RAM; all durable media/results remain on the D mount.
 const result=await run('docker',['run','--rm','--mount',mount,'--tmpfs','/mfa-work:uid=1000,gid=1000,size=1073741824','-e','MFA_ROOT_DIR=/mfa-work',runtime.image,'bash','-c',`mfa align /data/alignment/${job}/corpus /data/models/english_mfa.dict /data/models/english_mfa.zip /data/alignment/${job}/output --output_format json --num_jobs 2 --single_speaker --clean`],3600000);
 fs.writeFileSync(path.join(folder,'alignment.log'),result.stdout+'\n'+result.stderr);
 atomic(path.join(folder,'run.json'),{runtime,completedAtUtc:new Date().toISOString(),alignmentOutputAvailable:true,qualityPassed:false,paidRequests:0});
 console.log('ALIGNED-CANDIDATE',id,'manual coverage/token/playback review remains required');
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)await main();
