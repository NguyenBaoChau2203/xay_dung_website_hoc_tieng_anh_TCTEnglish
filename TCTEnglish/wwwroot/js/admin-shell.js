/**
 * TCT English - Shared Admin Shell Interactive Controller (AS01)
 * Handles responsive sidebar drawer (<992px), backdrop, focus management,
 * Escape key, body scroll locking, and resize recovery.
 */
(function (global) {
    'use strict';

    function initAdminShell() {
        var sidebar = document.getElementById('adminSidebar');
        var toggleBtn = document.getElementById('sidebarToggle');
        var closeBtn = document.getElementById('sidebarCloseBtn');
        var backdrop = document.getElementById('adminSidebarBackdrop');

        if (!sidebar || !toggleBtn) return;

        var mql = window.matchMedia('(max-width: 991.98px)');

        function isDrawerMode() {
            return mql.matches;
        }

        function isSidebarOpen() {
            return sidebar.classList.contains('show');
        }

        function setDrawerState(open, returnFocus) {
            if (!isDrawerMode()) {
                sidebar.classList.remove('show');
                sidebar.inert = false;
                toggleBtn.setAttribute('aria-expanded', 'false');
                if (backdrop) backdrop.classList.remove('show');
                document.body.classList.remove('admin-drawer-open');
                return;
            }

            if (open) {
                sidebar.classList.add('show');
                sidebar.inert = false;
                toggleBtn.setAttribute('aria-expanded', 'true');
                if (backdrop) backdrop.classList.add('show');
                document.body.classList.add('admin-drawer-open');

                // Move focus to first focusable link/button inside drawer
                var firstFocusable = sidebar.querySelector('a, button, input');
                if (firstFocusable && typeof firstFocusable.focus === 'function') {
                    firstFocusable.focus();
                }
            } else {
                sidebar.classList.remove('show');
                sidebar.inert = true;
                toggleBtn.setAttribute('aria-expanded', 'false');
                if (backdrop) backdrop.classList.remove('show');
                document.body.classList.remove('admin-drawer-open');

                if (returnFocus && typeof toggleBtn.focus === 'function') {
                    toggleBtn.focus();
                }
            }
        }

        function syncResponsiveState() {
            if (isDrawerMode()) {
                var open = isSidebarOpen();
                sidebar.inert = !open;
                toggleBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
                if (!open && backdrop) {
                    backdrop.classList.remove('show');
                }
                if (!open) {
                    document.body.classList.remove('admin-drawer-open');
                }
            } else {
                sidebar.classList.remove('show');
                sidebar.inert = false;
                toggleBtn.setAttribute('aria-expanded', 'false');
                if (backdrop) backdrop.classList.remove('show');
                document.body.classList.remove('admin-drawer-open');
            }
        }

        // Toggle click
        toggleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            var nextOpen = !isSidebarOpen();
            setDrawerState(nextOpen, !nextOpen);
        });

        // Close button inside sidebar
        if (closeBtn) {
            closeBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                setDrawerState(false, true);
            });
        }

        // Backdrop click closes drawer
        if (backdrop) {
            backdrop.addEventListener('click', function () {
                if (isDrawerMode() && isSidebarOpen()) {
                    setDrawerState(false, true);
                }
            });
        }

        // Outside click
        document.addEventListener('click', function (e) {
            if (isDrawerMode() && isSidebarOpen()) {
                if (!sidebar.contains(e.target) && !toggleBtn.contains(e.target)) {
                    setDrawerState(false, false);
                }
            }
        });

        // ESC key closes drawer
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && isDrawerMode() && isSidebarOpen()) {
                e.preventDefault();
                setDrawerState(false, true);
            }
        });

        // Media query listener
        if (typeof mql.addEventListener === 'function') {
            mql.addEventListener('change', syncResponsiveState);
        } else if (typeof mql.addListener === 'function') {
            mql.addListener(syncResponsiveState);
        }

        // Initial sync
        syncResponsiveState();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAdminShell);
    } else {
        initAdminShell();
    }

    global.initAdminShell = initAdminShell;
})(typeof window !== 'undefined' ? window : globalThis);
