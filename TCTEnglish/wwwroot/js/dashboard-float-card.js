/**
 * Dashboard Hybrid Floating Data — Quick Tooltip (hover/focus) + Detail Card (click).
 * Uses only data-* attributes rendered from DashboardViewModel (no fabricated fields).
 */
(function () {
    'use strict';

    var tip = document.getElementById('dbFloatTooltip');
    var detail = document.getElementById('dbFloatDetail');
    var detailBody = detail ? detail.querySelector('[data-db-float-detail-body]') : null;
    if (!tip || !detail || !detailBody) return;

    var OFFSET = 14;
    var LEAVE_DELAY_MS = 140;
    var activeEl = null;
    var activeKey = '';
    var mode = 'none'; // none | tip | detail
    var leaveTimer = null;
    var pointerBound = false;
    var lastPointer = { x: 0, y: 0 };

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function buildTooltipHtml(el) {
        var kind = el.getAttribute('data-db-float');
        var accent = el.getAttribute('data-accent') || 'var(--tct-primary)';
        var html = '<div class="db-float-tip__inner">';
        html += '<span class="db-float-card__swatch" style="background:' + escapeHtml(accent) + '" aria-hidden="true"></span>';
        html += '<div class="db-float-card__body">';

        if (kind === 'weekly') {
            var day = el.getAttribute('data-day') || '';
            var date = el.getAttribute('data-date') || '';
            var skill = el.getAttribute('data-skill') || '';
            var minutes = Number(el.getAttribute('data-minutes') || 0);
            html += '<p class="db-float-card__title">' + escapeHtml(day || date) + '</p>';
            html += '<p class="db-float-card__stat"><strong>' + minutes + '</strong> phút</p>';
            html += '<p class="db-float-card__meta">' + escapeHtml(skill) + (date ? ' · ' + escapeHtml(date) : '') + '</p>';
        } else if (kind === 'vocab') {
            var label = el.getAttribute('data-label') || '';
            var count = Number(el.getAttribute('data-count') || 0);
            var vPct = Number(el.getAttribute('data-pct') || 0);
            html += '<p class="db-float-card__title">' + escapeHtml(label) + '</p>';
            html += '<p class="db-float-card__stat"><strong>' + count + '</strong> thẻ</p>';
            html += '<p class="db-float-card__meta">' + vPct + '%</p>';
        } else if (kind === 'heatmap') {
            var hDate = el.getAttribute('data-date') || '';
            var hDay = el.getAttribute('data-day') || '';
            var reviewed = Number(el.getAttribute('data-reviewed') || 0);
            var heatTitle = hDate || hDay;
            html += '<p class="db-float-card__title">' + escapeHtml(heatTitle) + '</p>';
            if (reviewed <= 0) {
                html += '<p class="db-float-card__meta">Chưa có hoạt động</p>';
            } else {
                html += '<p class="db-float-card__stat"><strong>' + reviewed + '</strong> thẻ ôn</p>';
            }
        } else {
            html += '<p class="db-float-card__meta">Không có dữ liệu</p>';
        }

        html += '</div></div>';
        return html;
    }

    function buildDetailHtml(el) {
        var kind = el.getAttribute('data-db-float');
        var accent = el.getAttribute('data-accent') || 'var(--tct-primary)';
        var html = '<div class="db-float-detail__inner">';
        html += '<span class="db-float-card__swatch" style="background:' + escapeHtml(accent) + '" aria-hidden="true"></span>';
        html += '<div class="db-float-card__body">';

        if (kind === 'weekly') {
            var day = el.getAttribute('data-day') || '';
            var date = el.getAttribute('data-date') || '';
            var skill = el.getAttribute('data-skill') || '';
            var minutes = Number(el.getAttribute('data-minutes') || 0);
            var total = Number(el.getAttribute('data-total') || 0);
            var pct = total > 0 ? Math.round((minutes * 1000) / total) / 10 : null;
            html += '<p class="db-float-card__title">' + escapeHtml(skill) + '</p>';
            html += '<p class="db-float-card__meta">' + escapeHtml(day) + (date ? ' · ' + escapeHtml(date) : '') + '</p>';
            html += '<p class="db-float-card__stat"><strong>' + minutes + '</strong> phút kỹ năng</p>';
            html += '<p class="db-float-card__meta">Tổng ngày: ' + total + ' phút';
            if (pct !== null) html += ' · ' + pct + '%';
            html += '</p>';
        } else if (kind === 'vocab') {
            var label = el.getAttribute('data-label') || '';
            var count = Number(el.getAttribute('data-count') || 0);
            var vTotal = Number(el.getAttribute('data-total') || 0);
            var vPct = Number(el.getAttribute('data-pct') || 0);
            html += '<p class="db-float-card__title">' + escapeHtml(label) + '</p>';
            html += '<p class="db-float-card__stat"><strong>' + count + '</strong> thẻ</p>';
            html += '<p class="db-float-card__meta">Tổng ' + vTotal + ' thẻ · ' + vPct + '%</p>';
        } else if (kind === 'heatmap') {
            var hDate = el.getAttribute('data-date') || '';
            var hDay = el.getAttribute('data-day') || '';
            var hWeek = el.getAttribute('data-week') || '';
            var reviewed = Number(el.getAttribute('data-reviewed') || 0);
            var intensityLabel = el.getAttribute('data-intensity-label') || '';
            var heatTitle = hDate || (hDay + (hWeek ? ' · ' + hWeek : ''));
            html += '<p class="db-float-card__title">' + escapeHtml(heatTitle) + '</p>';
            if (hDate && (hDay || hWeek)) {
                html += '<p class="db-float-card__meta">' + escapeHtml(hDay) + (hWeek ? ' · ' + escapeHtml(hWeek) : '') + '</p>';
            }
            if (reviewed <= 0) {
                html += '<p class="db-float-card__meta">Chưa có hoạt động học tập</p>';
            } else {
                html += '<p class="db-float-card__stat"><strong>' + reviewed + '</strong> thẻ ôn</p>';
                html += '<p class="db-float-card__meta">Mức hoạt động: ' + escapeHtml(intensityLabel) + '</p>';
            }
        } else {
            html += '<p class="db-float-card__meta">Không có dữ liệu</p>';
        }

        html += '</div></div>';
        return html;
    }

    function elementKey(el) {
        return [
            el.getAttribute('data-db-float'),
            el.getAttribute('data-day'),
            el.getAttribute('data-date'),
            el.getAttribute('data-skill'),
            el.getAttribute('data-label'),
            el.getAttribute('data-week'),
            el.getAttribute('data-reviewed'),
            el.getAttribute('data-count'),
            el.getAttribute('data-minutes'),
            el.getAttribute('data-intensity')
        ].join('|');
    }

    function clearActiveClass() {
        document.querySelectorAll('.is-float-active').forEach(function (node) {
            node.classList.remove('is-float-active');
        });
        document.querySelectorAll('.db-time-chart.is-hovering').forEach(function (node) {
            node.classList.remove('is-hovering');
        });
    }

    function markActive(el) {
        clearActiveClass();
        if (!el) return;
        el.classList.add('is-float-active');
        var chart = el.closest('.db-time-chart');
        if (chart) chart.classList.add('is-hovering');
    }

    function positionLayer(layer, clientX, clientY) {
        layer.hidden = false;
        void layer.offsetWidth;
        var rect = layer.getBoundingClientRect();
        var vw = window.innerWidth;
        var vh = window.innerHeight;
        var width = rect.width || layer.offsetWidth || 180;
        var height = rect.height || layer.offsetHeight || 80;
        var x = clientX + OFFSET;
        var y = clientY + OFFSET;

        if (x + width > vw - 8) x = clientX - width - OFFSET;
        if (y + height > vh - 8) y = clientY - height - OFFSET;
        if (x < 8) x = 8;
        if (y < 8) y = 8;

        layer.style.left = Math.round(x) + 'px';
        layer.style.top = Math.round(y) + 'px';
    }

    function applyAccent(layer, el) {
        var accent = el.getAttribute('data-accent');
        if (accent) layer.style.setProperty('--db-float-accent', accent);
        else layer.style.removeProperty('--db-float-accent');
    }

    function hideTip() {
        tip.classList.remove('is-visible');
        tip.hidden = true;
        tip.setAttribute('aria-hidden', 'true');
        tip.innerHTML = '';
        if (mode === 'tip') mode = 'none';
    }

    function hideDetail() {
        detail.classList.remove('is-visible');
        detail.hidden = true;
        detail.setAttribute('aria-hidden', 'true');
        detailBody.innerHTML = '';
        if (mode === 'detail') {
            mode = 'none';
            activeEl = null;
            activeKey = '';
            clearActiveClass();
        }
        unbindPointerMove();
    }

    function hideAll() {
        clearLeaveTimer();
        hideTip();
        hideDetail();
        activeEl = null;
        activeKey = '';
        clearActiveClass();
        unbindPointerMove();
        mode = 'none';
    }

    function clearLeaveTimer() {
        if (leaveTimer) {
            clearTimeout(leaveTimer);
            leaveTimer = null;
        }
    }

    function scheduleHideTip() {
        clearLeaveTimer();
        leaveTimer = setTimeout(function () {
            leaveTimer = null;
            if (mode === 'tip') {
                hideTip();
                activeEl = null;
                activeKey = '';
                clearActiveClass();
                unbindPointerMove();
                mode = 'none';
            }
        }, LEAVE_DELAY_MS);
    }

    function showTip(el, clientX, clientY) {
        if (mode === 'detail') return;
        clearLeaveTimer();
        var key = elementKey(el);
        if (activeEl !== el || activeKey !== key || mode !== 'tip') {
            activeEl = el;
            activeKey = key;
            tip.innerHTML = buildTooltipHtml(el);
            applyAccent(tip, el);
        }
        markActive(el);
        tip.setAttribute('aria-hidden', 'false');
        tip.classList.add('is-visible');
        positionLayer(tip, clientX, clientY);
        mode = 'tip';
    }

    function showDetail(el, clientX, clientY) {
        clearLeaveTimer();
        hideTip();
        var key = elementKey(el);
        var same = activeEl === el && activeKey === key && mode === 'detail';
        activeEl = el;
        activeKey = key;
        if (!same) {
            detailBody.innerHTML = buildDetailHtml(el);
            applyAccent(detail, el);
        }
        markActive(el);
        detail.setAttribute('aria-hidden', 'false');
        detail.classList.add('is-visible');
        positionLayer(detail, clientX, clientY);
        mode = 'detail';
        unbindPointerMove();
    }

    function bindPointerMove() {
        if (pointerBound) return;
        pointerBound = true;
        document.addEventListener('pointermove', onPointerMove);
    }

    function unbindPointerMove() {
        if (!pointerBound) return;
        pointerBound = false;
        document.removeEventListener('pointermove', onPointerMove);
    }

    function onPointerMove(e) {
        lastPointer.x = e.clientX;
        lastPointer.y = e.clientY;
        if (mode !== 'tip' || !activeEl) return;
        positionLayer(tip, e.clientX, e.clientY);
    }

    function onEnter(e) {
        var el = e.currentTarget;
        var x = e.clientX;
        var y = e.clientY;
        if (e.type === 'focus') {
            var r = el.getBoundingClientRect();
            x = r.left + r.width / 2;
            y = r.top;
        }
        lastPointer.x = x;
        lastPointer.y = y;
        if (mode === 'detail') return;
        showTip(el, x, y);
        if (e.pointerType !== 'touch') bindPointerMove();
    }

    function onLeave(e) {
        if (mode !== 'tip') return;
        var related = e.relatedTarget;
        if (related && tip.contains(related)) return;
        if (related && related.closest && related.closest('#dbFloatTooltip')) return;
        scheduleHideTip();
    }

    function onTipEnter() {
        clearLeaveTimer();
    }

    function onTipLeave() {
        if (mode === 'tip') scheduleHideTip();
    }

    function onActivate(e) {
        var el = e.currentTarget;
        if (e.type === 'keydown') {
            if (e.key !== 'Enter' && e.key !== ' ') return;
            e.preventDefault();
        }
        if (e.type === 'pointerdown' && e.pointerType && e.pointerType !== 'touch' && e.button !== 0) return;

        var x = e.clientX != null ? e.clientX : lastPointer.x;
        var y = e.clientY != null ? e.clientY : lastPointer.y;
        if (e.type === 'keydown' || x == null) {
            var r = el.getBoundingClientRect();
            x = r.left + r.width / 2;
            y = r.top;
        }

        if (e.pointerType === 'touch') {
            e.preventDefault();
            if (mode === 'detail' && activeEl === el) {
                hideAll();
                return;
            }
            showDetail(el, x, y);
            return;
        }

        if (e.type === 'pointerdown' && e.pointerType !== 'touch') {
            // Desktop: click opens detail (handled on click to avoid steal).
            return;
        }

        if (mode === 'detail' && activeEl === el) {
            hideAll();
            return;
        }
        showDetail(el, x, y);
    }

    function onClick(e) {
        if (e.pointerType === 'touch') return;
        onActivate(e);
    }

    function onRootLeave(e) {
        if (mode !== 'tip') return;
        var root = e.currentTarget;
        var related = e.relatedTarget;
        if (related && root.contains(related)) return;
        if (related && related.closest && related.closest('#dbFloatTooltip')) return;
        scheduleHideTip();
    }

    document.querySelectorAll('[data-db-float]').forEach(function (el) {
        el.addEventListener('pointerenter', onEnter);
        el.addEventListener('pointerleave', onLeave);
        el.addEventListener('focus', onEnter);
        el.addEventListener('blur', onLeave);
        el.addEventListener('click', onClick);
        el.addEventListener('keydown', onActivate);
        el.addEventListener('pointerdown', function (e) {
            if (e.pointerType === 'touch') onActivate(e);
        });
    });

    document.querySelectorAll('[data-db-float-root]').forEach(function (root) {
        root.addEventListener('pointerleave', onRootLeave);
    });

    tip.addEventListener('pointerenter', onTipEnter);
    tip.addEventListener('pointerleave', onTipLeave);

    detail.querySelectorAll('[data-db-float-close]').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            hideAll();
        });
    });

    document.addEventListener('pointerdown', function (e) {
        if (mode === 'none') return;
        if (e.target.closest('[data-db-float]')) return;
        if (e.target.closest('#dbFloatTooltip')) return;
        if (e.target.closest('#dbFloatDetail')) return;
        hideAll();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && mode !== 'none') {
            hideAll();
        }
    });
})();
