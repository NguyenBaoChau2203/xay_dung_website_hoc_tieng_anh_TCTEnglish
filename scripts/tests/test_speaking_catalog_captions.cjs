'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const mod=()=>import('../../tools/SpeakingCatalogImport/captions.mjs');
function panel(text='The speaker and the listener are here for the English lesson.') {
 return {content:{engagementPanelSectionListRenderer:{content:{sectionListRenderer:{contents:[{itemSectionRenderer:{sectionIdentifier:'timeline_view_section0',contents:[{macroMarkersPanelItemViewModel:{item:{timelineItemViewModel:{contentItems:[{transcriptSegmentViewModel:{simpleText:text}}]}},onTap:{innertubeCommand:{watchEndpoint:{startTimeSeconds:1801}}}}}]}}]}}}},frameworkUpdates:{entityBatchUpdate:{mutations:[{payload:{timedMarkersListSyncEntity:{timedListData:{sections:[{sectionId:'timeline_view_section0',timedSyncDataList:[{videoTimeMs:'1801000',endTimeMs:'1806500'}]}]}}}}]}}};
}
test('modern public transcript preserves supplied full-duration millisecond timing and repeated text',async()=>{
 const {parsePanel}=await mod();const p=panel();const s=p.content.engagementPanelSectionListRenderer.content.sectionListRenderer.contents[0].itemSectionRenderer;
 s.contents.push(structuredClone(s.contents[0]));s.contents[1].macroMarkersPanelItemViewModel.onTap.innertubeCommand.watchEndpoint.startTimeSeconds=1900;
 p.frameworkUpdates.entityBatchUpdate.mutations[0].payload.timedMarkersListSyncEntity.timedListData.sections[0].timedSyncDataList.push({videoTimeMs:'1900000',endTimeMs:'1905500'});
 const cues=parsePanel(p);assert.equal(cues.length,2);assert.deepEqual(cues.map(x=>[x.startTime,x.endTime]),[[1801,1806.5],[1900,1905.5]]);assert.equal(cues[0].text,cues[1].text);
});
test('missing end, mismatch, nonfinite timing, malformed or non-English data fail without invented cues',async()=>{
 const {parsePanel}=await mod();assert.throws(()=>parsePanel({}),/transcript/);
 let p=panel();delete p.frameworkUpdates.entityBatchUpdate.mutations[0].payload.timedMarkersListSyncEntity.timedListData.sections[0].timedSyncDataList[0].endTimeMs;assert.throws(()=>parsePanel(p),/timing/);
 p=panel();p.frameworkUpdates.entityBatchUpdate.mutations[0].payload.timedMarkersListSyncEntity.timedListData.sections[0].timedSyncDataList[0].videoTimeMs='NaN';assert.throws(()=>parsePanel(p),/timing/);
 p=panel();delete p.content.engagementPanelSectionListRenderer.content.sectionListRenderer.contents[0].itemSectionRenderer.contents[0].macroMarkersPanelItemViewModel.onTap.innertubeCommand.watchEndpoint.startTimeSeconds;assert.throws(()=>parsePanel(p),/timing/);
 assert.throws(()=>parsePanel(panel('これは英語ではありません。')),/English/);
});
test('empty200,403 and429 are distinct acquisition failures; cancellation propagates',async()=>{
 const {readJsonResponse}=await mod();await assert.rejects(readJsonResponse(new Response('',{status:200})),/empty_200/);
 for(const status of [403,429])await assert.rejects(readJsonResponse(new Response('',{status})),new RegExp('http_'+status));
 await assert.rejects(readJsonResponse(new Response('broken',{status:200})),/malformed_json/);
 const response={ok:true,status:200,text:()=>Promise.reject(new DOMException('Cancelled','AbortError'))};await assert.rejects(readJsonResponse(response),{name:'AbortError'});
});
