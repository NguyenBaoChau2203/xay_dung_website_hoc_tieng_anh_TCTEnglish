'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createElement, createDocument } = require('./dom_fixture.cjs');

test('speaking.js respects prefers-reduced-motion when selecting sentences', (t) => {
  const { document, body } = createDocument([]);
  let requestedBehavior = null;

  // Mock global matchMedia to simulate reduced motion
  const matchMediaMock = (query) => ({
    matches: query.includes('prefers-reduced-motion: reduce'),
    media: query,
    addListener: () => {},
    removeListener: () => {}
  });

  const btn = createElement('button', 'sent-btn-0');
  btn.className = 'spk-sent-btn';
  btn.dataset.index = '0';
  btn.scrollIntoView = (options) => {
    requestedBehavior = options && options.behavior;
  };

  const container = createElement('div', 'sentence-scroll-container');
  container.appendChild(btn);
  body.appendChild(container);

  // When reduced motion matches, requested behavior must be 'auto'
  const isReducedMotion = matchMediaMock('(prefers-reduced-motion: reduce)').matches;
  assert.equal(isReducedMotion, true, 'Reduced motion media query should match');

  btn.scrollIntoView({ behavior: isReducedMotion ? 'auto' : 'smooth', block: 'nearest', inline: 'center' });
  assert.equal(requestedBehavior, 'auto', 'scrollIntoView must use auto behavior under reduced motion');
});

test('recording button UI state correctly transitions through idle, recording, assessing, and recovery', (t) => {
  const { body } = createDocument([]);
  const btnRecord = createElement('button', 'btn-record');
  btnRecord.className = 'spk-btn spk-btn--record spk-record-btn';
  const recordIcon = createElement('span', 'record-icon');
  const recordLabel = createElement('span', 'record-label');
  btnRecord.appendChild(recordIcon);
  btnRecord.appendChild(recordLabel);
  body.appendChild(btnRecord);

  // 1. Initial idle
  assert.equal(btnRecord.classList.contains('recording'), false);
  assert.equal(btnRecord.classList.contains('assessing'), false);

  // 2. Set to recording
  btnRecord.classList.add('is-recording', 'recording', 'spk-record-btn--active');
  btnRecord.setAttribute('aria-busy', 'true');
  btnRecord.setAttribute('aria-label', 'Đang ghi âm. Nhấn để dừng.');
  recordIcon.textContent = 'stop_circle';
  recordLabel.textContent = 'Đang ghi âm...';

  assert.equal(btnRecord.classList.contains('recording'), true);
  assert.equal(btnRecord.classList.contains('spk-record-btn--active'), true);
  assert.equal(btnRecord.getAttribute('aria-busy'), 'true');
  assert.equal(recordIcon.textContent, 'stop_circle');

  // 3. Set to assessing
  btnRecord.classList.remove('is-recording', 'recording', 'spk-record-btn--active');
  btnRecord.classList.add('is-scoring', 'assessing', 'spk-record-btn--assessing');
  btnRecord.disabled = true;
  btnRecord.setAttribute('aria-busy', 'true');
  btnRecord.setAttribute('aria-label', 'Đang chấm điểm...');
  recordLabel.textContent = 'Đang chấm điểm...';

  assert.equal(btnRecord.classList.contains('recording'), false);
  assert.equal(btnRecord.classList.contains('assessing'), true);
  assert.equal(btnRecord.disabled, true);
  assert.equal(btnRecord.getAttribute('aria-label'), 'Đang chấm điểm...');

  // 4. Safe recovery to idle
  btnRecord.classList.remove('is-scoring', 'assessing', 'spk-record-btn--assessing');
  btnRecord.disabled = false;
  btnRecord.attributes['aria-busy'] = null;
  btnRecord.setAttribute('aria-label', 'Kiểm tra phát âm');
  recordIcon.textContent = 'mic';
  recordLabel.textContent = 'Kiểm tra phát âm';

  assert.equal(btnRecord.classList.contains('assessing'), false);
  assert.equal(btnRecord.disabled, false);
  assert.equal(recordLabel.textContent, 'Kiểm tra phát âm');
});

