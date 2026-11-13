const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
test('all ninety local Speaking aggregates export complete acquired transcript children and real subject categories',()=>{
 const manifest=JSON.parse(fs.readFileSync('data/speaking/reach-a1-c2-catalog.json')),catalog=JSON.parse(fs.readFileSync('data/shared/catalog.json'));
 const releases=catalog.releases.filter(x=>x.id.startsWith('speaking-youtube-'));
 assert.equal(releases.length,90);let total=0;
 for(const item of manifest.items){
  const release=releases.find(x=>x.id==='speaking-youtube-'+hash(item.youtubeId));assert.ok(release,item.youtubeId);assert.equal(release.version,1);
  const bytes=fs.readFileSync(path.join('data/shared',release.path));assert.equal(hash(bytes),release.sha256);assert.equal(bytes.includes(Buffer.from('\r\n')),false);
  const pkg=JSON.parse(bytes),root=pkg.records.find(x=>x.kind==='SpeakingVideo'),draft=JSON.parse(fs.readFileSync('.appdata/local/speaking-catalog-transfer/local-test/'+item.youtubeId+'.json'));
  assert.equal(root.key,'tct.speaking.youtube.'+hash(item.youtubeId));assert.equal(root.values.YoutubeId,item.youtubeId);
  assert.equal(root.values.Topic,item.catalogTopic);assert.equal(root.values.Level,item.level);assert.equal(root.values.ImportStatus,'ready');
  assert.equal(root.values.TranscriptSource,'youtube-captions');assert.equal(draft.proof.fullSpeechReviewed,false);
  assert.ok(draft.proof.unresolvedFlags.includes('owner-local-test-before-staging-qa'));
  const children=pkg.records.filter(x=>x.kind==='SpeakingSentence');assert.equal(children.length,draft.cues.length);total+=children.length;
  const normalize=c=>JSON.stringify([c.startTime??c.StartTime,c.endTime??c.EndTime,c.text??c.Text]);
  assert.deepEqual(children.map(x=>normalize(x.values)).sort(),draft.cues.map(normalize).sort());
  for(const record of children){assert.equal(record.references.VideoId,root.key);assert.ok(record.values.StartTime>=0);assert.ok(record.values.EndTime<=draft.proof.measuredDurationSeconds);}
  assert.ok(pkg.records.every(x=>['SpeakingVideo','SpeakingSentence'].includes(x.kind)));
 }
 assert.ok(total>16000);
});
