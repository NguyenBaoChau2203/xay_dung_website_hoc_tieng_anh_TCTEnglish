'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const setup = require('../agent-tools.cjs');

test('a downloaded runtime with changed bytes is rejected before extraction', async () => {
  const good = Buffer.from('reviewed archive');
  const entry = { url: 'https://github.com/example/pin.zip', size: good.length,
    sha256: crypto.createHash('sha256').update(good).digest('hex') };
  const response = bytes => async () => ({ ok: true, arrayBuffer: async () => bytes });
  assert.deepEqual(await setup.downloadVerified(entry, response(good)), good);
  await assert.rejects(setup.downloadVerified(entry, response(Buffer.from('tampered archive'))), /integrity/i);
});

test('HTTP failure never becomes an extracted runtime', async () => {
  await assert.rejects(setup.downloadVerified({ url: 'https://github.com/example/pin.zip' },
    async () => ({ ok: false, status: 503 })), /503/);
});

test('concurrent client startups share one installation lock', async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct setup spaces '));
  let active = 0, maximum = 0;
  try {
    const install = () => setup.withLock(root, 'python', async () => {
      maximum = Math.max(maximum, ++active);
      await new Promise(resolve => setTimeout(resolve, 40));
      active--;
    }, { pollMs: 10, timeoutMs: 3000 });
    await Promise.all([install(), install(), install()]);
    assert.equal(maximum, 1);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('a failed installation releases its lock for a retry', async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct setup retry '));
  try {
    await assert.rejects(setup.withLock(root, 'python', async () => { throw new Error('offline'); }), /offline/);
    assert.equal(await setup.withLock(root, 'python', async () => 'retried'), 'retried');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('setup refuses to overwrite an existing hook manager', () => {
  const calls = [];
  const runner = (command, args) => {
    calls.push(args);
    return { status: 0, stdout: 'custom-hooks\n' };
  };
  assert.throws(() => setup.installGitHook(process.cwd(), runner), /existing hooksPath/i);
  assert.equal(calls.length, 1);
});

test('fast first-run guard denies delegation with the native host schema', () => {
  for (const [host, payload] of [
    ['claude', { tool_name: 'Agent' }],
    ['cursor', { subagent_id: 'example' }],
    ['antigravity', { toolCall: { name: 'invoke_subagent', args: {} } }],
  ]) {
    const result = setup.fastHook(host, 'PreToolUse', payload);
    if (host === 'claude') assert.equal(result.hookSpecificOutput.permissionDecision, 'deny');
    if (host === 'cursor') assert.equal(result.permission, 'deny');
    if (host === 'antigravity') assert.equal(result.decision, 'deny');
    assert.doesNotMatch(JSON.stringify(result), /auto_approve|updated_input|overwrite/);
  }
  assert.deepEqual(setup.fastHook('claude', 'PreToolUse', { tool_name: 'Read' }), {});
  assert.deepEqual(setup.fastHook('claude', 'PreToolUse', { tool_name: 'Agent' }, false), {});
});

test('repeated startup does not rewrite an already-enabled Git hook configuration', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct enabled hooks '));
  fs.mkdirSync(path.join(root, '.githooks'));
  for (const name of ['pre-commit', 'post-merge']) fs.writeFileSync(path.join(root, '.githooks', name), '#!/bin/sh\n');
  const calls = [];
  try {
    setup.installGitHook(root, (command, args) => {
      calls.push(args);
      return { status: 0, stdout: '.githooks\n' };
    });
    assert.equal(calls.length, 1);
    assert.deepEqual(calls[0], ['config', '--get', 'core.hooksPath']);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('a lock left by a terminated setup can be recovered', async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct setup interrupted '));
  const folder = path.join(root, 'tooling/.runtime/locks/python.lock');
  fs.mkdirSync(folder, { recursive: true });
  fs.writeFileSync(path.join(folder, 'owner.json'), JSON.stringify({ pid: 2147483647, token: 'old-owner' }));
  try {
    assert.equal(await setup.withLock(root, 'python', async () => 'recovered', { timeoutMs: 1000, pollMs: 10 }), 'recovered');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('an outside-workspace runtime junction is rejected before file mutation', async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct setup boundary '));
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'tct outside runtime '));
  fs.mkdirSync(path.join(root, 'tooling'));
  fs.symlinkSync(outside, path.join(root, 'tooling/.runtime'), process.platform === 'win32' ? 'junction' : 'dir');
  try {
    await assert.rejects(setup.withLock(root, 'python', async () => assert.fail('must not run')), /outside this checkout/);
    assert.deepEqual(fs.readdirSync(outside), []);
  } finally {
    fs.unlinkSync(path.join(root, 'tooling/.runtime'));
    fs.rmSync(root, { recursive: true, force: true });
    fs.rmSync(outside, { recursive: true, force: true });
  }
});
