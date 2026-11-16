/* TCT English landing page: progressive enhancement only.
   The page is complete without this script; it adds the phone menu (and, in later sections, the demo and motion). */
(function () {
    'use strict';

    // ------------------------------------------------------------------ phone menu
    function initNav() {
        var toggle = document.querySelector('[data-lp-nav-toggle]');
        var panel = document.getElementById('lp-nav-links');
        if (!toggle || !panel) { return; }

        function setOpen(open, restoreFocus) {
            panel.classList.toggle('is-open', open);
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            toggle.setAttribute('aria-label', open ? 'Đóng menu' : 'Mở menu');
            if (!open && restoreFocus) { toggle.focus(); }
        }

        toggle.addEventListener('click', function () {
            setOpen(!panel.classList.contains('is-open'), false);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && panel.classList.contains('is-open')) {
                setOpen(false, true);
            }
        });

        // Following an in-page link closes the panel; the browser then scrolls to the section natively.
        panel.addEventListener('click', function (event) {
            var link = event.target.closest ? event.target.closest('a') : null;
            if (link && panel.classList.contains('is-open')) { setOpen(false, false); }
        });
    }

    function init() {
        initNav();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
