(function () {
    'use strict';

    function prefersReducedMotion() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function parseSeries(raw) {
        if (!raw) {
            return [];
        }
        try {
            var data = JSON.parse(raw);
            return Array.isArray(data) ? data : [];
        } catch (err) {
            return [];
        }
    }

    function maxValue(series, key) {
        var max = 0;
        for (var i = 0; i < series.length; i++) {
            var value = Number(series[i][key]) || 0;
            if (value > max) {
                max = value;
            }
        }
        return max;
    }

    function formatDayLabel(isoDate) {
        var parts = String(isoDate || '').split('-');
        if (parts.length !== 3) {
            return '';
        }
        return parts[2] + '/' + parts[1];
    }

    function renderLineChart(container) {
        if (!container) {
            return;
        }

        var series = parseSeries(container.getAttribute('data-series'));
        var emptyEl = container.querySelector('[data-chart-empty]');
        var svgHost = container.querySelector('[data-chart-svg]');
        if (!svgHost) {
            return;
        }

        var hasData = series.some(function (point) {
            return (Number(point.completions) || 0) > 0 || (Number(point.xp) || 0) > 0;
        });

        if (!hasData) {
            svgHost.innerHTML = '';
            if (emptyEl) {
                emptyEl.hidden = false;
            }
            return;
        }

        if (emptyEl) {
            emptyEl.hidden = true;
        }

        var width = svgHost.clientWidth || container.clientWidth || 480;
        var height = 220;
        var padLeft = 36;
        var padRight = 12;
        var padTop = 16;
        var padBottom = 32;
        var plotW = Math.max(40, width - padLeft - padRight);
        var plotH = Math.max(40, height - padTop - padBottom);
        var peak = Math.max(1, maxValue(series, 'completions'));
        var n = series.length;
        var stepX = n <= 1 ? 0 : plotW / (n - 1);

        var points = [];
        for (var i = 0; i < n; i++) {
            var value = Number(series[i].completions) || 0;
            var x = padLeft + (n <= 1 ? plotW / 2 : i * stepX);
            var y = padTop + plotH - (value / peak) * plotH;
            points.push({ x: x, y: y, value: value, label: formatDayLabel(series[i].date) });
        }

        var linePath = points.map(function (p, idx) {
            return (idx === 0 ? 'M' : 'L') + p.x.toFixed(1) + ' ' + p.y.toFixed(1);
        }).join(' ');

        var areaPath = linePath
            + ' L' + points[points.length - 1].x.toFixed(1) + ' ' + (padTop + plotH).toFixed(1)
            + ' L' + points[0].x.toFixed(1) + ' ' + (padTop + plotH).toFixed(1)
            + ' Z';

        var gridLines = '';
        for (var g = 0; g <= 3; g++) {
            var gy = padTop + (plotH * g / 3);
            var tick = Math.round(peak * (1 - g / 3));
            gridLines += '<line x1="' + padLeft + '" y1="' + gy.toFixed(1)
                + '" x2="' + (padLeft + plotW) + '" y2="' + gy.toFixed(1)
                + '" stroke="var(--tct-border)" stroke-width="1" />';
            gridLines += '<text x="' + (padLeft - 8) + '" y="' + (gy + 4).toFixed(1)
                + '" text-anchor="end" font-size="11" fill="var(--tct-text-muted)">' + tick + '</text>';
        }

        var labels = '';
        var labelEvery = n > 14 ? 3 : (n > 8 ? 2 : 1);
        for (var li = 0; li < points.length; li++) {
            if (li % labelEvery !== 0 && li !== points.length - 1) {
                continue;
            }
            labels += '<text x="' + points[li].x.toFixed(1) + '" y="' + (height - 10)
                + '" text-anchor="middle" font-size="11" fill="var(--tct-text-muted)">'
                + points[li].label + '</text>';
        }

        var dots = points.map(function (p) {
            return '<circle cx="' + p.x.toFixed(1) + '" cy="' + p.y.toFixed(1)
                + '" r="3.5" fill="var(--tct-primary)" />';
        }).join('');

        var animate = prefersReducedMotion() ? '' : ' style="transition: opacity 0.3s ease"';

        svgHost.innerHTML =
            '<svg class="admin-dash-chart" viewBox="0 0 ' + width + ' ' + height
            + '" width="100%" height="' + height + '" role="img" aria-label="Biểu đồ hoạt động học theo ngày"'
            + animate + '>'
            + '<defs><linearGradient id="adminDashArea" x1="0" y1="0" x2="0" y2="1">'
            + '<stop offset="0%" stop-color="var(--tct-primary)" stop-opacity="0.28"/>'
            + '<stop offset="100%" stop-color="var(--tct-primary)" stop-opacity="0.02"/>'
            + '</linearGradient></defs>'
            + gridLines
            + '<path d="' + areaPath + '" fill="url(#adminDashArea)" />'
            + '<path d="' + linePath + '" fill="none" stroke="var(--tct-primary)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" />'
            + dots
            + labels
            + '</svg>';
    }

    function animateSkillBars(root) {
        if (prefersReducedMotion()) {
            root.querySelectorAll('[data-skill-fill]').forEach(function (el) {
                el.style.width = (el.getAttribute('data-percent') || '0') + '%';
            });
            return;
        }

        requestAnimationFrame(function () {
            root.querySelectorAll('[data-skill-fill]').forEach(function (el) {
                el.style.width = (el.getAttribute('data-percent') || '0') + '%';
            });
        });
    }

    function init() {
        var root = document.querySelector('[data-admin-dashboard]');
        if (!root) {
            return;
        }

        var chart = root.querySelector('[data-activity-chart]');
        renderLineChart(chart);
        animateSkillBars(root);

        var resizeTimer = null;
        window.addEventListener('resize', function () {
            if (resizeTimer) {
                clearTimeout(resizeTimer);
            }
            resizeTimer = setTimeout(function () {
                renderLineChart(chart);
            }, 120);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
