'use strict';
const test=require('node:test'),assert=require('node:assert/strict');
test('catalog chunks cover the complete >30 minute source and preserve offsets/repeated sections',async()=>{
 const {planChunks,originalTime,verifyMeasuredDuration}=await import('../../tools/SpeakingCatalogImport/media.mjs');
 const chunks=planChunks(2906.37);assert.equal(chunks.at(-1).endSeconds,2906.37);assert.ok(chunks.at(-1).ownedStartSeconds>1800);
 let end=0;for(const c of chunks){assert.equal(c.ownedStartSeconds,end);assert.ok(c.endSeconds-c.startSeconds<=301.5);assert.ok(c.startSeconds<=c.ownedStartSeconds);end=c.ownedEndSeconds;}
 assert.equal(originalTime(chunks[1],2),300.5);assert.throws(()=>verifyMeasuredDuration(1800,2906),/truncated/);
 assert.throws(()=>planChunks(NaN),/bounds/);assert.throws(()=>originalTime(chunks[0],-1),/relative/);
 assert.throws(()=>planChunks(300,NaN),/bounds/);assert.throws(()=>planChunks(300,300,Infinity),/bounds/);
 // Chunks are timeline intervals; identical teaching text in distant intervals is never deduplicated.
 assert.equal(chunks.filter(c=>c.startSeconds<=1810&&c.endSeconds>=1810).length,1);
});
