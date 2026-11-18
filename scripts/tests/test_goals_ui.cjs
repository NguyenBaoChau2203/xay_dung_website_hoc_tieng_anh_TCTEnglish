'use strict';

/**
 * Behavior tests for Goals UI interactions (goals.js).
 * Executes production TCTEnglish/wwwroot/js/goals.js in a Node harness
 * with fake DOM using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const fs = require('node:fs');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const GOALS_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/goals.js');

function setupGoalsDom(options = {}) {
  const { document: doc, byId, body } = createDocument([]);

  function dispatchWithBubble(target, evt) {
    evt.target = target;
    let cur = target;
    while (cur) {
      if (cur._listeners && cur._listeners[evt.type]) {
        for (const fn of cur._listeners[evt.type]) fn.call(cur, evt);
      }
      cur = cur.parentNode;
    }
    if (target !== doc && doc._listeners && doc._listeners[evt.type]) {
      for (const fn of doc._listeners[evt.type]) fn.call(doc, evt);
    }
  }

  doc.dispatchEvent = (evt) => {
    dispatchWithBubble(doc, evt);
  };

  function make(tag, id) {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    el.dispatchEvent = (evt) => dispatchWithBubble(el, evt);
    el.click = () => {
      dispatchWithBubble(el, { type: 'click', target: el, preventDefault() {} });
    };
    return el;
  }

  // Header trigger button
  const headerCta = make('button', 'goalHeaderCta');
  headerCta.setAttribute('data-testid', 'goal-header-cta');
  headerCta.setAttribute('data-bs-target', '#goalEditorModal');
  let focusedElement = null;
  headerCta.focus = () => { focusedElement = headerCta; };
  body.appendChild(headerCta);

  // Modal element
  const modal = make('div', 'goalEditorModal');
  modal.dataset.openOnLoad = options.openOnLoad ? 'true' : 'false';
  body.appendChild(modal);

  // Form inside modal
  const form = make('form', 'goalEditorForm');
  form.checkValidity = () => options.validity !== false;
  modal.appendChild(form);

  // Target input
  const targetInput = make('input', 'goalTargetInput');
  targetInput.className = 'goal-input';
  targetInput.value = '10';
  targetInput.focus = () => { focusedElement = targetInput; };
  targetInput.select = () => {};
  form.appendChild(targetInput);

  // Submit button
  const submitBtn = make('button', 'goalSubmitBtn');
  submitBtn.setAttribute('data-testid', 'goal-modal-submit');
  form.appendChild(submitBtn);

  // Stepper buttons
  const decBtn = make('button', 'goalTargetDecBtn');
  form.appendChild(decBtn);
  const incBtn = make('button', 'goalTargetIncBtn');
  form.appendChild(incBtn);

  // Area select & Unit badge
  const areaSelect = make('select', 'goalAreaSelect');
  areaSelect.value = 'Vocabulary';
  form.appendChild(areaSelect);

  const unitBadge = make('span', 'goalUnitBadge');
  form.appendChild(unitBadge);

  // Presets
  const preset5 = make('button', 'preset5');
  preset5.className = 'goal-preset-btn';
  preset5.setAttribute('data-preset', '5');
  form.appendChild(preset5);

  const preset20 = make('button', 'preset20');
  preset20.className = 'goal-preset-btn';
  preset20.setAttribute('data-preset', '20');
  form.appendChild(preset20);

  // Toast
  const toast = make('div', 'goalUpdateToast');
  const dismissBtn = make('button', 'toastDismissBtn');
  dismissBtn.setAttribute('data-goals-dismiss', 'goalUpdateToast');
  toast.appendChild(dismissBtn);
  body.appendChild(toast);

  // Badges filter buttons
  const badgeFilterAll = make('button', 'badgeFilterAll');
  badgeFilterAll.className = 'goals-badge-filter-btn is-active btn-primary';
  badgeFilterAll.setAttribute('data-badge-filter', 'all');
  body.appendChild(badgeFilterAll);

  const badgeFilterChill = make('button', 'badgeFilterChill');
  badgeFilterChill.className = 'goals-badge-filter-btn btn-outline-secondary';
  badgeFilterChill.setAttribute('data-badge-filter', 'chill');
  body.appendChild(badgeFilterChill);

  const badgeFilterMystery = make('button', 'badgeFilterMystery');
  badgeFilterMystery.className = 'goals-badge-filter-btn btn-outline-secondary';
  badgeFilterMystery.setAttribute('data-badge-filter', 'bi-an');
  body.appendChild(badgeFilterMystery);

  [badgeFilterAll, badgeFilterChill, badgeFilterMystery].forEach((button) => {
    button.focus = () => { focusedElement = button; };
  });

  // Badge grid & tiles
  const badgeTileChill = make('div', 'badgeTileChill');
  badgeTileChill.className = 'goals-badge-tile goals-badge-tile--unlocked';
  badgeTileChill.setAttribute('data-badge-code', 'writing-mood');
  badgeTileChill.setAttribute('data-badge-group', 'chill');
  badgeTileChill.setAttribute('data-badge-status', 'unlocked');
  badgeTileChill.setAttribute('data-badge-name', 'Bút mới lên mood');
  badgeTileChill.setAttribute('data-badge-desc', 'Hoàn thành 3 bài viết khác nhau');
  badgeTileChill.setAttribute('data-badge-group-label', 'Nhập môn chill');
  badgeTileChill.setAttribute('data-badge-status-text', 'Đã mở khóa');
  badgeTileChill.setAttribute('data-badge-unlocked', 'true');
  badgeTileChill.setAttribute('data-badge-redacted', 'false');
  badgeTileChill.setAttribute('data-badge-awarded', '14/11/2026');
  badgeTileChill.focus = () => { focusedElement = badgeTileChill; };
  body.appendChild(badgeTileChill);

  const badgeTileMystery = make('div', 'badgeTileMystery');
  badgeTileMystery.className = 'goals-badge-tile goals-badge-tile--locked goals-badge-tile--mystery-locked';
  badgeTileMystery.setAttribute('data-badge-code', '');
  badgeTileMystery.setAttribute('data-badge-group', 'bi-an');
  badgeTileMystery.setAttribute('data-badge-status', 'locked');
  badgeTileMystery.setAttribute('data-badge-name', 'Ủa, bí mật nè!');
  badgeTileMystery.setAttribute('data-badge-desc', 'Một điều bất ngờ đang chờ bạn khám phá.');
  badgeTileMystery.setAttribute('data-badge-group-label', 'Ủa, bí mật nè!');
  badgeTileMystery.setAttribute('data-badge-status-text', 'Chưa khám phá');
  badgeTileMystery.setAttribute('data-badge-unlocked', 'false');
  badgeTileMystery.setAttribute('data-badge-redacted', 'true');
  badgeTileMystery.setAttribute('data-badge-progress-label', 'Chưa khám phá');
  badgeTileMystery.focus = () => { focusedElement = badgeTileMystery; };
  body.appendChild(badgeTileMystery);

  const badgeFilterEmpty = make('div', 'badgeFilterEmptyState');
  badgeFilterEmpty.className = 'd-none';
  body.appendChild(badgeFilterEmpty);

  // Badge detail modal elements
  const badgeDetailModal = make('div', 'badgeDetailModal');
  const badgeDetailTitle = make('h5', 'badgeDetailModalTitle');
  const badgeDetailDesc = make('p', 'badgeDetailDesc');
  const badgeDetailGroup = make('span', 'badgeDetailGroupBadge');
  const badgeDetailStatus = make('span', 'badgeDetailStatusBadge');
  const badgeDetailIconBox = make('div', 'badgeDetailIconBox');
  const badgeDetailIcon = make('i', 'badgeDetailIcon');
  const badgeDetailProgressWrap = make('div', 'badgeDetailProgressWrap');
  const badgeDetailAwardedWrap = make('div', 'badgeDetailAwardedWrap');
  const badgeDetailAwardedText = make('span', 'badgeDetailAwardedText');
  badgeDetailModal.appendChild(badgeDetailTitle);
  badgeDetailModal.appendChild(badgeDetailDesc);
  badgeDetailModal.appendChild(badgeDetailGroup);
  badgeDetailModal.appendChild(badgeDetailStatus);
  badgeDetailModal.appendChild(badgeDetailIconBox);
  badgeDetailModal.appendChild(badgeDetailIcon);
  badgeDetailModal.appendChild(badgeDetailProgressWrap);
  badgeDetailModal.appendChild(badgeDetailAwardedWrap);
  badgeDetailModal.appendChild(badgeDetailAwardedText);
  body.appendChild(badgeDetailModal);

  // Bootstrap Modal mock
  let modalShownCount = 0;
  let badgeModalShownCount = 0;
  const mockModalInstance = {
    show: () => {
      modalShownCount++;
      dispatchWithBubble(modal, { type: 'shown.bs.modal' });
    },
    hide: () => {
      dispatchWithBubble(modal, { type: 'hidden.bs.modal' });
    }
  };
  const mockBadgeModalInstance = {
    show: () => {
      badgeModalShownCount++;
      dispatchWithBubble(badgeDetailModal, { type: 'shown.bs.modal' });
    },
    hide: () => {
      dispatchWithBubble(badgeDetailModal, { type: 'hidden.bs.modal' });
    }
  };

  const sessionStorageMock = {
    _data: {},
    getItem(k) { return Object.prototype.hasOwnProperty.call(this._data, k) ? this._data[k] : null; },
    setItem(k, v) { this._data[k] = String(v); },
    removeItem(k) { delete this._data[k]; }
  };

  const historyMock = {
    state: null,
    url: 'http://localhost:5000/Goals',
    replaceState(state, title, url) { this.state = state; this.url = url; },
    pushState(state, title, url) { this.state = state; this.url = url; }
  };

  const windowMock = {
    document: doc,
    sessionStorage: sessionStorageMock,
    history: historyMock,
    location: new URL('http://localhost:5000/Goals'),
    bootstrap: {
      Modal: {
        getOrCreateInstance: (el) => (el === badgeDetailModal ? mockBadgeModalInstance : mockModalInstance)
      }
    },
    setTimeout: (fn, ms) => setTimeout(fn, ms),
    clearTimeout: (id) => clearTimeout(id),
    addEventListener: (type, fn) => {
      if (!doc._listeners[type]) doc._listeners[type] = [];
      doc._listeners[type].push(fn);
    }
  };

  return {
    doc,
    body,
    windowMock,
    sessionStorageMock,
    historyMock,
    headerCta,
    modal,
    form,
    targetInput,
    submitBtn,
    preset5,
    preset20,
    decBtn,
    incBtn,
    areaSelect,
    unitBadge,
    toast,
    dismissBtn,
    badgeFilterAll,
    badgeFilterChill,
    badgeFilterMystery,
    badgeTileChill,
    badgeTileMystery,
    badgeFilterEmpty,
    badgeDetailModal,
    badgeDetailTitle,
    badgeDetailDesc,
    badgeDetailGroup,
    badgeDetailStatus,
    badgeDetailAwardedWrap,
    badgeDetailAwardedText,
    badgeDetailProgressWrap,
    getFocusedElement: () => focusedElement,
    getModalShownCount: () => modalShownCount,
    getBadgeModalShownCount: () => badgeModalShownCount,
    mockModalInstance,
    mockBadgeModalInstance,
    dispatchWithBubble
  };
}

function runGoalsJs(windowMock) {
  const code = fs.readFileSync(GOALS_JS_PATH, 'utf8');
  const context = vm.createContext({
    document: windowMock.document,
    window: windowMock,
    Event: function (type, init) { this.type = type; this.bubbles = !!init?.bubbles; },
    URL: globalThis.URL,
    URLSearchParams: globalThis.URLSearchParams,
    setTimeout: windowMock.setTimeout,
    clearTimeout: windowMock.clearTimeout
  });
  vm.runInContext(code, context);
  // Trigger DOMContentLoaded
  windowMock.document.dispatchEvent({ type: 'DOMContentLoaded' });
}

test('Goals UI — Home/End select and focus the first/last badge group with one tab stop', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);
  env.badgeFilterChill.focus();
  env.badgeFilterChill.dispatchEvent({ type: 'keydown', key: 'End', preventDefault() {} });
  assert.equal(env.getFocusedElement(), env.badgeFilterMystery);
  assert.equal(env.badgeFilterMystery.getAttribute('aria-selected'), 'true');
  assert.equal(env.badgeFilterMystery.getAttribute('tabindex'), '0');
  assert.equal(env.badgeFilterAll.getAttribute('tabindex'), '-1');
  assert.equal(env.badgeTileChill.classList.contains('d-none'), true);
  env.badgeFilterMystery.dispatchEvent({ type: 'keydown', key: 'Home', preventDefault() {} });
  assert.equal(env.getFocusedElement(), env.badgeFilterAll);
  assert.equal(env.badgeFilterAll.getAttribute('aria-selected'), 'true');
  assert.equal(env.badgeFilterMystery.getAttribute('tabindex'), '-1');
  assert.equal(env.badgeTileChill.classList.contains('d-none'), false);
});

test('Goals UI — stale URL/storage groups recover to all on pageshow instead of hiding every badge', () => {
  for (const source of ['url', 'storage']) {
    const env = setupGoalsDom();
    if (source === 'url') env.windowMock.location = new URL('http://localhost:5000/Goals?badgeGroup=retired-group');
    else env.sessionStorageMock.setItem('goals_badge_group', 'retired-group');
    runGoalsJs(env.windowMock);
    env.doc.dispatchEvent({ type: 'pageshow', persisted: true });
    assert.equal(env.badgeFilterAll.getAttribute('aria-selected'), 'true', source);
    assert.equal(env.badgeTileChill.classList.contains('d-none'), false, source);
    assert.equal(env.badgeTileMystery.classList.contains('d-none'), false, source);
    assert.equal(env.badgeFilterEmpty.classList.contains('d-none'), true, source);
  }
});

test('Goals UI — Quick preset buttons update target input', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  assert.equal(env.targetInput.value, '10');
  env.preset20.click();
  assert.equal(env.targetInput.value, '20');
  assert.equal(env.getFocusedElement(), env.targetInput);

  env.preset5.click();
  assert.equal(env.targetInput.value, '5');
});

test('Goals UI — Modal auto-opens when data-open-on-load is true', () => {
  const env = setupGoalsDom({ openOnLoad: true });
  runGoalsJs(env.windowMock);

  assert.equal(env.getModalShownCount(), 1);
});

test('Goals UI — Modal focuses target input on shown, returns focus to trigger on hide', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Click trigger
  env.headerCta.click();

  // Show modal
  env.mockModalInstance.show();
  assert.equal(env.getFocusedElement(), env.targetInput);

  // Hide modal
  env.mockModalInstance.hide();
  assert.equal(env.getFocusedElement(), env.headerCta);
});

test('Goals UI — Double-submit prevention disables submit button on valid submit and restores on hide', async () => {
  const env = setupGoalsDom({ validity: true });
  runGoalsJs(env.windowMock);

  assert.equal(env.submitBtn.disabled, false);

  env.dispatchWithBubble(env.form, { type: 'submit', preventDefault() {} });
  await delay(50);

  assert.equal(env.submitBtn.disabled, true);
  assert.equal(env.submitBtn.getAttribute('aria-busy'), 'true');

  // Modal hidden event resets state
  env.mockModalInstance.hide();
  assert.equal(env.submitBtn.disabled, false);
});

test('Goals UI — Feedback toast adds is-visible and responds to dismiss', async () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  await delay(50);
  assert.equal(env.toast.classList.contains('is-visible'), true);

  env.dismissBtn.click();
  assert.equal(env.toast.classList.contains('is-visible'), false);
});


test('Goals UI — Stepper buttons decrement and increment target value clamped between 0 and 500', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  env.targetInput.value = '10';
  env.decBtn.click();
  assert.equal(env.targetInput.value, 5);

  env.decBtn.click();
  assert.equal(env.targetInput.value, 0);

  // Clamping at min 0
  env.decBtn.click();
  assert.equal(env.targetInput.value, 0);

  // Incrementing from 0 steps by 5
  env.incBtn.click();
  assert.equal(env.targetInput.value, 5);

  env.incBtn.click();
  assert.equal(env.targetInput.value, 10);

  // Sub-5 stepping (e.g. 3 -> 4 -> 5)
  env.targetInput.value = '3';
  env.incBtn.click();
  assert.equal(env.targetInput.value, 4);

  // Clamping at max 500
  env.targetInput.value = '498';
  env.incBtn.click();
  assert.equal(env.targetInput.value, 500);
});

test('Goals UI — Preset chips sync active state when input value changes', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Initial value is '10' so preset5 is not active
  assert.equal(env.preset5.classList.contains('is-active'), false);

  // Click preset5
  env.preset5.click();
  assert.equal(env.targetInput.value, '5');
  assert.equal(env.preset5.classList.contains('is-active'), true);
  assert.equal(env.preset20.classList.contains('is-active'), false);

  // Change input to 20
  env.targetInput.value = '20';
  env.dispatchWithBubble(env.targetInput, { type: 'input' });
  assert.equal(env.preset20.classList.contains('is-active'), true);
  assert.equal(env.preset5.classList.contains('is-active'), false);
});

test('Goals UI — Badge tier filters toggle active state and filter visible badge tiles', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Initial state: 'all' filter active, both tiles visible
  assert.equal(env.badgeFilterAll.classList.contains('is-active'), true);
  assert.equal(env.badgeTileChill.classList.contains('d-none'), false);
  assert.equal(env.badgeTileMystery.classList.contains('d-none'), false);

  // Click 'chill' filter: only chill tile visible, mystery tile hidden
  env.badgeFilterChill.click();
  assert.equal(env.badgeFilterChill.classList.contains('is-active'), true);
  assert.equal(env.badgeFilterAll.classList.contains('is-active'), false);
  assert.equal(env.badgeTileChill.classList.contains('d-none'), false);
  assert.equal(env.badgeTileMystery.classList.contains('d-none'), true);
  assert.equal(env.sessionStorageMock.getItem('goals_badge_group'), 'chill');
  assert.equal(env.historyMock.state?.badgeGroup, 'chill');

  // Click 'all' filter: both tiles restored
  env.badgeFilterAll.click();
  assert.equal(env.badgeTileChill.classList.contains('d-none'), false);
  assert.equal(env.badgeTileMystery.classList.contains('d-none'), false);
  assert.equal(env.sessionStorageMock.getItem('goals_badge_group'), 'all');
});

test('Goals UI — Locked mystery badges redact secrets from attributes and DOM', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Verify mystery badge attributes are completely redacted
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-code'), '');
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-redacted'), 'true');
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-name'), 'Ủa, bí mật nè!');
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-desc'), 'Một điều bất ngờ đang chờ bạn khám phá.');
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-group-label'), 'Ủa, bí mật nè!');
  assert.equal(env.badgeTileMystery.getAttribute('data-badge-status-text'), 'Chưa khám phá');

  // Verify no secret leak in attributes
  const allMysteryAttrs = JSON.stringify(env.badgeTileMystery.attributes);
  assert.equal(allMysteryAttrs.includes('Plot twist'), false);
  assert.equal(allMysteryAttrs.includes('Hệ chăm ngầm'), false);
  assert.equal(allMysteryAttrs.includes('13 distinct'), false);
  assert.equal(allMysteryAttrs.includes('21 confirmed'), false);
});

test('Goals UI — Badge detail modal opens with honest information and restores focus on hide', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Click unlocked chill badge tile
  env.badgeTileChill.click();
  assert.equal(env.getBadgeModalShownCount(), 1);
  assert.equal(env.badgeDetailTitle.textContent, 'Bút mới lên mood');
  assert.equal(env.badgeDetailDesc.textContent, 'Hoàn thành 3 bài viết khác nhau');
  assert.equal(env.badgeDetailGroup.textContent, 'Nhập môn chill');
  assert.equal(env.badgeDetailAwardedWrap.classList.contains('d-none'), false);
  assert.equal(env.badgeDetailAwardedText.textContent.includes('14/11/2026'), true);

  // Hide modal restores focus to trigger tile
  env.mockBadgeModalInstance.hide();
  assert.equal(env.getFocusedElement(), env.badgeTileChill);

  // Click mystery badge tile
  env.badgeTileMystery.click();
  assert.equal(env.getBadgeModalShownCount(), 2);
  assert.equal(env.badgeDetailTitle.textContent, 'Ủa, bí mật nè!');
  assert.equal(env.badgeDetailDesc.textContent, 'Một điều bất ngờ đang chờ bạn khám phá.');
  assert.equal(env.badgeDetailAwardedWrap.classList.contains('d-none'), true);
  assert.equal(env.badgeDetailProgressWrap.classList.contains('d-none'), true);
});
