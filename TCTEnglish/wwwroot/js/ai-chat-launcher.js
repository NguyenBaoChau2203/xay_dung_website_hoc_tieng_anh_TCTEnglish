(function () {
    const QUOTA_SYNC_EVENT = 'tct-ai-launcher-quota-consumed';

    function initLauncher(root) {
        if (!root) {
            return;
        }

        const toggleButton = root.querySelector('[data-ai-toggle]');
        const closeButton = root.querySelector('[data-ai-close]');
        const backdrop = root.querySelector('[data-ai-backdrop]');
        const panel = root.querySelector('.ai-launcher-panel');
        const frame = root.querySelector('[data-ai-frame]');
        const standardQuotaElement = root.querySelector('[data-ai-plan-quota]');
        const usageUrl = root.getAttribute('data-ai-usage-url') || '';
        let isRefreshingQuota = false;
        let lastFocusedElement = null;

        if (!toggleButton || !panel) {
            return;
        }

        function syncStandardQuotaDisplay(usedCount, dailyLimit) {
            if (!standardQuotaElement || !Number.isFinite(usedCount) || !Number.isFinite(dailyLimit) || dailyLimit <= 0) {
                return;
            }

            const nextUsed = Math.min(dailyLimit, Math.max(0, usedCount));
            standardQuotaElement.setAttribute('data-ai-plan-quota-used', String(nextUsed));
            standardQuotaElement.setAttribute('data-ai-plan-quota-limit', String(dailyLimit));
            standardQuotaElement.textContent = `Đã dùng ${nextUsed}/${dailyLimit}`;
        }

        async function refreshQuotaFromServerAsync() {
            if (!standardQuotaElement || !usageUrl || isRefreshingQuota) {
                return;
            }

            isRefreshingQuota = true;

            try {
                const response = await fetch(usageUrl, {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/json'
                    },
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    return;
                }

                const payload = await response.json();
                if (!payload || payload.isUnlimited === true) {
                    return;
                }

                const usedCount = Number.parseInt(payload.requestedToday, 10);
                const dailyLimit = Number.parseInt(payload.dailyLimit, 10);
                syncStandardQuotaDisplay(usedCount, dailyLimit);
            } catch {
                // no-op
            } finally {
                isRefreshingQuota = false;
            }
        }

        function handleFrameMessage(event) {
            if (!frame || !frame.contentWindow || event.source !== frame.contentWindow) {
                return;
            }

            if (event.origin !== window.location.origin) {
                return;
            }

            const payload = event.data;
            if (!payload || payload.type !== QUOTA_SYNC_EVENT) {
                return;
            }

            void refreshQuotaFromServerAsync();
        }

        function ensureFrameLoaded() {
            if (!frame || frame.getAttribute('src')) {
                return;
            }

            const source = frame.getAttribute('data-src');
            if (source) {
                frame.setAttribute('src', source);
                
                frame.addEventListener('load', function () {
                    try {
                        const frameDoc = frame.contentDocument || frame.contentWindow.document;
                        if (!frameDoc) {
                            return;
                        }

                        frameDoc.addEventListener('keydown', function (event) {
                            if (!root.classList.contains('is-open')) {
                                return;
                            }

                            if (event.key === 'Escape') {
                                event.preventDefault();
                                closeLauncher();
                                return;
                            }

                            if (event.key === 'Tab') {
                                const parentFocusableElements = getFocusableElements();
                                if (parentFocusableElements.length === 0) {
                                    return;
                                }

                                const frameFocusableElements = getFocusableElementsInside(frameDoc);
                                if (frameFocusableElements.length === 0) {
                                    if (event.shiftKey) {
                                        event.preventDefault();
                                        const frameIndex = parentFocusableElements.indexOf(frame);
                                        if (frameIndex > 0) {
                                            parentFocusableElements[frameIndex - 1].focus();
                                        } else {
                                            parentFocusableElements[parentFocusableElements.length - 1].focus();
                                        }
                                    } else {
                                        event.preventDefault();
                                        parentFocusableElements[0].focus();
                                    }
                                    return;
                                }

                                const firstElement = frameFocusableElements[0];
                                const lastElement = frameFocusableElements[frameFocusableElements.length - 1];
                                const currentElement = frameDoc.activeElement;

                                if (event.shiftKey && (currentElement === firstElement || currentElement === frameDoc.body || currentElement === frameDoc.documentElement || currentElement === null)) {
                                    event.preventDefault();
                                    const frameIndex = parentFocusableElements.indexOf(frame);
                                    if (frameIndex > 0) {
                                        parentFocusableElements[frameIndex - 1].focus();
                                    } else {
                                        parentFocusableElements[parentFocusableElements.length - 1].focus();
                                    }
                                    return;
                                }

                                if (!event.shiftKey && currentElement === lastElement) {
                                    event.preventDefault();
                                    parentFocusableElements[0].focus();
                                }
                            }
                        });
                    } catch (e) {
                        // Ignore cross-origin issues
                    }
                });
            }
        }

        function openLauncher() {
            ensureFrameLoaded();
            lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
            root.classList.add('is-open');
            toggleButton.setAttribute('aria-expanded', 'true');
            panel.setAttribute('aria-hidden', 'false');
            document.body.classList.add('ai-chat-open');
            moveFocusIntoPanel();
        }

        function closeLauncher() {
            root.classList.remove('is-open');
            toggleButton.setAttribute('aria-expanded', 'false');
            panel.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('ai-chat-open');

            if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
                lastFocusedElement.focus();
            } else {
                toggleButton.focus();
            }

            lastFocusedElement = null;
        }

        function moveFocusIntoPanel() {
            const initialFocusTarget = panel.querySelector('[data-ai-initial-focus]');
            if (initialFocusTarget && !initialFocusTarget.hasAttribute('disabled')) {
                initialFocusTarget.focus();
                return;
            }

            const focusableElements = getFocusableElements();
            if (focusableElements.length > 0) {
                focusableElements[0].focus();
                return;
            }

            panel.focus();
        }

        function getFocusableElements() {
            const selector = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), iframe, [tabindex]:not([tabindex="-1"])';
            return Array.from(panel.querySelectorAll(selector)).filter(function (element) {
                return element instanceof HTMLElement
                    && !element.hasAttribute('disabled')
                    && element.getAttribute('aria-hidden') !== 'true'
                    && element.offsetParent !== null;
            });
        }

        function getFocusableElementsInside(doc) {
            const selector = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';
            return Array.from(doc.querySelectorAll(selector)).filter(function (element) {
                return element instanceof HTMLElement
                    && !element.hasAttribute('disabled')
                    && element.getAttribute('aria-hidden') !== 'true'
                    && element.offsetParent !== null;
            });
        }

        function trapFocus(event) {
            if (event.key !== 'Tab') {
                return;
            }

            const focusableElements = getFocusableElements();
            if (focusableElements.length === 0) {
                event.preventDefault();
                panel.focus();
                return;
            }

            const firstElement = focusableElements[0];
            const lastElement = focusableElements[focusableElements.length - 1];
            const currentElement = document.activeElement;

            if (event.shiftKey && currentElement === firstElement) {
                if (firstElement === frame) {
                    return;
                }
                event.preventDefault();
                if (lastElement === frame) {
                    try {
                        const frameDoc = frame.contentDocument || frame.contentWindow.document;
                        if (frameDoc) {
                            const innerFocusables = getFocusableElementsInside(frameDoc);
                            if (innerFocusables.length > 0) {
                                innerFocusables[innerFocusables.length - 1].focus();
                                return;
                            }
                        }
                    } catch (e) {
                        // ignore cross-origin
                    }
                }
                lastElement.focus();
                return;
            }

            if (!event.shiftKey && currentElement === lastElement) {
                if (lastElement === frame) {
                    return;
                }
                event.preventDefault();
                firstElement.focus();
            }
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

        panel.addEventListener('keydown', function (event) {
            if (!root.classList.contains('is-open')) {
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeLauncher();
                return;
            }

            trapFocus(event);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && root.classList.contains('is-open')) {
                event.preventDefault();
                closeLauncher();
            }
        });

        window.addEventListener('message', handleFrameMessage);
    }

    document.addEventListener('DOMContentLoaded', function () {
        const launchers = document.querySelectorAll('[data-ai-launcher]');
        launchers.forEach(initLauncher);
    });
})();
