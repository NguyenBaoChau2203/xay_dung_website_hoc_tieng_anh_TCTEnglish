const fs=require('node:fs'),path=require('node:path'),test=require('node:test'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const file=()=>path.resolve('.appdata/local/temp',crypto.randomUUID()+'-synthetic-cost.json');
const approval={ownerApproved:true,model:'gpt-transcribe',reference:'OFFLINE SYNTHETIC TEST APPROVAL ONLY',maximumUsd:.002,usdPerMinute:.0045};
test('absent owner authority blocks reservation before file creation or request',async()=>{
 const {CatalogCostLedger}=await import('../../tools/SpeakingCatalogImport/cost-ledger.mjs');const p=file();
 assert.throws(()=>new CatalogCostLedger(p,null).reserve({sha256:'a'.repeat(64),seconds:10}),/approval_required/);assert.equal(fs.existsSync(p),false);
});
test('successful paid chunk is cacheable across restart and bound includes overlap',async()=>{
 const {CatalogCostLedger}=await import('../../tools/SpeakingCatalogImport/cost-ledger.mjs');const p=file(),chunk={sha256:'a'.repeat(64),seconds:11.5};
 const first=new CatalogCostLedger(p,approval).reserve(chunk);assert.equal(first.cached,false);assert.equal(first.receipt.boundMicroUsd,863);
 new CatalogCostLedger(p,approval).finish(first.receipt.requestId,{state:'success',transcriptSha256:'b'.repeat(64)});
 const cached=new CatalogCostLedger(p,approval).reserve(chunk);assert.equal(cached.cached,true);assert.equal(JSON.parse(fs.readFileSync(p)).entries.length,1);
});
test('unknown or interrupted POST cannot automatically retry; reserved cost enforces ceiling',async()=>{
 const {CatalogCostLedger}=await import('../../tools/SpeakingCatalogImport/cost-ledger.mjs');const p=file(),chunk={sha256:'c'.repeat(64),seconds:20};
 const reserved=new CatalogCostLedger(p,approval).reserve(chunk);assert.throws(()=>new CatalogCostLedger(p,approval).reserve(chunk),/reconciliation/);
 new CatalogCostLedger(p,approval).finish(reserved.receipt.requestId,{state:'unknown'});
 assert.throws(()=>new CatalogCostLedger(p,approval).reserve(chunk),/reconciliation/);
 assert.throws(()=>new CatalogCostLedger(p,approval).reserve({sha256:'d'.repeat(64),seconds:10}),/ceiling/);
 assert.equal(JSON.parse(fs.readFileSync(p)).entries.length,1);
});
