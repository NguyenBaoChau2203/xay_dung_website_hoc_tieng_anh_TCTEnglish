const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
test('transcript click resolves the practice API after its script loads and keeps text/highlight selection together',()=>{
 const view=fs.readFileSync('TCTEnglish/Views/Speaking/Practice.cshtml','utf8');
 const start=view.indexOf('/* Hook into speaking.js:'),end=view.indexOf("document.getElementById('dic-input')",start);
 assert.ok(start>=0&&end>start);const calls=[],highlights=[],context={window:{},document:{addEventListener(){}},setTimeout:fn=>fn(),updateTranscriptPanel:i=>highlights.push(i)};
 vm.runInNewContext(view.slice(start,end),context);
 context.window.TctSpeakingPractice={selectSentence:(i,autoPlay)=>calls.push({i,autoPlay})};
 context.window.selectSentence(109,false);context.window.selectSentence(218,true);
 assert.deepEqual(calls,[{i:109,autoPlay:false},{i:218,autoPlay:true}]);assert.deepEqual(highlights,[109,218]);
});
