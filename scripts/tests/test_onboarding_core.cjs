'use strict';
// node --test scripts/tests/test_onboarding_core.cjs
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const core = require(path.join(__dirname, '../../TCTEnglish/wwwroot/js/onboarding-core.js'));

const topics = [
    { key: 'A16', name: 'Phương tiện giao thông', band: 'A1–A2', goals: ['travel', 'beginner'] },
    { key: 'A19', name: 'Thể thao', band: 'A1–A2', goals: ['hobbies'] },
    { key: 'B01', name: 'Công việc & văn phòng', band: 'B1–B2', goals: ['work'] },
    { key: 'B04', name: 'Du lịch & sân bay', band: 'B1–B2', goals: ['travel'] },
    { key: 'C04', name: 'Hỏi và chỉ đường', band: 'Giao tiếp', goals: ['travel', 'communication'] }
];

test('limits mirror the server', () => {
    assert.equal(core.MAX_GOALS, 3);
    assert.equal(core.MIN_TOPICS, 3);
    assert.equal(core.MAX_TOPICS, 8);
    assert.deepEqual(core.LEVELS, ['A1', 'A2', 'B1', 'B2']);
    assert.deepEqual(core.PACES, [5, 10, 15, 20]);
});

test('toggleLimited adds, removes and refuses the one over the limit without deselecting anything', () => {
    let r = core.toggleLimited([], 'a', 3);
    assert.deepEqual(r, { list: ['a'], blocked: false });
    r = core.toggleLimited(r.list, 'b', 3);
    r = core.toggleLimited(r.list, 'c', 3);
    assert.deepEqual(r.list, ['a', 'b', 'c']);

    const refused = core.toggleLimited(r.list, 'd', 3);
    assert.equal(refused.blocked, true);
    assert.deepEqual(refused.list, ['a', 'b', 'c']);

    const removed = core.toggleLimited(r.list, 'b', 3);
    assert.deepEqual(removed, { list: ['a', 'c'], blocked: false });
    // removing works even at the limit, and the selection order is kept
    assert.deepEqual(core.toggleLimited(['a', 'c'], 'b', 3).list, ['a', 'c', 'b']);
});

test('toggleLimited does not mutate its input', () => {
    const input = ['a', 'b'];
    core.toggleLimited(input, 'c', 5);
    core.toggleLimited(input, 'a', 5);
    assert.deepEqual(input, ['a', 'b']);
});

test('step guard: goals 1-3, topics 3-8, level and pace required', () => {
    assert.equal(core.canContinue(1, { goals: [] }), false);
    assert.equal(core.canContinue(1, { goals: ['travel'] }), true);
    assert.equal(core.canContinue(1, { goals: ['a', 'b', 'c', 'd'] }), false);
    assert.equal(core.canContinue(2, { topics: ['1', '2'] }), false);
    assert.equal(core.canContinue(2, { topics: ['1', '2', '3'] }), true);
    assert.equal(core.canContinue(2, { topics: Array.from({ length: 8 }, (_, i) => String(i)) }), true);
    assert.equal(core.canContinue(2, { topics: Array.from({ length: 9 }, (_, i) => String(i)) }), false);
    assert.equal(core.canContinue(3, { level: 'A2', pace: 10 }), true);
    assert.equal(core.canContinue(3, { level: 'C2', pace: 10 }), false);
    assert.equal(core.canContinue(3, { level: 'A2', pace: 7 }), false);
    assert.equal(core.canContinue(3, { level: null, pace: 10 }), false);
});

test('default level is A1 for beginners and A2 otherwise', () => {
    assert.equal(core.defaultLevel(['travel', 'beginner']), 'A1');
    assert.equal(core.defaultLevel(['travel']), 'A2');
    assert.equal(core.defaultLevel([]), 'A2');
});

test('estimateDays is the ceiling of words over pace', () => {
    assert.equal(core.estimateDays(25, 5), 5);
    assert.equal(core.estimateDays(26, 5), 6);
    assert.equal(core.estimateDays(73, 15), 5);
    assert.equal(core.estimateDays(0, 10), 0);
    assert.equal(core.estimateDays(10, 0), 0);
});

