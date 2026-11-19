'use strict';
// node --test scripts/tests/test_onboarding_assets.cjs
// Static guards for the first-run onboarding assets (plan 2026-11-19-onboarding-personal-decks, G6/G15).
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const web = path.join(__dirname, '../../TCTEnglish');
const read = (rel) => fs.readFileSync(path.join(web, rel), 'utf8');

const js = read('wwwroot/js/onboarding.js');
const core = read('wwwroot/js/onboarding-core.js');
const css = read('wwwroot/css/onboarding.css');
const host = read('Views/Shared/_OnboardingHost.cshtml');

test('scripts never write markup from data', () => {
    for (const [name, source] of [['onboarding.js', js], ['onboarding-core.js', core]]) {
        assert.doesNotMatch(source, /\binnerHTML\b|\bouterHTML\b|insertAdjacentHTML|document\.write|\beval\(|new Function/, name);
    }
});

test('css uses tokens only: no hex or rgb colours, and no new :root palette', () => {
    assert.doesNotMatch(css, /#[0-9a-fA-F]{3,8}\b/);
    assert.doesNotMatch(css, /\brgba?\(|\bhsla?\(/);
    assert.doesNotMatch(css, /^\s*:root\b/m);
    assert.match(css, /prefers-reduced-motion: reduce/);
    assert.match(css, /forced-colors: active/);
});

test('every rule is scoped to .tob-root (except the one page-level launcher rule)', () => {
    const selectors = css.replace(/\/\*[\s\S]*?\*\//g, '').split('}')
        .map(block => block.split('{')[0].trim()).filter(Boolean)
        .filter(sel => !sel.startsWith('@') && !/^(from|to)$/.test(sel));
    // split a selector list on top-level commas only (":is(a, b)" stays in one piece)
    const parts = (sel) => {
        const out = [];
        let depth = 0, current = '';
        for (const ch of sel) {
            if (ch === '(') depth++;
            if (ch === ')') depth--;
            if (ch === ',' && depth === 0) { out.push(current.trim()); current = ''; } else { current += ch; }
        }
        out.push(current.trim());
        return out;
    };
    const loose = selectors.filter(sel => parts(sel).some(part => !part.startsWith('.tob-root') && !/^\d+%$/.test(part)));
    assert.deepEqual(loose, ['body.tob-open .ai-launcher']);
});

test('the host has no inline style, handler or script body other than the JSON config', () => {
    assert.doesNotMatch(host, / style=/);
    assert.doesNotMatch(host, / on[a-z]+="/);
    const scripts = host.match(/<script\b[^>]*>/g) || [];
    for (const tag of scripts) {
        assert.ok(/type="application\/json"/.test(tag) || /\ssrc=/.test(tag), 'inline script: ' + tag);
    }
    assert.match(host, /data-bs-backdrop="static"/);
    assert.match(host, /role="dialog"/);
    assert.match(host, /aria-modal="true"/);
});

test('the dialog uses the roles the plan asks for', () => {
    for (const role of ['checkbox', 'radio', 'radiogroup', 'switch', 'tab', 'tablist']) {
        assert.ok(js.includes("'" + role + "'"), 'role ' + role);
    }
    assert.match(js, /aria-pressed/);
    assert.match(js, /aria-live|announce\(/);
    assert.match(js, /Escape/);
});

test('copy follows the plan: no English labels in the visible strings', () => {
    for (const text of ['Để sau', 'Quay lại', 'Tiếp tục', 'Xem trước bộ thẻ', 'Đã biết', 'Giữ lại', 'Mở Bộ thẻ của tôi', 'Đang tạo bộ thẻ…']) {
        assert.ok(js.includes(text), text);
    }
    assert.doesNotMatch(js, /'(Next|Back|Skip|Continue|Done|Cancel|Retry)'/);
    assert.doesNotMatch(js, /bi bi-/);
});
