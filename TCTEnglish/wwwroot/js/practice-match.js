/*
 * Ghép thẻ (match): boards of up to 6 pairs, a timer, +1 s per miss, device-local best time. Never writes progress.
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var ui = P.ui;
    var core = P.core;
    var el = ui.el;
    var icon = ui.icon;

    var FEEDBACK_MS = 400;

    function run(session) {
        var config = session.config;
        var userId = config.userId;
        var requested = (config.query && config.query.n) || config.targets.length;
        var boards = core.buildMatchBoards(config.targets, requested, Math.random);
        var totalPairs = boards.reduce(function (sum, b) { return sum + b.pairIds.length; }, 0);

        if (boards.length === 0) {
            session.render(ui.stateMessage({
                title: 'Cần ít nhất 2 từ để ghép thẻ.',
                message: 'Thêm từ vào bộ thẻ hoặc chọn nguồn từ khác.',
                actionLabel: 'Quay lại Luyện tập',
                actionUrl: config.hubUrl
            }));
            session.setCounter('');
            return;
        }

        var best = session.store.getMatchBest(userId, config.scopeKey, requested);
        var live = el('p', 'visually-hidden');
        live.setAttribute('aria-live', 'polite');
        var game = { boardIndex: 0, matched: 0, misses: 0, penalty: 0, startedAt: 0, running: false, selected: null, locked: false };

        // ------------------------------------------------------------- start screen
        function showStart() {
            var box = el('section', 'tct-card tp-match-start');
            box.appendChild(el('h2', 'tp-summary__title', 'Ghép thẻ'));
            box.appendChild(el('p', 'm-0', 'Ghép từ với nghĩa nhanh nhất có thể. Ghép sai bị cộng 1 giây.'));
            var record = el('p', 'tp-match-record');
            if (best) {
                record.appendChild(icon('fa-solid fa-trophy me-1'));
                record.appendChild(document.createTextNode('Kỷ lục: ' + core.formatMs(best.ms)));
            } else {
                record.textContent = 'Chưa có kỷ lục';
            }
            box.appendChild(record);
            var start = el('button', 'btn btn-primary btn-lg', 'Bắt đầu');
            start.type = 'button';
            start.addEventListener('click', begin);
            box.appendChild(start);
            session.setSlot([]);
            session.setProgress(0, totalPairs);
            session.setCounter('');
            session.render(box);
            start.focus();
        }

        // ------------------------------------------------------------- timer
        var timerText = el('span', 'tp-timer-text', '0,0 giây');
        timerText.setAttribute('aria-hidden', 'true');
        var timer = el('span', 'tp-timer');
        timer.appendChild(icon('fa-solid fa-stopwatch'));
        timer.appendChild(timerText);
        var boardLabel = el('span', 'tct-badge tct-badge--warning');
        var lastTenths = -1;

        function elapsed() { return performance.now() - game.startedAt + game.penalty; }

        function tick() {
            if (!game.running) { return; }
            var tenths = Math.floor(elapsed() / 100);
            if (tenths !== lastTenths) {
                lastTenths = tenths;
                timerText.textContent = core.formatMs(tenths * 100);
            }
            window.requestAnimationFrame(tick);
        }

        function announce(text) { live.textContent = text; }

        // ------------------------------------------------------------- board
        var grid = null;

        function showBoard() {
            var board = boards[game.boardIndex];
            boardLabel.textContent = 'Bảng ' + (game.boardIndex + 1) + '/' + boards.length;
            var section = el('section', 'tp-match-board');
            section.setAttribute('aria-label', 'Bảng ghép ' + (game.boardIndex + 1));
            grid = el('div', 'tp-match-grid');
            board.tiles.forEach(function (tile) {
                var button = el('button', 'tct-card tp-tile');
                button.type = 'button';
                button.dataset.tileId = tile.id;
                button.setAttribute('aria-pressed', 'false');
                var label = el('span', 'tp-tile__label tct-clamp-2', tile.label);
                label.title = tile.label;
                if (tile.side === 'term') { label.lang = 'en'; }
                button.appendChild(label);
                button.addEventListener('click', function () { pick(tile, button); });
                tile.button = button;
                grid.appendChild(button);
            });
            section.appendChild(grid);
            section.appendChild(live);
            session.render(section, true);
            session.stage.focus({ preventScroll: true });
        }

        function setSelected(tile) {
            if (game.selected) {
                game.selected.tile.button.setAttribute('aria-pressed', 'false');
                game.selected.tile.button.classList.remove('is-selected');
            }
            game.selected = tile ? { tile: tile } : null;
            if (tile) {
                tile.button.setAttribute('aria-pressed', 'true');
                tile.button.classList.add('is-selected');
            }
        }

        function floatPenalty(button) {
            var badge = el('span', 'tct-badge tct-badge--danger tp-penalty', '+1 giây');
            badge.setAttribute('aria-hidden', 'true');
            button.appendChild(badge);
            setTimeout(function () { badge.remove(); }, 900);
        }

        function pick(tile, button) {
            if (game.locked || !game.running || button.disabled) { return; }
            if (!game.selected) { setSelected(tile); return; }
            if (game.selected.tile === tile) { setSelected(null); return; }

            var first = game.selected.tile;
            game.locked = true;
            session.noteAnswer();
            if (core.isPair(first, tile)) {
                setSelected(null);
                [first, tile].forEach(function (t) { t.button.classList.add('is-matched', 'tp-pop'); t.button.setAttribute('aria-pressed', 'false'); });
                game.matched += 1;
                session.setProgress(game.matched, totalPairs);
                announce('Đúng');
                setTimeout(function () {
                    [first, tile].forEach(function (t) { t.button.classList.add('is-gone'); t.button.disabled = true; });
                    game.locked = false;
                    boardCleared();
                }, FEEDBACK_MS);
            } else {
                game.misses += 1;
                game.penalty += core.MATCH_PENALTY_MS;
                [first, tile].forEach(function (t) { t.button.classList.add('is-miss'); });
                floatPenalty(tile.button);
                announce('Sai, cộng 1 giây');
                setTimeout(function () {
                    [first, tile].forEach(function (t) { t.button.classList.remove('is-miss'); });
                    setSelected(null);
                    game.locked = false;
                }, FEEDBACK_MS);
            }
        }

        function boardCleared() {
            var board = boards[game.boardIndex];
            var remaining = board.tiles.filter(function (t) { return !t.button.disabled; });
            if (remaining.length > 0) { return; }
            if (game.boardIndex < boards.length - 1) {
                game.boardIndex += 1;
                showBoard();
            } else {
                finish();
            }
        }

        // ------------------------------------------------------------- flow
        function begin() {
            game.running = true;
            game.startedAt = performance.now();
            session.setSlot([timer, boardLabel]);
            showBoard();
            window.requestAnimationFrame(tick);
        }

        function finish() {
            game.running = false;
            session.finish();
            var ms = Math.round(elapsed());
            var record = session.store.setMatchBest(userId, config.scopeKey, requested, ms);
            if (config.setId && !(config.query && config.query.words)) {
                session.store.mergeStats(config.setId, { matchMs: ms, matchPairs: totalPairs });
            }
            var currentBest = record ? { ms: ms } : (session.store.getMatchBest(userId, config.scopeKey, requested) || { ms: ms });

            var root = el('section', 'tp-summary');
            root.setAttribute('aria-label', 'Kết quả');
            var head = el('div', 'tp-summary__head');
            var star = el('div', 'tp-stars' + (record ? ' tp-pop' : ''));
            star.setAttribute('role', 'img');
            star.setAttribute('aria-label', record ? 'Kỷ lục mới' : 'Hoàn thành');
            star.appendChild(icon('fa-solid fa-trophy'));
            head.appendChild(star);
            var title = el('h2', 'tp-summary__title', 'Xong rồi!');
            title.tabIndex = -1;
            head.appendChild(title);
            if (record) {
                head.appendChild(el('span', 'tct-badge tct-badge--success', 'Kỷ lục mới!'));
            } else {
                head.appendChild(el('p', 'm-0', 'Kỷ lục: ' + core.formatMs(currentBest.ms)));
            }
            root.appendChild(head);

            var kpis = el('div', 'tp-kpis');
            [['Thời gian', core.formatMs(ms)], ['Ghép sai', String(game.misses)], ['Số cặp', String(totalPairs)]].forEach(function (pair) {
                var card = el('div', 'tct-card tp-kpi');
                card.appendChild(el('span', 'tp-kpi__value', pair[1]));
                card.appendChild(el('span', 'tp-kpi__label', pair[0]));
                kpis.appendChild(card);
            });
            root.appendChild(kpis);

            var actions = el('div', 'tp-summary__actions');
            [['Chơi lại', config.againUrl, 'btn-primary'], ['Thử chế độ Học', config.learnUrl, 'btn-outline-primary'], ['Xong', config.exitUrl, 'btn-outline-primary']]
                .forEach(function (a) {
                    var link = el('a', 'btn ' + a[2], a[0]);
                    link.href = a[1];
                    actions.appendChild(link);
                });
            root.appendChild(actions);
            session.setSlot([]);
            session.setProgress(totalPairs, totalPairs);
            session.render(root);
            title.focus();
        }

        showStart();
    }

    P.modes.match = run;
}());
