const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const repo=path.resolve(__dirname,'../..');
test('all ninety Speaking packages match portable acquisition evidence and real subject categories',()=>{
 const manifest=JSON.parse(fs.readFileSync(path.join(repo,'data/speaking/reach-a1-c2-catalog.json'))),catalog=JSON.parse(fs.readFileSync(path.join(repo,'data/shared/catalog.json')));
 const evidence=JSON.parse(fs.readFileSync(path.join(repo,'data/speaking/reach-a1-c2-transcript-evidence.json')));
 assert.equal(evidence.schemaVersion,1);assert.equal(evidence.qualityStatus,'full-acoustic-and-timing-review-pending');
 assert.equal(evidence.items.length,90);assert.equal(new Set(evidence.items.map(x=>x.youtubeId)).size,90);
 const releases=catalog.releases.filter(x=>x.id.startsWith('speaking-youtube-'));
 assert.equal(releases.length,90);let total=0;
 for(const item of manifest.items){
  const release=releases.find(x=>x.id==='speaking-youtube-'+hash(item.youtubeId));assert.ok(release,item.youtubeId);assert.equal(release.version,1);
  const bytes=fs.readFileSync(path.join(repo,'data/shared',release.path));assert.equal(hash(bytes),release.sha256);assert.equal(bytes.includes(Buffer.from('\r\n')),false);
  const pkg=JSON.parse(bytes),root=pkg.records.find(x=>x.kind==='SpeakingVideo'),proof=evidence.items.find(x=>x.youtubeId===item.youtubeId);
  assert.ok(proof,item.youtubeId);assert.ok(Number.isFinite(proof.measuredDurationSeconds)&&proof.measuredDurationSeconds>0);
  assert.equal(root.key,'tct.speaking.youtube.'+hash(item.youtubeId));assert.equal(root.values.YoutubeId,item.youtubeId);
  assert.equal(root.values.Topic,item.catalogTopic);assert.equal(root.values.Level,item.level);assert.equal(root.values.ImportStatus,'ready');
  assert.equal(root.values.TranscriptSource,'youtube-captions');assert.equal(proof.fullSpeechReviewed,false);
  assert.ok(proof.unresolvedFlags.includes('owner-local-test-before-staging-qa'));
  const children=pkg.records.filter(x=>x.kind==='SpeakingSentence');assert.equal(children.length,proof.cueCount);total+=children.length;
  const normalize=c=>JSON.stringify([c.startTime??c.StartTime,c.endTime??c.EndTime,c.text??c.Text]);
  assert.equal(hash(children.map(x=>normalize(x.values)).sort().join('\n')),proof.cueSha256,item.youtubeId);
  for(const record of children){assert.equal(record.references.VideoId,root.key);assert.ok(record.values.StartTime>=0);assert.ok(record.values.EndTime>record.values.StartTime);assert.ok(record.values.EndTime<=proof.measuredDurationSeconds);}
  assert.ok(pkg.records.every(x=>['SpeakingVideo','SpeakingSentence'].includes(x.kind)));
 }
 assert.ok(total>16000);
});
