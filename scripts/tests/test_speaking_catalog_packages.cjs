'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {spawnSync}=require('node:child_process');
const root=path.resolve(__dirname,'../..'),sha=b=>crypto.createHash('sha256').update(b).digest('hex');
test('Speaking selection maps original roots and explicit same-level owner replacements; immutable withdrawals survive Git bytes',()=>{
 const oldBytes=fs.readFileSync(path.join(root,'data/listening/reach-a1-c2-catalog.json')),old=JSON.parse(oldBytes);
 const selected=JSON.parse(fs.readFileSync(path.join(root,'data/speaking/reach-a1-c2-catalog.json'))),catalog=JSON.parse(fs.readFileSync(path.join(root,'data/shared/catalog.json')));
 assert.equal(selected.supersedes.sha256,sha(oldBytes));assert.deepEqual(selected.items.map(x=>x.replacementOfYoutubeId||x.youtubeId).sort(),old.items.map(x=>x.youtubeId).sort());
 assert.equal(new Set(selected.items.map(x=>x.youtubeId)).size,90);
 const categories=['Ăn uống','Vui chơi & giải trí','Trò chuyện & tình cảm','Gia đình & đời sống','Mua sắm & tài chính','Sức khỏe','Học tập & công việc','Xã hội & di chuyển','Khoa học & văn hóa','Luyện thi tổng hợp'];
 for(const item of selected.items){assert.ok(categories.includes(item.catalogTopic));assert.equal(item.topicEvidence.source,item.watchUrl);assert.equal(item.topicEvidence.detail,item.topic);}
 for(const level of ['A1','A2','B1','B2','C1','C2'])assert.equal(selected.items.filter(x=>x.level===level).length,15);
 for(const item of selected.items.filter(x=>x.replacementOfYoutubeId)){
  const decision=selected.replacements.find(x=>x.originalYoutubeId===item.replacementOfYoutubeId&&x.replacementYoutubeId===item.youtubeId);
  assert.equal(decision.ownerAuthorization,'owner-chat-2026-11-13-replace-unavailable-with-same-level-transcript');
  assert.equal(item.level,old.items.find(x=>x.youtubeId===item.replacementOfYoutubeId).level);assert.equal(decision.level,item.level);
  assert.equal(decision.evidenceUrl,item.watchUrl);assert.equal(decision.reason,'audio-http-403');
  assert.ok(!old.items.some(x=>x.youtubeId===item.youtubeId));assert.equal(item.transfer.captionReady,true);
 }
 for(const item of old.items){
  const id='listening-youtube-'+sha(Buffer.from(item.youtubeId));const releases=catalog.releases.filter(x=>x.id===id).sort((a,b)=>a.version-b.version);
  assert.equal(releases[0].version,1);assert.ok(releases.length>=2,item.youtubeId);
  for(const release of releases){const filename=path.join(root,'data/shared',release.path),b=fs.readFileSync(filename);assert.equal(sha(b),release.sha256);
   const pkg=JSON.parse(b);assert.equal(pkg.records[0].kind,'ListeningLesson');assert.equal(pkg.records[0].values.YoutubeId,item.youtubeId);
   assert.equal(pkg.records[0].values.IsPublished,release.version===1);
   if(release.version>1)assert.equal(b.includes(Buffer.from('\r\n')),false,'New versions use LF before hashing');
   const raw=spawnSync('git',['hash-object','--no-filters',filename],{cwd:root,encoding:'utf8'});
   const clean=spawnSync('git',['hash-object','--path','data/shared/'+release.path,filename],{cwd:root,encoding:'utf8'});
   assert.equal(raw.status,0);assert.equal(clean.status,0);assert.equal(raw.stdout.trim(),clean.stdout.trim());
  }
 }
});
test('superseded Listening CLI rejects authoring even with SkipBuild before any dependency/DB operation',()=>{
 const r=spawnSync('pwsh',['-NoProfile','-File','scripts/import-listening-catalog.ps1','-Action','Import','-SkipBuild'],{cwd:root,encoding:'utf8'});
 assert.notEqual(r.status,0);assert.match(r.stderr+r.stdout,/superseded by Speaking/);
});
