'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { run } = require('../shared-data-hook.cjs');

test('post-merge invokes local sync with argv array and inherits no credential arguments', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tct shared hook '));
  try {
    fs.mkdirSync(path.join(root,'data','shared'),{recursive:true});
    fs.writeFileSync(path.join(root,'data','shared','catalog.json'),'{}');
    const env={OPENAI_API_KEY:'test-key-only'};
    let count=0;
    assert.equal(run(root,env,(exe,args,opts)=>{
      count++; assert.equal(exe,'pwsh');
      assert.deepEqual(args,['-NoProfile','-File',path.join(root,'scripts','shared-data.ps1'),'-Action','Sync']);
      assert.equal(opts.env,env); assert.equal(opts.windowsHide,true); assert.equal(opts.shell,undefined);
      assert.ok(!args.join(' ').includes('test-key-only'));
      return {status:0};
    }),0);
    assert.equal(count,1);
    assert.equal(run(root,{TCT_SHARED_SYNC_IN_PROGRESS:'1'},()=>{throw Error('nested sync');}),0);
    assert.equal(run(root,{},()=>({status:2})),1);
    assert.equal(run(root,{},()=>({error:Error('missing pwsh')})),1);
  } finally { fs.rmSync(root,{recursive:true,force:true}); }
});

test('old checkout with no catalog does not start Docker',()=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'tct no catalog '));
  try { assert.equal(run(root,{},()=>{throw Error('must not start');}),0); }
  finally {fs.rmSync(root,{recursive:true,force:true});}
});
