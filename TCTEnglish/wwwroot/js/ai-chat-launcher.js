(function () {
    function initLauncher(root) {
        if (!root) {
            return;
        }

        const toggleButton = root.querySelector('[data-ai-toggle]');
        const closeButton = root.querySelector('[data-ai-close]');
        const backdrop = root.querySelector('[data-ai-backdrop]');
        const panel = root.querySelector('.ai-launcher-panel');
        const frame = root.querySelector('[data-ai-frame]');

        if (!toggleButton || !panel) {
            return;
        }

        function ensureFrameLoaded() {
            if (!frame || frame.getAttribute('src')) {
                return;
            }

            const source = frame.getAttribute('data-src');
            if (source) {
                frame.setAttribute('src', source);
            }
        }

        function openLauncher() {
            ensureFrameLoaded();
            root.classList.add('is-open');
            toggleButton.setAttribute('aria-expanded', 'true');
            panel.setAttribute('aria-hidden', 'false');
            document.body.classList.add('ai-chat-open');
        }

        function closeLauncher() {
            root.classList.remove('is-open');
            toggleButton.setAttribute('aria-expanded', 'false');
            panel.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('ai-chat-open');
        }

        toggleButton.addEventListener('click', function () {
            if (root.classList.contains('is-open')) {
                closeLauncher();
                return;
            }

            openLauncher();
        });

        if (closeButton) {
            closeButton.addEventListener('click', closeLauncher);
        }

        if (backdrop) {
            backdrop.addEventListener('click', closeLauncher);
        }

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && root.classList.contains('is-open')) {
                closeLauncher();
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        const launchers = document.querySelectorAll('[data-ai-launcher]');
        launchers.forEach(initLauncher);
    });
})();
