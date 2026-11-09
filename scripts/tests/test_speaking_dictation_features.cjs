'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createDocument, createElement } = require('./dom_fixture.cjs');

test('renderDictationFeedback correctly marks correct characters in green and wrong in red', (t) => {
  // Load speaking.js code into evaluation sandbox
  const speakingCode = fs.readFileSync(path.resolve('TCTEnglish/wwwroot/js/speaking.js'), 'utf8');

  const { document, body } = createDocument([
    'dic-feedback',
    'dic-input',
    'btn-dic-hint'
  ]);

  const dicFeedback = document.getElementById('dic-feedback');
  assert.ok(dicFeedback, 'dic-feedback must exist');

  // Minimal window/global environment
  const mockGlobal = {
    document,
    window: null,
    SPK_VIDEO_ID: 'test-video',
    SPK_SENTENCES: [{ id: 1, start: 0, end: 5, text: 'Welcome to our class', isPracticed: false }],
    matchMedia: () => ({ matches: false, addListener: () => {}, removeListener: () => {} }),
    addEventListener: () => {},
    removeEventListener: () => {},
    setTimeout: (fn) => setTimeout(fn, 0),
    clearTimeout: () => {},
    setInterval: () => 1,
    clearInterval: () => {},
  };
  mockGlobal.window = mockGlobal;

  const fn = new Function('global', 'window', 'document', speakingCode);
  fn(mockGlobal, mockGlobal, document);

  assert.ok(typeof mockGlobal.renderDictationFeedback === 'function', 'renderDictationFeedback must be exported to global');

  // Case 1: Untyped -> all asterisks
  mockGlobal.renderDictationFeedback('', 'Welcome to class');
  assert.match(dicFeedback.innerHTML, /char-untyped spk-dic-char--untyped/);
  assert.match(dicFeedback.innerHTML, /\*/);
  assert.doesNotMatch(dicFeedback.innerHTML, /char-correct/);
  assert.doesNotMatch(dicFeedback.innerHTML, /char-incorrect/);

  // Case 2: Partially correct ("Welc" for "Welcome")
  mockGlobal.renderDictationFeedback('Welc', 'Welcome to class');
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">W<\/span>/);
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">e<\/span>/);
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">l<\/span>/);
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">c<\/span>/);
  // Untyped remaining letters
  assert.match(dicFeedback.innerHTML, /char-untyped spk-dic-char--untyped">\*<\/span>/);

  // Case 3: Incorrect character ("Welx" for "Welcome")
  mockGlobal.renderDictationFeedback('Welx', 'Welcome to class');
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">W<\/span>/);
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">e<\/span>/);
  assert.match(dicFeedback.innerHTML, /char-correct spk-dic-char--correct">l<\/span>/);
  // 'x' is wrong -> must have char-incorrect and spk-dic-char--wrong
  assert.match(dicFeedback.innerHTML, /char-incorrect spk-dic-char--wrong">x<\/span>/);
});

test('speaking.css defines green for correct chars, red for wrong chars, and 2-part layout', (t) => {
  const css = fs.readFileSync(path.resolve('TCTEnglish/wwwroot/css/speaking.css'), 'utf8');

  // Color rules
  assert.match(css, /\.char-correct[\s\S]*?color:\s*#16a34a/i, 'char-correct must have green color #16a34a');
  assert.match(css, /\.spk-dic-char--correct[\s\S]*?color:\s*#16a34a/i, 'spk-dic-char--correct must have green color #16a34a');
  assert.match(css, /\.char-incorrect[\s\S]*?color:\s*#dc2626/i, 'char-incorrect must have red color #dc2626');
  assert.match(css, /\.spk-dic-char--wrong[\s\S]*?color:\s*#dc2626/i, 'spk-dic-char--wrong must have red color #dc2626');
  assert.match(css, /\.char-untyped[\s\S]*?color:\s*#94a3b8/i, 'char-untyped must have muted slate color #94a3b8');

  // 2-part layout rules
  assert.match(css, /\.spk-practice-wrapper\.spk-mode--dictation \.spk-practice-body/, 'spk-mode--dictation modifier for body layout exists');
  assert.match(css, /\.spk-practice-wrapper\.spk-mode--dictation \.spk-transcript-panel[\s\S]*?display:\s*none/i, 'transcript hidden in dictation mode');
  assert.match(css, /"spk-video\s+spk-dictation"/i, 'dictation mode configures side-by-side video and dictation grid areas');
  assert.match(css, /\.spk-practice-wrapper\.spk-mode--dictation\s+\.spk-player-wrap\s*\{[\s\S]*?grid-area:\s*spk-video/i, 'video player mapped to left spk-video area');
  assert.match(css, /\.spk-practice-wrapper\.spk-mode--dictation\s+#dictation-content\s*\{[\s\S]*?grid-area:\s*spk-dictation/i, 'dictation pane mapped to right spk-dictation area');
});

test('Practice.cshtml layout places video player on top and dictation card below', (t) => {
  const html = fs.readFileSync(path.resolve('TCTEnglish/Views/Speaking/Practice.cshtml'), 'utf8');

  const playerPos = html.indexOf('id="youtube-player"');
  const pronPos = html.indexOf('id="pronunciation-content"');
  const dicPos = html.indexOf('id="dictation-content"');
  const dicInputPos = html.indexOf('id="dic-input"');
  const dicFeedbackPos = html.indexOf('id="dic-feedback"');

  assert.ok(playerPos !== -1, 'youtube-player must exist');
  assert.ok(pronPos !== -1, 'pronunciation-content must exist');
  assert.ok(dicPos !== -1, 'dictation-content must exist');
  assert.ok(dicInputPos !== -1, 'dic-input must exist');
  assert.ok(dicFeedbackPos !== -1, 'dic-feedback must exist');

  // Video player is on TOP (precedes both pronunciation and dictation panes)
  assert.ok(playerPos < pronPos, 'youtube-player must appear before pronunciation-content in DOM');
  assert.ok(playerPos < dicPos, 'youtube-player must appear before dictation-content in DOM (video on top)');
  assert.ok(dicPos < dicInputPos, 'dictation-content must contain dic-input (writing below)');
  assert.ok(dicInputPos < dicFeedbackPos, 'dic-input must precede dic-feedback');
});

test('tab switcher has animated glider and high-contrast active indicator in Practice.cshtml & speaking.css', (t) => {
  const html = fs.readFileSync(path.resolve('TCTEnglish/Views/Speaking/Practice.cshtml'), 'utf8');
  const css = fs.readFileSync(path.resolve('TCTEnglish/wwwroot/css/speaking.css'), 'utf8');

  // HTML glider exists
  assert.match(html, /class="[^"]*spk-tabs-glider[^"]*"/, 'Practice.cshtml must contain spk-tabs-glider');
  assert.match(html, /id="spk-practice-tabs"/, 'Practice.cshtml must contain id=spk-practice-tabs');

  // CSS high-contrast active styling
  assert.match(css, /\.spk-tabs-glider[\s\S]*?background:\s*linear-gradient/i, 'glider must have vivid gradient');
  assert.match(css, /\.spk-practice-tabs\.spk-tabs--dictation-active \.spk-tabs-glider[\s\S]*?transform:\s*translateX\(100%\)/i, 'glider slides on dictation active');
  assert.match(css, /\.spk-practice-tab\.active[\s\S]*?color:\s*#ffffff !important/i, 'active tab must have high-contrast crisp white text');

  // Motion enter keyframes
  assert.match(css, /@keyframes spk-mode-enter/, 'speaking.css must define @keyframes spk-mode-enter');
  assert.match(css, /\.spk-mode-pane\.d-flex[\s\S]*?animation:\s*spk-mode-enter/i, 'pane entry must trigger spk-mode-enter animation');
});

test('focus ring across all inputs uses rounded box-shadow halo without broken rectangular outline', (t) => {
  const speakingCss = fs.readFileSync(path.resolve('TCTEnglish/wwwroot/css/speaking.css'), 'utf8');
  const adminCss = fs.readFileSync(path.resolve('TCTEnglish/wwwroot/css/admin-speaking-video.css'), 'utf8');

  // Hero url wrap
  assert.match(speakingCss, /\.spk-hero-url-wrap:focus-within[\s\S]*?box-shadow:\s*0 0 0 3\.5px/i);
  assert.match(speakingCss, /\.spk-hero-url-wrap:focus-within[\s\S]*?outline:\s*none !important/i);
  assert.match(speakingCss, /\.spk-hero-url-input[\s\S]*?outline:\s*none !important/i);

  // Search wrap
  assert.match(speakingCss, /\.spk-search-wrap:focus-within[\s\S]*?box-shadow:\s*0 0 0 3\.5px/i);
  assert.match(speakingCss, /\.spk-search-wrap:focus-within[\s\S]*?outline:\s*none !important/i);

  // Dictation textarea
  assert.match(speakingCss, /\.spk-dic-textarea:focus[\s\S]*?box-shadow:\s*0 0 0 3\.5px/i);
  assert.match(speakingCss, /\.spk-dic-textarea:focus[\s\S]*?outline:\s*none !important/i);

  // Admin form controls
  assert.match(adminCss, /\.admin-speaking-page \.form-control:focus[\s\S]*?box-shadow:\s*0 0 0 3\.5px/i);
  assert.match(adminCss, /\.admin-speaking-page \.form-control:focus[\s\S]*?outline:\s*none !important/i);
});
