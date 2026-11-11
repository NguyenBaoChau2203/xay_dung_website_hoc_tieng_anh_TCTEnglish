/**
 * goals.js — TCT English Goals & Learning Progress UI interactions
 * Handles modal lifecycle, accessible focus trapping/return,
 * quick preset chips, toast dismissal, and double-submit prevention.
 */
document.addEventListener("DOMContentLoaded", function () {
    var modalElement = document.getElementById("goalEditorModal");
    var formElement = document.getElementById("goalEditorForm");
    var submitButton = document.getElementById("goalSubmitBtn");
    var targetInput = document.getElementById("goalTargetInput");
    var areaSelect = document.getElementById("goalAreaSelect");
    var unitBadge = document.getElementById("goalUnitBadge");
    var decBtn = document.getElementById("goalTargetDecBtn");
    var incBtn = document.getElementById("goalTargetIncBtn");
    var modalApi = window.bootstrap?.Modal;
    var lastFocusedGoalTrigger = null;

    function updateActivePreset(val) {
        var strVal = String(val !== undefined && val !== null ? val : (targetInput ? targetInput.value : "")).trim();
        document.querySelectorAll(".goal-preset-btn").forEach(function (btn) {
            if (btn.getAttribute("data-preset") === strVal) {
                btn.classList.add("is-active");
            } else {
                btn.classList.remove("is-active");
            }
        });
    }

    function updateUnitBadge() {
        if (!areaSelect || !unitBadge) return;
        var val = (areaSelect.value || "").toLowerCase();
        if (val.indexOf("vocabulary") !== -1 || val.indexOf("từ") !== -1) {
            unitBadge.textContent = "từ / ngày";
        } else {
            unitBadge.textContent = "bài / ngày";
        }
    }

    if (areaSelect) {
        areaSelect.addEventListener("change", updateUnitBadge);
        updateUnitBadge();
    }

    if (targetInput) {
        targetInput.addEventListener("input", function () {
            updateActivePreset(targetInput.value);
        });
        targetInput.addEventListener("change", function () {
            updateActivePreset(targetInput.value);
        });
        updateActivePreset(targetInput.value);
    }

    if (decBtn && targetInput) {
        decBtn.addEventListener("click", function () {
            var cur = parseInt(targetInput.value, 10);
            if (isNaN(cur)) cur = 0;
            var next = (cur > 0 && cur < 5) ? (cur - 1) : Math.max(0, cur - 5);
            targetInput.value = next;
            targetInput.focus();
            targetInput.dispatchEvent(new Event("change", { bubbles: true }));
            targetInput.dispatchEvent(new Event("input", { bubbles: true }));
        });
    }

    if (incBtn && targetInput) {
        incBtn.addEventListener("click", function () {
            var cur = parseInt(targetInput.value, 10);
            if (isNaN(cur)) cur = 0;
            var next = (cur > 0 && cur < 5) ? (cur + 1) : Math.min(500, cur + 5);
            targetInput.value = next;
            targetInput.focus();
            targetInput.dispatchEvent(new Event("change", { bubbles: true }));
            targetInput.dispatchEvent(new Event("input", { bubbles: true }));
        });
    }

    // Track trigger element for focus return
    document.querySelectorAll('[data-bs-target="#goalEditorModal"]').forEach(function (triggerButton) {
        triggerButton.addEventListener("click", function () {
            lastFocusedGoalTrigger = triggerButton;
        });
    });

    function setButtonPending(btn, busy) {
        if (!btn) return;
        btn.disabled = busy;
        if (busy) {
            btn.setAttribute("aria-busy", "true");
        } else {
            if (typeof btn.removeAttribute === "function") {
                btn.removeAttribute("aria-busy");
            } else if (btn.attributes) {
                delete btn.attributes["aria-busy"];
            }
        }
    }

    function resetSubmittingState() {
        if (formElement) {
            formElement._inFlight = false;
        }
        setButtonPending(submitButton, false);
    }

    if (modalElement) {
        // Auto-focus input on modal display
        modalElement.addEventListener("shown.bs.modal", function () {
            var inputToFocus = targetInput || modalElement.querySelector(".goal-input")
                || modalElement.querySelector("input:not([type='hidden']), select, textarea, button");
            inputToFocus?.focus();
            if (inputToFocus && typeof inputToFocus.select === "function") {
                inputToFocus.select();
            }
            updateActivePreset();
            updateUnitBadge();
        });

        // Accessible return focus on modal close
        modalElement.addEventListener("hidden.bs.modal", function () {
            var targetToFocus = lastFocusedGoalTrigger || document.querySelector('[data-testid="goal-header-cta"]');
            targetToFocus?.focus();
            resetSubmittingState();
        });

        // Auto-open modal if server indicated error re-render
        var shouldOpenGoalEditor = modalElement.dataset.openOnLoad === "true";
        if (shouldOpenGoalEditor && modalApi) {
            modalApi.getOrCreateInstance(modalElement).show();
        }
    }

    // Quick preset buttons (e.g., 5, 10, 20, 30, 50)
    document.querySelectorAll(".goal-preset-btn").forEach(function (presetBtn) {
        presetBtn.addEventListener("click", function () {
            var val = presetBtn.getAttribute("data-preset");
            if (targetInput && val !== null) {
                targetInput.value = val;
                targetInput.focus();
                targetInput.dispatchEvent(new Event("change", { bubbles: true }));
                targetInput.dispatchEvent(new Event("input", { bubbles: true }));
            }
        });
    });

    // Double-submit prevention on valid form submission
    if (formElement && submitButton) {
        formElement.addEventListener("submit", function (e) {
            if (e && e.defaultPrevented) {
                return;
            }

            // Check native HTML5 validation
            if (typeof formElement.checkValidity === "function" && !formElement.checkValidity()) {
                return; // Let browser show validation popups
            }

            // Check jQuery validation if loaded
            if (window.jQuery && typeof window.jQuery(formElement).valid === "function") {
                if (!window.jQuery(formElement).valid()) {
                    return;
                }
            }

            if (formElement._inFlight) {
                if (e && e.preventDefault) e.preventDefault();
                return;
            }

            formElement._inFlight = true;
            setButtonPending(submitButton, true);

            // If a later validator cancels submission, roll back pending state
            Promise.resolve().then(function () {
                if (e && e.defaultPrevented) {
                    resetSubmittingState();
                }
            });
        });
    }

    // Recover state on BFCache restore (back/forward navigation)
    window.addEventListener("pageshow", function () {
        resetSubmittingState();
    });

    // Feedback Toast notification handling
    var goalUpdateToast = document.getElementById("goalUpdateToast");
    if (!goalUpdateToast) {
        return;
    }

    var hideToast = function () {
        goalUpdateToast.classList.remove("is-visible");
    };

    // Auto-reveal with gentle transition
    window.setTimeout(function () {
        goalUpdateToast.classList.add("is-visible");
    }, 10);

    // Auto-hide after 3800ms
    var toastTimeout = window.setTimeout(hideToast, 3800);

    // Manual dismissal
    document.querySelectorAll('[data-goals-dismiss="goalUpdateToast"]').forEach(function (dismissButton) {
        dismissButton.addEventListener("click", function () {
            window.clearTimeout(toastTimeout);
            hideToast();
        });
    });
});