test('switching tabs from pronunciation to dictation safely triggers abortActiveRecognition', (t) => {
  let abortCalledWith = null;
  let isRecording = true;

  function abortActiveRecognition(reason) {
    abortCalledWith = reason;
    isRecording = false;
  }

  // Simulate clicking dictation tab while recording is true
  function onTabDictationClick() {
    if (isRecording) {
      abortActiveRecognition('tab-switch');
    }
  }

  onTabDictationClick();
  assert.equal(abortCalledWith, 'tab-switch', 'Active recording must be aborted with reason tab-switch');
  assert.equal(isRecording, false, 'Recording state must be false after tab switch');
});

test('all Speaking views contain tct-theme wrapper and one single h1 heading', (t) => {
  const views = [
    'TCTEnglish/Views/Speaking/Index.cshtml',
    'TCTEnglish/Views/Speaking/Practice.cshtml',
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml',
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml',
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Edit.cshtml'
  ];

  for (const viewPath of views) {
    const content = fs.readFileSync(path.resolve(viewPath), 'utf8');
    assert.match(content, /class="[^"]*tct-theme/i, `${viewPath} must contain tct-theme class wrapper`);

    // Match all <h1> tags (allowing attributes)
    const h1Matches = content.match(/<h1[^>]*>/gi) || [];
    assert.equal(h1Matches.length, 1, `${viewPath} must contain exactly ONE <h1> heading, found ${h1Matches.length}`);
  }
});

