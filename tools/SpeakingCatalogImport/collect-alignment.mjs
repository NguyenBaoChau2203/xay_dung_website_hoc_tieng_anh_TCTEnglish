import fs from 'node:fs';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
const state='.appdata/local/speaking-catalog-transfer';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
export function mapAlignedWords(window,output,duration){
 const words=output.tiers?.words?.entries;
 if(!Array.isArray(words)||!words.length)throw Error('missing_word_alignment');
 let end=-1;
 const mapped=words.filter(x=>x[2]&&x[2]!=='<eps>').map(([s,e,text])=>{
  if(!Number.isFinite(s)||!Number.isFinite(e)||s<0||s<end-.001||e<=s||window.start+e>duration+.001||e>window.end-window.start+.001)throw Error('invalid_aligned_word');
  end=e;return {startTime:window.start+s,endTime:window.start+e,text};
 });
 if(!mapped.length)throw Error('empty_word_alignment');
 return {startTime:mapped[0].startTime,endTime:mapped.at(-1).endTime,text:window.text,words:mapped};
}
const tokens=text=>text.toLowerCase().replaceAll('’',"'").split(/\s+/).map(x=>x.replace(/[^\p{L}\p{N}']/gu,'').replace(/^'+|'+$/g,'')).filter(Boolean);
export function splitGroupedWords(window,aligned){
 const members=window.members||[window],source=members.flatMap(x=>tokens(x.text)),target=aligned.words.map(x=>tokens(x.text).join(''));
 const n=source.length,m=target.length;if(!n||!m||n>3000||m>3000)throw Error('unbounded_or_empty_word_mapping');
 const stride=m+1,d=new Uint16Array((n+1)*stride);
 for(let i=0;i<=n;i++)d[i*stride]=i;for(let j=0;j<=m;j++)d[j]=j;
 for(let i=1;i<=n;i++)for(let j=1;j<=m;j++)d[i*stride+j]=Math.min(d[(i-1)*stride+j]+1,d[i*stride+j-1]+1,d[(i-1)*stride+j-1]+(source[i-1]===target[j-1]?0:1));
 const assigned=new Array(n).fill(null);let i=n,j=m;
 while(i||j){
  if(i&&j&&d[i*stride+j]===d[(i-1)*stride+j-1]+(source[i-1]===target[j-1]?0:1)){assigned[i-1]=j-1;i--;j--;}
  else if(i&&d[i*stride+j]===d[(i-1)*stride+j]+1)i--;else j--;
 }
 const cues=[];let position=0;
 for(const member of members){
  const length=tokens(member.text).length,indices=assigned.slice(position,position+length),words=indices.filter(x=>x!==null).map(x=>aligned.words[x]);
  if(!words.length)throw Error('source_sentence_has_no_aligned_words');
  const flags=[];
  if(indices.some((v,k)=>v===null||source[position+k]!==target[v]))flags.push('source_token_mapping_difference');
  cues.push({startTime:words[0].startTime,endTime:words.at(-1).endTime,text:member.text,words,mappingFlags:flags,sourceIndex:member.index,owned:member.owned!==false});position+=length;
 }
 return {cues,editDistance:d[n*stride+m],unmappedOutputWords:m-new Set(assigned.filter(x=>x!==null)).size};
}
async function main(){
 const id=process.argv.find(x=>x.startsWith('--id='))?.slice(5);if(!/^[A-Za-z0-9_-]{11}$/.test(id||''))throw Error('invalid_id');
 const group=Number(process.argv.find(x=>x.startsWith('--group='))?.slice(8)||1);if(!Number.isInteger(group)||group<1||group>20)throw Error('invalid_group');
 const context=Number(process.argv.find(x=>x.startsWith('--context='))?.slice(10)||0);if(!Number.isInteger(context)||context<0||context>2)throw Error('invalid_context');
 const dir=`${state}/alignment/${id}${group===1?'':`-g${group}`}${context?`c${context}`:''}`,input=JSON.parse(fs.readFileSync(dir+'/input.json')),run=JSON.parse(fs.readFileSync(dir+'/run.json'));
 const cues=[],flags=[],outputHashes=[];
 for(const w of input.windows){
  const bytes=fs.readFileSync(`${dir}/output/${String(w.index).padStart(5,'0')}.json`);outputHashes.push(hash(bytes));
  try{const aligned=mapAlignedWords(w,JSON.parse(bytes),input.measuredDurationSeconds);
   const split=w.members?splitGroupedWords(w,aligned):{cues:[aligned],unmappedOutputWords:0};
   if(split.unmappedOutputWords)flags.push(`unmapped_output_words_in_group_${w.index}`);
   for(const cue of split.cues.filter(x=>x.owned!==false)){
    const index=cue.sourceIndex??w.index;
    if(cue.startTime<(cues.at(-1)?.endTime||0))flags.push(`overlap_at_source_cue_${index}`);
    if(cue.words.some(x=>/^<unk>$|^spn$|^<oov>$/i.test(x.text)))flags.push(`unknown_words_at_source_cue_${index}`);
    for(const flag of cue.mappingFlags||[])flags.push(`cue_${index}_${flag}`);
    cues.push(cue);
   }
  }catch(e){flags.push(`cue_${w.index}_${e.message}`);}
 }
 flags.push('whole_speech_interval_review_pending','manual_token_accuracy_pending','beginning_middle_late_boundary_playback_pending');
 const candidate={youtubeId:id,groupSize:group,context,mediaSha256:input.mediaSha256,sourceSha256:hash(fs.readFileSync(`${state}/captions/${id}.json`)),measuredDurationSeconds:input.measuredDurationSeconds,runtime:run.runtime,sourceCueCount:input.windows.reduce((n,w)=>n+(w.members?.filter(x=>x.owned!==false).length||1),0),cues,outputHashes,unresolvedFlags:flags,qualityPassed:false,observedAtUtc:new Date().toISOString()};
 fs.writeFileSync(dir+'/candidate.json.tmp',JSON.stringify(candidate,null,2)+'\n');fs.renameSync(dir+'/candidate.json.tmp',dir+'/candidate.json');
 console.log(id,'aligned_candidates='+cues.length,'tail='+cues.at(-1)?.endTime,'quality_passed=false','flags='+flags.length);
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)await main();
