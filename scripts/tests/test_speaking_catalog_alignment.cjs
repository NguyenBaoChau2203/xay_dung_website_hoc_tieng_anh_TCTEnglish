const test=require('node:test');const assert=require('node:assert/strict');
test('alignment search windows retain original offsets and true repetitions beyond 30 minutes',async()=>{
 const {alignmentWindows}=await import('../../tools/SpeakingCatalogImport/align.mjs');
 const windows=alignmentWindows([{startTime:0,endTime:4,text:'Real repeated phrase.'},{startTime:1901,endTime:1905,text:'Real repeated phrase.'},{startTime:2601,endTime:2607,text:'Actual tail.'}],2605);
 assert.deepEqual(windows.map(x=>[x.start,x.end]),[[0,5.5],[1899.5,1906.5],[2599.5,2605]]);
 assert.equal(windows[0].text,windows[1].text);assert.equal(windows.length,3);
 assert.throws(()=>alignmentWindows([{startTime:2606,endTime:2607,text:'outside'}],2605));
 assert.throws(()=>alignmentWindows([{startTime:NaN,endTime:4,text:'invalid'}],2605));
});
test('grouped alignment maps every sentence to genuine ordered word times without proportional timing',async()=>{
 const {groupedWindows}=await import('../../tools/SpeakingCatalogImport/align.mjs');
 const {splitGroupedWords}=await import('../../tools/SpeakingCatalogImport/collect-alignment.mjs');
 const source=[{startTime:1801,endTime:1804,text:'Repeat this.'},{startTime:1804,endTime:1807,text:'Repeat this.'}];
 const group=groupedWindows(source,1900,10)[0];assert.equal(group.members.length,2);
 const words=[{startTime:1801.2,endTime:1802,text:'repeat'},{startTime:1802,endTime:1802.4,text:'this'},{startTime:1804.7,endTime:1805,text:'repeat'},{startTime:1805,endTime:1806.3,text:'this'}];
 const mapped=splitGroupedWords(group,{words});assert.equal(mapped.editDistance,0);assert.equal(mapped.cues.length,2);
 assert.deepEqual(mapped.cues.map(x=>[x.startTime,x.endTime]),[[1801.2,1802.4],[1804.7,1806.3]]);
 assert.equal(mapped.cues[0].text,mapped.cues[1].text);assert.equal(mapped.cues[0].mappingFlags.length,0);
 const missing=splitGroupedWords(group,{words:words.slice(1)});assert.ok(missing.cues.some(x=>x.mappingFlags.length));
});
test('context overlap is discarded by source ownership, preserving genuine repeated teaching sections',async()=>{
 const {groupedWindows}=await import('../../tools/SpeakingCatalogImport/align.mjs');
 const source=Array.from({length:25},(_,i)=>({startTime:i*5,endTime:i*5+4,text:'Repeat this real phrase.'}));
 const groups=groupedWindows(source,126,10,1);
 assert.deepEqual(groups.flatMap(g=>g.members.filter(x=>x.owned).map(x=>x.index)),source.map((_,i)=>i));
 assert.ok(groups.flatMap(g=>g.members).length>25);assert.equal(groups.length,3);
 assert.ok(groups.every(g=>g.end-g.start<=300));
});
test('word times come from alignment and original offsets; invalid timing is never fabricated',async()=>{
 const {mapAlignedWords}=await import('../../tools/SpeakingCatalogImport/collect-alignment.mjs');
 const window={start:1899.5,end:1906.5,text:'Repeated genuine phrase.'};
 const output={tiers:{words:{entries:[[1.2,1.8,'repeated'],[1.8,2.3,'genuine'],[2.3,2.8,'phrase']]}}};
 const cue=mapAlignedWords(window,output,2605);assert.equal(cue.startTime,1900.7);assert.equal(cue.endTime,1902.3);assert.equal(cue.text,window.text);
 assert.throws(()=>mapAlignedWords(window,{tiers:{words:{entries:[[1,NaN,'bad']]}}},2605));
 assert.throws(()=>mapAlignedWords(window,{tiers:{words:{entries:[]}}},2605));
});
