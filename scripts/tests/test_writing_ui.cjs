'use strict';

/**
 * Behavior and contract tests for Writing UI interactions (writing-index.js, writing-exercises.js)
 * and design system compliance (Claymorphism, SVG assets, no private root palettes).
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

const ROOT_DIR = path.resolve(__dirname, '../..');
const WRITING_INDEX_JS = path.join(ROOT_DIR, 'TCTEnglish/wwwroot/js/writing-index.js');
const WRITING_EXERCISES_JS = path.join(ROOT_DIR, 'TCTEnglish/wwwroot/js/writing-exercises.js');
const ASSETS_DIR = path.join(ROOT_DIR, 'TCTEnglish/wwwroot/images/writing');

// ─── 1. Writing Index UI Tests ─────────────────────────────────────────

test('writing-index.js: switching level card updates class, aria-current, copy, and link hrefs', () => {
  const { document, body } = createDocument([]);

  // Create Level Cards
  const cardBeginner = createElement('a');
  cardBeginner.setAttribute('data-writing-level-card', '');
  cardBeginner.setAttribute('data-level-key', 'beginner');
  cardBeginner.setAttribute('data-level-title', 'Cơ bản');
  cardBeginner.setAttribute('href', '/Home/Writing?level=beginner');
  cardBeginner.classList.add('is-selected');
  cardBeginner.setAttribute('aria-current', 'true');
  body.appendChild(cardBeginner);

  const cardAdvanced = createElement('a');
  cardAdvanced.setAttribute('data-writing-level-card', '');
  cardAdvanced.setAttribute('data-level-key', 'advanced');
  cardAdvanced.setAttribute('data-level-title', 'Nâng cao');
  cardAdvanced.setAttribute('href', '/Home/Writing?level=advanced');
  cardAdvanced.setAttribute('aria-current', 'false');
  body.appendChild(cardAdvanced);

  // Copy element
  const copyEl = createElement('p');
  copyEl.setAttribute('data-writing-selected-level-copy', '');
  copyEl.textContent = 'Chế độ Cơ bản đã sẵn sàng.';
  body.appendChild(copyEl);

  // Content links
  const contentLink = createElement('a');
  contentLink.setAttribute('data-writing-content-link', '');
  contentLink.setAttribute('href', '/Home/Writing/Exercises?level=beginner&contentType=emails');
  body.appendChild(contentLink);

  let replacedState = null;
  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    history: {
      replaceState(state, title, url) {
        replacedState = { state, title, url };
      }
    },
    URL: globalThis.URL,
    console
  };
  windowObj.window = windowObj;

  loadScript(WRITING_INDEX_JS, {
    window: windowObj,
    document,
    URL: globalThis.URL,
    console
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  // Simulate click on Advanced card
  const listeners = cardAdvanced._listeners && cardAdvanced._listeners.click;
  assert.ok(listeners && listeners.length > 0, 'Click listener must be registered on level card');
  let prevented = false;
  for (const fn of listeners) {
    fn({
      preventDefault() { prevented = true; }
    });
  }

  assert.ok(prevented, 'Default navigation should be prevented on AJAX level switch');
  assert.ok(cardAdvanced.classList.contains('is-selected'), 'Advanced card must have is-selected class');
  assert.ok(!cardBeginner.classList.contains('is-selected'), 'Beginner card must lose is-selected class');
  assert.equal(cardAdvanced.getAttribute('aria-current'), 'true');
  assert.equal(cardBeginner.getAttribute('aria-current'), 'false');
  assert.match(copyEl.textContent, /Nâng cao/, 'Copy should be updated to mention Nâng cao');
  assert.match(contentLink.getAttribute('href'), /level=advanced/, 'Content link href must include level=advanced');
  assert.ok(replacedState, 'history.replaceState must be called');
  assert.equal(replacedState.state.writingLevel, 'advanced');
});

// ─── 2. Writing Exercises Catalog UI Tests ─────────────────────────────

test('writing-exercises.js: filter selects trigger form submit', () => {
  const { document, body } = createDocument([]);

  let submitted = false;
  const form = createElement('form');
  form.setAttribute('data-writing-filter-form', '');
  form.submit = () => { submitted = true; };
  body.appendChild(form);

  const select = createElement('select');
  select.setAttribute('data-filter-select', '');
  form.appendChild(select);

  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    console
  };
  windowObj.window = windowObj;

  loadScript(WRITING_EXERCISES_JS, {
    window: windowObj,
    document,
    console
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  const changeListeners = select._listeners && select._listeners.change;
  assert.ok(changeListeners && changeListeners.length > 0, 'Change listener must be registered on filter select');
  for (const fn of changeListeners) {
    fn({ type: 'change', target: select });
  }

  assert.ok(submitted, 'Form must submit on select change');
});

test('writing-exercises.js: delete form triggers window.confirm', () => {
  const { document, body } = createDocument([]);

  const form = createElement('form');
  form.setAttribute('data-writing-delete-form', '');
  body.appendChild(form);

  let confirmCalled = false;
  let confirmPrompt = '';
  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    confirm(msg) {
      confirmCalled = true;
      confirmPrompt = msg;
      return false; // simulate user cancel
    },
    console
  };
  windowObj.window = windowObj;

  loadScript(WRITING_EXERCISES_JS, {
    window: windowObj,
    document,
    confirm: windowObj.confirm,
    console
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  const submitListeners = form._listeners && form._listeners.submit;
  assert.ok(submitListeners && submitListeners.length > 0, 'Submit listener must be registered on delete form');
  let prevented = false;
  for (const fn of submitListeners) {
    fn({
      type: 'submit',
      preventDefault() { prevented = true; }
    });
  }

  assert.ok(confirmCalled, 'window.confirm must be prompted');
  assert.match(confirmPrompt, /Bạn có chắc muốn xóa/i);
  assert.ok(prevented, 'Submit must be prevented when user cancels');
});

test('writing-exercises.js: AI create form validates empty input', async () => {
  const { document, body } = createDocument([]);

  const form = createElement('form');
  form.setAttribute('data-writing-create-form', '');
  form.setAttribute('data-create-url', '/Home/Writing/Exercises/CreateFromAi');
  form.setAttribute('data-practice-url', '/Home/Writing/Practice');

  const csrf = createElement('input');
  csrf.setAttribute('name', '__RequestVerificationToken');
  csrf.value = 'token-123';
  form.appendChild(csrf);

  const textarea = createElement('textarea');
  textarea.setAttribute('name', 'sourceText');
  textarea.value = '   ';
  let focused = false;
  textarea.focus = () => { focused = true; };
  form.appendChild(textarea);

  const btn = createElement('button');
  btn.setAttribute('data-writing-create-submit', '');
  form.appendChild(btn);

  const btnText = createElement('span');
  btnText.setAttribute('data-writing-create-submit-text', '');
  btnText.textContent = 'Tạo bài bằng AI';
  btn.appendChild(btnText);

  const msg = createElement('p');
  msg.setAttribute('data-writing-create-message', '');
  msg.hidden = true;
  form.appendChild(msg);

  body.appendChild(form);

  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    console
  };
  windowObj.window = windowObj;

  loadScript(WRITING_EXERCISES_JS, {
    window: windowObj,
    document,
    console
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  const submitListeners = form._listeners && form._listeners.submit;
  assert.ok(submitListeners && submitListeners.length > 0);
  for (const fn of submitListeners) {
    await fn({
      type: 'submit',
      preventDefault() {}
    });
  }

  assert.equal(msg.hidden, false, 'Message box should be shown on error');
  assert.ok(msg.classList.contains('is-error'), 'Message box should have is-error class');
  assert.match(msg.textContent, /Vui lòng dán bài viết/i);
  assert.ok(focused, 'Textarea should be focused');
});

// ─── 3. Design System & Asset Compliance ────────────────────────────────

test('SVG assets: all 4 purposeful Clay SVGs exist, under 10KB, clean XML, no scripts', () => {
  const files = [
    'writing-banner-clay.svg',
    'writing-quill-clay.svg',
    'writing-ai-sparkle-clay.svg',
    'writing-empty-search-clay.svg'
  ];

  for (const file of files) {
    const filePath = path.join(ASSETS_DIR, file);
    assert.ok(fs.existsSync(filePath), `Asset ${file} must exist`);
    const content = fs.readFileSync(filePath, 'utf8');
    const stat = fs.statSync(filePath);

    assert.ok(stat.size < 10240, `Asset ${file} should be reasonably sized (<10KB), actual: ${stat.size}B`);
    assert.match(content, /^<svg/i, `Asset ${file} must start with <svg`);
    assert.match(content, /<\/svg>$/i, `Asset ${file} must end with </svg>`);
    assert.doesNotMatch(content, /<script/i, `Asset ${file} must not contain script tags`);
    assert.doesNotMatch(content, /javascript:/i, `Asset ${file} must not contain javascript: URLs`);
  }
});

test('CSS palettes: no private root palettes (--wi-*, --we-*, --w-*) remain in scoped files', () => {
  const cssFiles = [
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing-index.css'),
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing-exercises.css'),
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing.css'),
  ];

  for (const file of cssFiles) {
    const css = fs.readFileSync(file, 'utf8');
    assert.doesNotMatch(css, /:root\s*\{[^}]*--wi-[a-z]/i, `${file} must not define --wi-* in :root`);
    assert.doesNotMatch(css, /:root\s*\{[^}]*--we-[a-z]/i, `${file} must not define --we-* in :root`);
    assert.doesNotMatch(css, /:root\s*\{[^}]*--w-[a-z]/i, `${file} must not define --w-* in :root`);
    assert.doesNotMatch(css, /font-family:\s*['"]?Be Vietnam Pro['"]?/i, `${file} must not force Be Vietnam Pro`);
  }
});

test('Razor views: all 6 main views and partials wrap in or use tct-theme', () => {
  const views = [
    path.join(ROOT_DIR, 'TCTEnglish/Views/Study/Writing.cshtml'),
    path.join(ROOT_DIR, 'TCTEnglish/Views/Study/WritingExercises.cshtml'),
    path.join(ROOT_DIR, 'TCTEnglish/Views/Study/WritingPractice.cshtml'),
    path.join(ROOT_DIR, 'TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml'),
    path.join(ROOT_DIR, 'TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Create.cshtml'),
    path.join(ROOT_DIR, 'TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml'),
  ];

  for (const view of views) {
    const content = fs.readFileSync(view, 'utf8');
    assert.match(content, /tct-theme/, `${path.basename(view)} must include tct-theme class`);
    const h1Count = (content.match(/<h1[\s>]/gi) || []).length;
    assert.equal(h1Count, 1, `${path.basename(view)} must have exactly one <h1>`);
  }
});

test('Motion & Micro-interactions: all scoped writing CSS files define keyframes and respect prefers-reduced-motion', () => {
  const cssFiles = [
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing-index.css'),
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing-exercises.css'),
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/writing.css'),
    path.join(ROOT_DIR, 'TCTEnglish/wwwroot/css/admin-writing-exercise.css'),
  ];

  for (const file of cssFiles) {
    const css = fs.readFileSync(file, 'utf8');
    assert.match(css, /@keyframes\s+[\w-]+/i, `${path.basename(file)} must define CSS keyframe animations`);
    assert.match(css, /@media\s*\(\s*prefers-reduced-motion:\s*reduce\s*\)/i, `${path.basename(file)} must implement prefers-reduced-motion guard`);
  }
});
