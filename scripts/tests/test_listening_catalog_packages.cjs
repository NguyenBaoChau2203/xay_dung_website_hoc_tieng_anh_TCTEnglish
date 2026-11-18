'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const repo = path.resolve(__dirname, '../..');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

test('catalog has 90 distinct evidenced additions and canonical public packages match them', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(repo, 'data/listening/reach-a1-c2-catalog.json')));
  const catalog = JSON.parse(fs.readFileSync(path.join(repo, 'data/shared/catalog.json')));
  assert.equal(manifest.items.length, 90);
  assert.equal(new Set(manifest.items.map(x => x.youtubeId)).size, 90);
  for (const level of ['A1', 'A2', 'B1', 'B2', 'C1', 'C2']) assert.equal(manifest.items.filter(x => x.level === level).length, 15);
  for (const item of manifest.items) {
    assert.equal(item.availability.status, 'OK');
    assert.equal(item.availability.isPublic, true);
    assert.equal(item.availability.playableInEmbed, true);
    assert.equal(item.levelEvidence.url, item.watchUrl);
    assert.match(item.sourceTitle, new RegExp(`\\b${item.level}\\b`));
    const identity = hash(Buffer.from(item.youtubeId, 'utf8'));
    const release = catalog.releases.find(x => x.id === `listening-youtube-${identity}`);
    assert.ok(release, item.watchUrl);
    const filename = path.join(repo, 'data/shared', release.path);
    const bytes = fs.readFileSync(filename);
    assert.equal(hash(bytes), release.sha256, release.path);
    const pkg = JSON.parse(bytes);
    assert.equal(pkg.records.length, 1, 'This video-only batch exports only its public lesson root');
    const record = pkg.records[0];
    assert.equal(record.key, `tct.listening.youtube.${identity}`);
    assert.equal(record.kind, 'ListeningLesson');
    assert.equal(record.values.YoutubeId, item.youtubeId);
    assert.equal(record.values.Level, item.level);
    assert.equal(record.values.Title, item.displayTitle);
    assert.equal(record.values.IsPublished, true);
    assert.equal(record.values.TranscriptSource, null);
    assert.deepEqual(record.references, {});
    assert.deepEqual(pkg.assets, []);
    const raw = spawnSync('git', ['hash-object', '--no-filters', filename], { cwd: repo, encoding: 'utf8' });
    const throughGit = spawnSync('git', ['hash-object', '--path', release.path.replace(/^packages\//, 'data/shared/packages/'), filename], { cwd: repo, encoding: 'utf8' });
    assert.equal(raw.status, 0); assert.equal(throughGit.status, 0);
    assert.equal(throughGit.stdout.trim(), raw.stdout.trim(), 'Git must preserve bytes covered by catalog SHA256');
  }
});
