/* TCT English landing page: pure logic of the "hoc thu" demo (three fixed vocabulary questions).
   No DOM, no network, no storage. Loaded in the browser as window.TctLanding and in Node by the tests. */
(function (root, factory) {
    'use strict';
    var api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    else { root.TctLanding = api; }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    // Keep the first question in sync with the no-JS markup in Views/Home/landing.cshtml (a node test checks it).
    var QUESTIONS = [
        {
            id: 'q1', prompt: 'Từ nào có nghĩa này?', word: 'đàm phán', correctId: 'negotiate',
            options: [{ id: 'accomplish', label: 'accomplish' }, { id: 'negotiate', label: 'negotiate' }, { id: 'schedule', label: 'schedule' }, { id: 'confirm', label: 'confirm' }]
        },
        {
            id: 'q2', prompt: 'Từ này nghĩa là gì?', word: 'deadline', correctId: 'deadline',
            options: [{ id: 'meeting', label: 'cuộc họp' }, { id: 'deadline', label: 'hạn chót' }, { id: 'contract', label: 'hợp đồng' }, { id: 'report', label: 'báo cáo' }]
        },
        {
            id: 'q3', prompt: 'Từ nào có nghĩa này?', word: 'cộng tác', correctId: 'collaborate',
            options: [{ id: 'compete', label: 'compete' }, { id: 'collaborate', label: 'collaborate' }, { id: 'complain', label: 'complain' }, { id: 'calculate', label: 'calculate' }]
        }
    ];

    // Small seeded generator (mulberry32) so a given seed always gives the same option order.
    function rng(seed) {
        var a = (seed >>> 0) || 1;
        return function () {
            a = (a + 0x6D2B79F5) >>> 0;
            var t = a;
            t = Math.imul(t ^ (t >>> 15), t | 1);
            t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
    }

    function shuffled(list, random) {
        var copy = list.slice();
        for (var i = copy.length - 1; i > 0; i--) {
            var j = Math.floor(random() * (i + 1));
            var tmp = copy[i]; copy[i] = copy[j]; copy[j] = tmp;
        }
        return copy;
    }

    function createDemo(questions, seed) {
        var random = rng(seed);
        return {
            seed: seed,
            source: questions,
            // Question order is fixed; only the options are shuffled, once per demo.
            questions: questions.map(function (q) {
                return {
                    id: q.id, prompt: q.prompt, word: q.word, correctId: q.correctId,
                    options: shuffled(q.options.map(function (o) { return { id: o.id, label: o.label }; }), random)
                };
            }),
            index: 0,
            chosen: questions.map(function () { return null; }),
            finished: false
        };
    }

    function labelOf(question, id) {
        for (var i = 0; i < question.options.length; i++) {
            if (question.options[i].id === id) { return question.options[i].label; }
        }
        return '';
    }

    function current(state) {
        var q = state.questions[state.index];
        var chosenId = state.chosen[state.index];
        return {
            index: state.index,
            number: state.index + 1,
            total: state.questions.length,
            prompt: q.prompt,
            word: q.word,
            options: q.options.slice(),
            answered: chosenId !== null,
            chosenId: chosenId,
            correctId: q.correctId,
            isLast: state.index === state.questions.length - 1
        };
    }

    function answer(state, optionId) {
        if (state.finished) { return { ignored: true }; }
        var q = state.questions[state.index];
        if (state.chosen[state.index] !== null) { return { ignored: true }; }
        if (!q.options.some(function (o) { return o.id === optionId; })) { return { ignored: true }; }
        state.chosen[state.index] = optionId;
        var correct = optionId === q.correctId;
        return {
            correct: correct,
            correctId: q.correctId,
            chosenId: optionId,
            label: correct ? 'Đúng rồi!' : 'Chưa đúng',
            detail: correct ? '' : 'Đáp án: ' + labelOf(q, q.correctId),
            schedule: correct ? 'Ôn lại sau 1 ngày' : 'Ôn lại sau 10 phút'
        };
    }

    function next(state) {
        if (state.finished) { return { moved: false, finished: true }; }
        if (state.chosen[state.index] === null) { return { moved: false, finished: false }; }
        if (state.index >= state.questions.length - 1) {
            state.finished = true;
            return { moved: false, finished: true };
        }
        state.index += 1;
        return { moved: true, finished: false };
    }

    function summary(state) {
        var correct = 0;
        state.questions.forEach(function (q, i) { if (state.chosen[i] === q.correctId) { correct += 1; } });
        return { correct: correct, total: state.questions.length };
    }

    function reset(state) {
        return createDemo(state.source, state.seed);
    }

    // Keys 1-4 pick the option at that position; anything else is not an answer key.
    function keyOptionIndex(key) {
        return /^[1-4]$/.test(String(key === undefined ? '' : key)) ? Number(key) - 1 : -1;
    }

    return {
        QUESTIONS: QUESTIONS,
        createDemo: createDemo,
        current: current,
        answer: answer,
        next: next,
        summary: summary,
        reset: reset,
        keyOptionIndex: keyOptionIndex
    };
});
