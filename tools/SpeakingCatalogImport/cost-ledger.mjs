import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

// Offline accounting primitive only. There is deliberately no API/key/client in
// this module. Production approval must come from the owner, never saved keys.
export class CatalogCostLedger {
 constructor(file,approval){this.file=file;this.approval=approval;}
 mutate(action){
  const a=this.approval;
  if(!a?.ownerApproved||a.model!=='gpt-transcribe'||!a.reference?.trim()||!Number.isFinite(a.maximumUsd)||a.maximumUsd<=0||!Number.isFinite(a.usdPerMinute)||a.usdPerMinute<=0)throw Error('explicit_owner_spending_approval_required');
  fs.mkdirSync(path.dirname(this.file),{recursive:true});const lock=this.file+'.lock',handle=fs.openSync(lock,'wx');
  try{
   const ledger=fs.existsSync(this.file)?JSON.parse(fs.readFileSync(this.file)):{schemaVersion:1,approvalReference:a.reference,entries:[]};
   if(ledger.schemaVersion!==1||ledger.approvalReference!==a.reference)throw Error('cost_ledger_approval_conflict');
   const result=action(ledger,a);
   fs.writeFileSync(this.file+'.tmp',JSON.stringify(ledger,null,2)+'\n');fs.renameSync(this.file+'.tmp',this.file);return result;
  }finally{fs.closeSync(handle);fs.unlinkSync(lock);}
 }
 reserve(chunk){return this.mutate((ledger,a)=>{
  if(!/^[a-f0-9]{64}$/.test(chunk.sha256)||!Number.isFinite(chunk.seconds)||chunk.seconds<=0||chunk.seconds>302)throw Error('invalid_bounded_chunk');
  const previous=ledger.entries.find(x=>x.sha256===chunk.sha256);
  if(previous){if(previous.seconds!==chunk.seconds)throw Error('chunk_duration_conflict');if(previous.state==='success')return {cached:true,receipt:previous};throw Error('unknown_or_reserved_post_requires_reconciliation');}
  const ceiling=Math.floor(a.maximumUsd*1e6),bound=Math.ceil(chunk.seconds/60*a.usdPerMinute*1e6);
  if(ledger.entries.reduce((n,x)=>n+x.boundMicroUsd,0)+bound>ceiling)throw Error('approved_cost_ceiling_exceeded');
  const receipt={sha256:chunk.sha256,seconds:chunk.seconds,boundMicroUsd:bound,state:'reserved',requestId:crypto.randomUUID(),reservedAtUtc:new Date().toISOString()};
  ledger.entries.push(receipt);return {cached:false,receipt};
 });}
 finish(requestId,result){return this.mutate(ledger=>{
  const entry=ledger.entries.find(x=>x.requestId===requestId);if(!entry||entry.state!=='reserved')throw Error('request_state_conflict');
  if(result.state==='success'){
   if(!/^[a-f0-9]{64}$/.test(result.transcriptSha256))throw Error('invalid_success_cache_hash');entry.state='success';entry.transcriptSha256=result.transcriptSha256;
  }else if(result.state==='unknown')entry.state='unknown';else throw Error('unknown_outcome_is_not_a_free_retry');
  entry.completedAtUtc=new Date().toISOString();return entry;
 });}
}