test('tabTopics: suggested first, then the rest; bands by tab', () => {
    const suggested = core.tabTopics(topics, ['travel'], 'suggested').map(t => t.key);
    assert.deepEqual(suggested, ['A16', 'B04', 'C04', 'A19', 'B01']);
    assert.deepEqual(core.tabTopics(topics, [], 'suggested').map(t => t.key), ['A16', 'A19', 'B01', 'B04', 'C04']);
    assert.deepEqual(core.tabTopics(topics, ['travel'], 'A').map(t => t.key), ['A16', 'A19']);
    assert.deepEqual(core.tabTopics(topics, ['travel'], 'B').map(t => t.key), ['B01', 'B04']);
    assert.deepEqual(core.tabTopics(topics, ['travel'], 'C').map(t => t.key), ['C04']);
    assert.equal(core.isSuggested(topics[0], ['travel']), true);
    assert.equal(core.isSuggested(topics[1], ['travel']), false);
});

const decks = [
    { key: 'travel', cards: [{ cardId: 1 }, { cardId: 2 }, { cardId: 3 }, { cardId: 4 }] },
    { key: 'phrases', cards: [{ cardId: 5 }, { cardId: 6 }] }
];

test('previewTotals counts only decks that are on and cards not marked known', () => {
    const t = core.previewTotals(decks, {}, { 2: true, 5: true }, 10);
    assert.deepEqual({ decks: t.decks, total: t.total, days: t.days }, { decks: 2, total: 4, days: 1 });
    assert.equal(t.text, 'Tổng cộng 4 từ trong 2 bộ · khoảng 1 ngày với 10 từ/ngày');

    const off = core.previewTotals(decks, { phrases: true }, {}, 5);
    assert.equal(off.text, 'Tổng cộng 4 từ trong 1 bộ · khoảng 1 ngày với 5 từ/ngày');
});

test('keptCards and canCreate follow the switches and the known marks', () => {
    assert.deepEqual(core.keptCards(decks[0], { 1: true, 3: true }).map(c => c.cardId), [2, 4]);
    assert.equal(core.canCreate(decks, {}, {}), true);
    assert.equal(core.canCreate(decks, { travel: true, phrases: true }, {}), false);
    assert.equal(core.canCreate(decks, { travel: true }, { 5: true, 6: true }), false); // the only deck left has no card
    assert.equal(core.canCreate(decks, { travel: true }, { 5: true }), true);
});

test('greeting uses the display name as the dashboard shows it, never an e-mail', () => {
    assert.equal(core.welcomeTitle('Minh Anh'), 'Chào mừng Minh Anh đến với TCT English!');
    assert.equal(core.welcomeTitle('  Huỳnh   Phú  Trọng '), 'Chào mừng Huỳnh Phú Trọng đến với TCT English!');
    assert.equal(core.greetingName('x'.repeat(60)).length, 40);
    assert.equal(core.welcomeTitle('someone@example.test'), 'Chào mừng bạn đến với TCT English!');
    assert.equal(core.welcomeTitle(''), 'Chào mừng bạn đến với TCT English!');
    assert.equal(core.welcomeTitle(null), 'Chào mừng bạn đến với TCT English!');
});

test('request ids: crypto.randomUUID, getRandomValues and the Math.random fallback all give valid ids', () => {
    assert.equal(core.newRequestId({ randomUUID: () => '123e4567-e89b-42d3-a456-426614174000' }), '123e4567-e89b-42d3-a456-426614174000');

    const viaValues = core.newRequestId({ getRandomValues: (arr) => { for (let i = 0; i < arr.length; i++) { arr[i] = i * 7; } return arr; } });
    assert.ok(core.isRequestId(viaValues), viaValues);

    const ids = new Set();
    for (let i = 0; i < 200; i++) {
        const id = core.newRequestId(null);
        assert.ok(core.isRequestId(id), id);
        ids.add(id);
    }
    assert.equal(ids.size, 200);
    assert.equal(core.isRequestId('not-a-guid'), false);
    assert.equal(core.isRequestId(''), false);
});

test('the preview shows the first twelve cards', () => {
    assert.equal(core.PREVIEW_FIRST, 12);
});
