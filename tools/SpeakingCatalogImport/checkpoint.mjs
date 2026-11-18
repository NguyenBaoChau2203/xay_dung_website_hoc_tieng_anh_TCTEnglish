import fs from 'node:fs';
import crypto from 'node:crypto';
const root='.appdata/local/speaking-catalog-transfer';
const file='data/speaking/reach-a1-c2-catalog.json';
const manifest=JSON.parse(fs.readFileSync(file));
const errors=fs.existsSync(root+'/media-errors.jsonl')?fs.readFileSync(root+'/media-errors.jsonl','utf8').trim().split('\n').filter(Boolean).map(x=>JSON.parse(x)):[];
const counts=[],mappings=[];
for(const item of manifest.items){
 const receiptPath=`${root}/receipts/${item.youtubeId}.json`,originalReceiptPath=`${root}/receipts/${item.replacementOfYoutubeId}.json`;
 let receipt=fs.existsSync(receiptPath)?JSON.parse(fs.readFileSync(receiptPath)):null;
 if(!receipt&&item.replacementOfYoutubeId&&fs.existsSync(originalReceiptPath)){
  const original=JSON.parse(fs.readFileSync(originalReceiptPath));
  receipt={...original,youtubeId:item.youtubeId,listeningYoutubeId:item.replacementOfYoutubeId,speakingId:null,speakingCreated:false,speakingKey:'tct.speaking.youtube.'+crypto.createHash('sha256').update(item.youtubeId).digest('hex')};
 }
 if(receipt)mappings.push(receipt);
 const mediaPath=`${root}/media/${item.youtubeId}/media.json`;
 item.transfer.listeningRetired=receipt?.listeningRetired===true;
 item.transfer.speakingPublished=receipt?.speakingId!=null;
 item.transfer.availableForLocalTest=receipt?.speakingId!=null;
 item.transfer.audioMeasured=fs.existsSync(mediaPath);
 const paths=['-g10c2','-g10c1','-g10',''].flatMap(s=>[`${root}/alignment/${item.youtubeId}${s}/ctc-candidate.json`,`${root}/alignment/${item.youtubeId}${s}/candidate.json`]);
 const rawCues=JSON.parse(fs.readFileSync(`${root}/captions/${item.youtubeId}.json`)).cues;
 const rawCount=rawCues.length,englishCount=rawCues.filter(x=>!/^\s*\[(music|applause|laughter)\]\s*$/i.test(x.text)).length;
 const candidateFolder=paths.filter(x=>fs.existsSync(x)).sort((a,b)=>Number(b.endsWith('ctc-candidate.json'))-Number(a.endsWith('ctc-candidate.json'))).find(x=>[rawCount,englishCount].includes(JSON.parse(fs.readFileSync(x)).cues.length));
 item.transfer.alignmentCandidateAvailable=!!candidateFolder;
 item.transfer.ctcCandidateAvailable=paths.some(x=>x.endsWith('ctc-candidate.json')&&fs.existsSync(x));
 if(candidateFolder){const candidate=JSON.parse(fs.readFileSync(candidateFolder));item.transfer.alignmentCandidatePath=candidateFolder;item.transfer.alignmentCandidateUnresolvedFlags=candidate.unresolvedFlags.length;}
 const failure=errors.findLast(x=>x.youtubeId===item.youtubeId);
 item.transfer.blockedReason=!item.transfer.audioMeasured&&failure?`Audio acquisition ${failure.error}: ${failure.url}`:null;
 // Acquisition is evidence of availability, never evidence of review/quality.
 if(item.transfer.availableForLocalTest&&!item.transfer.qualityPassed)item.transfer.status='available-local-test-quality-pending';
 else if(item.transfer.captionReady&&!item.transfer.qualityPassed)item.transfer.status='timed-captions-acquired-quality-pending';
}
for(const level of ['A1','A2','B1','B2','C1','C2']){
 const a=manifest.items.filter(x=>x.level===level);
 counts.push({level,selected:a.length,replaced:a.filter(x=>x.replacementOfYoutubeId).length,captionReady:a.filter(x=>x.transfer.captionReady).length,audioMeasured:a.filter(x=>x.transfer.audioMeasured).length,asrRequired:a.filter(x=>x.transfer.asrRequired===true).length,alignmentCandidates:a.filter(x=>x.transfer.alignmentCandidateAvailable).length,aligned:a.filter(x=>x.transfer.alignmentReady).length,qualityPassed:a.filter(x=>x.transfer.qualityPassed).length,speakingPublished:a.filter(x=>x.transfer.speakingPublished).length,listeningRetired:a.filter(x=>x.transfer.listeningRetired).length});
}
fs.writeFileSync(file+'.tmp',JSON.stringify(manifest,null,2)+'\n');fs.renameSync(file+'.tmp',file);
fs.writeFileSync(root+'/checkpoint.json',JSON.stringify({observedAtUtc:new Date().toISOString(),counts,paidRequests:0,paidCostUsd:0},null,2)+'\n');
fs.writeFileSync(root+'/transfer-map.json.tmp',JSON.stringify({observedAtUtc:new Date().toISOString(),mappings},null,2)+'\n');fs.renameSync(root+'/transfer-map.json.tmp',root+'/transfer-map.json');
console.table(counts);
