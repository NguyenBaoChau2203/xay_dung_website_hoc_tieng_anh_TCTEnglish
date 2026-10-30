'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { readLocalPolicy } = require('../../tooling/codegraph/project-policy.cjs');
const { restrictRequest, restrictToolList } = require('../../tooling/codegraph/mcp-autosync.cjs');

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct-codegraph-policy-'));
  fs.mkdirSync(path.join(root, '.ai'));
  fs.mkdirSync(path.join(root, '.codegraph'));
  fs.writeFileSync(path.join(root, '.codegraph/codegraph.db'), 'fixture');
  t.after(() => {
    const actual = fs.realpathSync(root);
    assert.equal(path.dirname(actual), fs.realpathSync(os.tmpdir()));
    assert.ok(path.basename(actual).startsWith('tct-codegraph-policy-'));
    fs.rmSync(actual, { recursive: true });
  });
  const policy = { schemaVersion: 1, mode: 'auto-sync', checkoutRoot: fs.realpathSync(root), reviewedVersion: '1.6.2', debounceMs: 2000 };
  return { root, policy, save(value = policy) { fs.writeFileSync(path.join(root, '.ai/codegraph.local.json'), JSON.stringify(value)); } };
}

test('a checkout without local approval remains read-only', t => {
  const f = fixture(t);
  assert.equal(readLocalPolicy(f.root, '1.6.2').autoSync, false);
});
test('approval is scoped to this checkout and reviewed version', t => {
  const f = fixture(t); f.save();
  assert.deepEqual(readLocalPolicy(f.root, '1.6.2'), { autoSync: true, debounceMs: 2000 });
});
test('a copied approval from another checkout cannot enable a writer', t => {
  const f = fixture(t); f.save({ ...f.policy, checkoutRoot: os.tmpdir() });
  assert.throws(() => readLocalPolicy(f.root, '1.6.2'), /checkout/i);
});
test('a new package version needs reviewed approval again', t => {
  const f = fixture(t); f.save();
  assert.throws(() => readLocalPolicy(f.root, '1.6.3'), /version/i);
});
test('an opted-in checkout cannot silently create a missing index', t => {
  const f = fixture(t); f.save(); fs.unlinkSync(path.join(f.root, '.codegraph/codegraph.db'));
  assert.throws(() => readLocalPolicy(f.root, '1.6.2'), /index/i);
});
test('read-only mode explicitly disables the writer', t => {
  const f = fixture(t); f.save({ schemaVersion: 1, mode: 'read-only' });
  assert.equal(readLocalPolicy(f.root, '1.6.2').autoSync, false);
});
test('malformed or unknown policy fails closed without echoing its content', t => {
  const f = fixture(t);
  fs.writeFileSync(path.join(f.root, '.ai/codegraph.local.json'), 'PRIVATE_SENTINEL');
  assert.throws(() => readLocalPolicy(f.root, '1.6.2'), e => /policy/i.test(e.message) && !e.message.includes('PRIVATE_SENTINEL'));
  f.save({ ...f.policy, schemaVersion: 2 });
  assert.throws(() => readLocalPolicy(f.root, '1.6.2'), /schema/i);
  f.save({ ...f.policy, mode: 'rebuild' });
  assert.throws(() => readLocalPolicy(f.root, '1.6.2'), /mode/i);
});
test('debounce is bounded and cannot become a per-keystroke sync loop', t => {
  const f = fixture(t);
  for (const debounceMs of [0, -1, 60001, '2000']) {
    f.save({ ...f.policy, debounceMs });
    assert.throws(() => readLocalPolicy(f.root, '1.6.2'), /debounce/i);
  }
});

test('auto-sync MCP denies unlisted tools and paths outside the approved checkout', t => {
  const f = fixture(t);
  const call = { jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name: 'codegraph_explore', arguments: { query: 'Fixture' } } };
  assert.equal(restrictRequest(call, f.root).params.arguments.projectPath, fs.realpathSync(f.root));
  assert.throws(() => restrictRequest({ ...call, params: { name: 'codegraph_sync' } }, f.root), /codegraph_explore/);
  assert.throws(() => restrictRequest([call], f.root), /individual/);
  assert.throws(() => restrictRequest({ ...call, params: { ...call.params, arguments: { projectPath: os.tmpdir() } } }, f.root), /checkout/);
  const initialized = { jsonrpc: '2.0', id: 2, method: 'initialize', params: {} };
  assert.equal(restrictRequest(initialized, f.root), initialized);
});

test('a shared backend cannot advertise additional tools through this gateway', () => {
  const response = { jsonrpc: '2.0', id: 5, result: { tools: [{ name: 'codegraph_explore' }, { name: 'codegraph_status' }] } };
  assert.deepEqual(restrictToolList(response).result.tools, [{ name: 'codegraph_explore' }]);
  const source = { jsonrpc: '2.0', id: 6, result: { content: [{ type: 'text', text: 'source' }] } };
  assert.equal(restrictToolList(source), source);
});