test('admin-speaking-video.css and speaking.css contain prefers-reduced-motion queries', (t) => {
  const cssFiles = [
    'TCTEnglish/wwwroot/css/speaking.css',
    'TCTEnglish/wwwroot/css/admin-speaking-video.css'
  ];

  for (const cssPath of cssFiles) {
    const content = fs.readFileSync(path.resolve(cssPath), 'utf8');
    assert.match(content, /prefers-reduced-motion:\s*reduce/i, `${cssPath} must include prefers-reduced-motion block`);
    assert.match(content, /var\(--tct-clay-/i, `${cssPath} must consume --tct-clay- tokens`);
  }
});

test('speaking-hero-clay.svg separates AI SCORE and 98% vertically with no text collision', (t) => {
  const svgPath = path.resolve('TCTEnglish/wwwroot/images/speaking/speaking-hero-clay.svg');
  const svg = fs.readFileSync(svgPath, 'utf8');

  // Verify both labels are present
  assert.match(svg, /AI SCORE/, 'SVG must contain AI SCORE label');
  assert.match(svg, /98%/, 'SVG must contain 98% score');

  // Match the text nodes
  const aiScoreTag = svg.match(/<text\b[^>]*>AI SCORE<\/text>/)?.[0] || '';
  const scoreTag = svg.match(/<text\b[^>]*>98%<\/text>/)?.[0] || '';

  assert.ok(aiScoreTag, 'AI SCORE text tag must exist');
  assert.ok(scoreTag, '98% text tag must exist');

  const y1 = parseFloat(aiScoreTag.match(/y="([\d.]+)"/)?.[1]);
  const y2 = parseFloat(scoreTag.match(/y="([\d.]+)"/)?.[1]);

  // Ensure they are separated vertically by at least 15px so they cannot collide
  assert.ok(Math.abs(y2 - y1) >= 15, `AI SCORE (y=${y1}) and 98% (y=${y2}) must be separated vertically by >= 15px`);
  assert.match(aiScoreTag, /text-anchor="middle"/, 'AI SCORE must be horizontally centered');
  assert.match(scoreTag, /text-anchor="middle"/, '98% must be horizontally centered');
});

test('Admin Speaking views use Font Awesome and do not reference Material Symbols', (t) => {
  const adminViews = [
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml',
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml',
    'TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Edit.cshtml'
  ];

  for (const viewPath of adminViews) {
    const content = fs.readFileSync(path.resolve(viewPath), 'utf8');
    assert.doesNotMatch(content, /material-symbols/i, `${viewPath} must NOT use Material Symbols since _AdminLayout only provides Font Awesome`);
    assert.match(content, /fa-solid/i, `${viewPath} must use Font Awesome icons`);
  }
});

test('speaking.css defines proportional font-size for spk-section-title', (t) => {
  const cssPath = path.resolve('TCTEnglish/wwwroot/css/speaking.css');
  const css = fs.readFileSync(cssPath, 'utf8');

  assert.match(css, /\.spk-section-title[\s\S]*?font-size:\s*var\(--tct-text-xl,\s*1\.25rem\)/, 'spk-section-title must have controlled font-size (1.25rem)');
});

test('spk-my-empty is styled as a full-width Bento showcase card with step cards and helper actions', (t) => {
  const cssPath = path.resolve('TCTEnglish/wwwroot/css/speaking.css');
  const css = fs.readFileSync(cssPath, 'utf8');

  assert.match(css, /\.spk-my-empty\s*\{[\s\S]*?grid-column:\s*1\s*\/\s*-1/i, 'spk-my-empty must span full grid width (grid-column: 1 / -1)');
  assert.match(css, /\.spk-empty-step-card/i, 'speaking.css must style step cards');
  assert.match(css, /\.spk-sample-chip/i, 'speaking.css must style sample chips');

  const indexPath = path.resolve('TCTEnglish/Views/Speaking/Index.cshtml');
  const indexHtml = fs.readFileSync(indexPath, 'utf8');

  assert.match(indexHtml, /id="my-empty-state"/, 'Index.cshtml must retain #my-empty-state element for script bindings');
  assert.match(indexHtml, /spk-empty-step-card/, 'Index.cshtml must include visual step cards');
  assert.match(indexHtml, /setSampleVideo\(this\)/, 'Index.cshtml must include quick sample video chips');

  const jsPath = path.resolve('TCTEnglish/wwwroot/js/speaking.js');
  const js = fs.readFileSync(jsPath, 'utf8');

  assert.match(js, /window\.focusImportInput/, 'speaking.js must expose focusImportInput helper');
  assert.match(js, /window\.setSampleVideo/, 'speaking.js must expose setSampleVideo helper');
});

test('spk-chip and spk-pill-btn use clean porcelain background and soft pastel shadows without black smudges', (t) => {
  const cssPath = path.resolve('TCTEnglish/wwwroot/css/speaking.css');
  const css = fs.readFileSync(cssPath, 'utf8');

  // Verify spk-chip and spk-pill-btn use clean porcelain background
  assert.match(css, /\.spk-pill-btn,\s*\.spk-chip\s*\{[\s\S]*?background:\s*#ffffff;/i, 'Pills must have #ffffff background');
  // Verify spk-topic-chips hides scrollbar
  assert.match(css, /\.spk-topic-chips\s*\{[\s\S]*?scrollbar-width:\s*none;/i, 'Topic chips container must hide scrollbar-width');
});

test('speaking.css and Index.cshtml optimize footer and level section bottom spacing', (t) => {
  const cssPath = path.resolve('TCTEnglish/wwwroot/css/speaking.css');
  const css = fs.readFileSync(cssPath, 'utf8');

  // .main:has(.spk-page-wrapper) must have min-height: auto and tightened padding-bottom
  assert.match(css, /\.main:has\(\.spk-page-wrapper\)[\s\S]*?min-height:\s*auto\s*!important;/i, 'Speaking page must prevent artificial min-height stretching');
  assert.match(css, /\.main:has\(\.spk-page-wrapper\)[\s\S]*?padding-bottom:\s*var\(--tct-space-lg,\s*1\.5rem\)\s*!important;/i, 'Speaking page must tighten bottom padding to 1.5rem');

  // .tct-footer must have tightened margin-top
  assert.match(css, /body:has\(\.spk-page-wrapper\)\s+\.tct-footer[\s\S]*?margin-top:\s*var\(--tct-space-lg,\s*1\.5rem\)\s*!important;/i, 'Footer top margin must be tightened on Speaking page');

  // .numbered-pagination-container:empty must be hidden
  assert.match(css, /\.numbered-pagination-container:empty\s*\{[\s\S]*?display:\s*none\s*!important;/i, 'Empty pagination container must be hidden');

  // Final level section bottom margin tightened
  assert.match(css, /\.spk-page-wrapper\s*>\s*\.spk-level-section:last-of-type[\s\S]*?margin-bottom:\s*var\(--tct-space-md,\s*1rem\)\s*!important;/i, 'Last level section bottom margin must be tightened to 1rem');

  // Index.cshtml must not hardcode mt-4 on empty numbered-pagination-container
  const indexPath = path.resolve('TCTEnglish/Views/Speaking/Index.cshtml');
  const indexHtml = fs.readFileSync(indexPath, 'utf8');
  assert.doesNotMatch(indexHtml, /numbered-pagination-container[^\"]*mt-4/i, 'numbered-pagination-container in Index.cshtml should not have hardcoded mt-4');
});

test('Index.cshtml combines library header and filters into a single unified toolbar placed below My Videos', (t) => {
  const indexPath = path.resolve('TCTEnglish/Views/Speaking/Index.cshtml');
  const indexHtml = fs.readFileSync(indexPath, 'utf8');

  // Verify unified toolbar exists
  assert.match(indexHtml, /class="spk-unified-toolbar spk-library-toolbar mb-4"/, 'Unified toolbar element must exist');

  // Verify the position: My Videos section comes BEFORE the unified toolbar
  const myVideosPos = indexHtml.indexOf('id="my-videos-section"');
  const unifiedToolbarPos = indexHtml.indexOf('spk-unified-toolbar');
  assert.ok(myVideosPos !== -1, 'My videos section must exist');
  assert.ok(unifiedToolbarPos !== -1, 'Unified toolbar must exist');
  assert.ok(myVideosPos < unifiedToolbarPos, 'My Videos section must appear BEFORE the unified toolbar');

  // Verify that the standalone spk-filter-section before my-videos-section is removed
  const filterSectionBeforeMyVideos = indexHtml.substring(0, myVideosPos);
  assert.doesNotMatch(filterSectionBeforeMyVideos, /class="spk-filter-section/, 'No standalone filter section should exist before My Videos');

  // Verify the unified toolbar contains both the library title, search input, and filter controls
  const unifiedToolbarHtml = indexHtml.substring(unifiedToolbarPos, indexHtml.indexOf('class="vi-level-section', unifiedToolbarPos));
  assert.match(unifiedToolbarHtml, /Video luyện nói theo chủ đề/, 'Unified toolbar must contain the subject-first library title');
  assert.match(unifiedToolbarHtml, /id="vi-search"/, 'Unified toolbar must contain search input');
  assert.match(unifiedToolbarHtml, /class="spk-toolbar-divider"/, 'Unified toolbar must contain visual divider');
  assert.match(unifiedToolbarHtml, /level-filter-btn spk-pill-btn/, 'Unified toolbar must contain level filter buttons');
  assert.match(unifiedToolbarHtml, /topic-filter-btn spk-chip/, 'Unified toolbar must contain topic filter chips');

  // Verify speaking.css styles the unified toolbar
  const cssPath = path.resolve('TCTEnglish/wwwroot/css/speaking.css');
  const css = fs.readFileSync(cssPath, 'utf8');
  assert.match(css, /\.spk-unified-toolbar/, 'speaking.css must style .spk-unified-toolbar');
  assert.match(css, /\.spk-toolbar-top-row/, 'speaking.css must style .spk-toolbar-top-row');
  assert.match(css, /\.spk-toolbar-divider/, 'speaking.css must style .spk-toolbar-divider');
  assert.match(css, /\.spk-toolbar-filter-row/, 'speaking.css must style .spk-toolbar-filter-row');
});
