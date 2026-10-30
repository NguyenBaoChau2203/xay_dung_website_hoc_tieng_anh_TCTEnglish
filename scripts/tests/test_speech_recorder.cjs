'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function loadRecorder() {
  const code = fs.readFileSync(
    path.join(__dirname, '../../TCTEnglish/wwwroot/js/speech-recorder.js'),
    'utf8');
  const sandbox = { window: {}, console };
  sandbox.window = sandbox;
  sandbox.globalThis = sandbox;
  vm.runInNewContext(code, sandbox, { filename: 'speech-recorder.js' });
  return sandbox.TctSpeechRecorder;
}

test('speech-recorder exports support helper and maxSeconds', () => {
  const api = loadRecorder();
  assert.equal(typeof api.isSupported, 'function');
  assert.equal(api.maxSeconds, 15);
  assert.equal(api.maxSamples, 16000 * 15);
  assert.equal(api.isSupported(), false);
});

test('auto-stop with pcm samples resolves one wav and a second stop returns the same blob', async () => {
  const completed = [];
  let processor = null;
  const sandbox = {
    console,
    Blob: globalThis.Blob,
    DataView: globalThis.DataView,
    ArrayBuffer: globalThis.ArrayBuffer,
    Float32Array: globalThis.Float32Array,
    Uint8Array: globalThis.Uint8Array,
    setTimeout: (fn) => {
      sandbox._timers.push(fn);
      return sandbox._timers.length;
    },
    clearTimeout() {},
    AudioContext: class {
      constructor() {
        this.sampleRate = 44100;
        sandbox.lastContext = this;
      }
      createMediaStreamSource() { return { connect() {}, disconnect() {} }; }
      createScriptProcessor() {
        processor = { connect() {}, disconnect() {}, onaudioprocess: null };
        return processor;
      }
      close() { return Promise.resolve(); }
    },
    navigator: {
      mediaDevices: {
        getUserMedia: async () => ({ getTracks: () => [{ stop() {} }] })
      }
    },
    _timers: []
  };
  sandbox.window = sandbox;
  sandbox.globalThis = sandbox;
  const code = require('node:fs').readFileSync(
    require('node:path').join(__dirname, '../../TCTEnglish/wwwroot/js/speech-recorder.js'),
    'utf8');
  require('node:vm').runInNewContext(code, sandbox, { filename: 'speech-recorder.js' });
  const recorder = sandbox.TctSpeechRecorder.create({ onComplete: (blob) => completed.push(blob) });
  await recorder.start();
  const samples = new Float32Array(1600);
  samples[0] = 0.25;
  processor.onaudioprocess({ inputBuffer: { getChannelData: () => samples } });
  sandbox._timers[0]();
  const first = await recorder.stop();
  const second = await recorder.stop();
  assert.ok(first instanceof sandbox.Blob);
  assert.ok(first.size >= 44);
  assert.equal(second, first);
  assert.equal(completed.length, 1);
  assert.equal(completed[0], first);
});
