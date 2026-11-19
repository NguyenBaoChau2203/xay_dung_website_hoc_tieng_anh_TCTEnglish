'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('TCTEnglish/wwwroot/js/ai-voice-page.js', 'utf8');
const start = source.indexOf('    function checkMascotVisibility()');
const end = source.indexOf('    if (floatingMascot) {', start);
assert.ok(start >= 0 && end > start);
function harness({ workspaceTop = 100, heroBottom = 200, reduced = false } = {}) {
    const h = { hidden: true };
    const context = { floatingMascot: { removeAttribute() { h.hidden = false; }, setAttribute() { h.hidden = true; } },
        heroPanel: { getBoundingClientRect: () => ({ bottom: heroBottom }), scrollIntoView: options => { h.scroll = options; } },
        workspaceEl: { getBoundingClientRect: () => ({ top: workspaceTop }) },
        window: { innerHeight: 900, matchMedia: () => ({ matches: reduced }) } };
    vm.runInNewContext(source.slice(start, end) + '\nglobalThis.check = checkMascotVisibility;globalThis.scroll = scrollToHeroMascot;', context);
    return { h, context };
}
test('standalone companion appears after its workspace scrolls above the viewport', () => {
    const { h, context } = harness({ workspaceTop: -500, heroBottom: -100 });
    context.check(); assert.equal(h.hidden, false);
});
test('inline companion stays hidden while the hero is visible and appears past it', () => {
    for (const [heroBottom, hidden] of [[200,true],[120,false]]) {
        const { h, context } = harness({ heroBottom }); context.check(); assert.equal(h.hidden, hidden);
    }
});
test('return-to-mascot respects reduced motion without losing navigation', () => {
    for (const reduced of [false,true]) {
        const { h, context } = harness({ reduced }); let prevented = false;
        context.scroll({ preventDefault() { prevented = true; } });
        assert.equal(prevented, true); assert.equal(h.scroll.block, 'start');
        assert.equal(h.scroll.behavior, reduced ? 'auto' : 'smooth');
    }
});
