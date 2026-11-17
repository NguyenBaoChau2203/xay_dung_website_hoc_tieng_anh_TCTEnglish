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
            unitBadge.textContent = "thẻ / ngày";
        } else if (val.indexOf("speaking") !== -1 || val.indexOf("nói") !== -1) {
            unitBadge.textContent = "video / ngày";
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
        if (typeof applyBadgeFilter === "function") {
            try {
                var urlGrp = new URLSearchParams(window.location.search).get("badgeGroup")
                    || (window.sessionStorage ? window.sessionStorage.getItem("goals_badge_group") : null);
                if (urlGrp) applyBadgeFilter(urlGrp, false);
            } catch (e) {
                // Ignore
            }
        }
    });

    // ═══════════════ BADGE SHOWCASE: TIER FILTERS & ACCESSIBLE DETAILS ═══════════════
    var badgeFilterButtons = Array.prototype.slice.call(document.querySelectorAll(".goals-badge-filter-btn"));
    var badgeTiles = Array.prototype.slice.call(document.querySelectorAll(".goals-badge-tile"));
    var badgeEmptyState = document.getElementById("badgeFilterEmptyState");
    var badgeDetailModalEl = document.getElementById("badgeDetailModal");
    var lastFocusedBadgeTile = null;

    function applyBadgeFilter(filterKey, updateState) {
        if (!badgeFilterButtons.length) return;
        var activeKey = badgeFilterButtons.some(function (btn) {
            return btn.getAttribute("data-badge-filter") === filterKey;
        }) ? filterKey : "all";

        badgeFilterButtons.forEach(function (btn) {
            var isCurrent = btn.getAttribute("data-badge-filter") === activeKey;
            btn.classList.toggle("is-active", isCurrent);
            btn.classList.toggle("btn-primary", isCurrent);
            btn.classList.toggle("btn-outline-secondary", !isCurrent);
            btn.setAttribute("aria-selected", isCurrent ? "true" : "false");
            btn.setAttribute("tabindex", isCurrent ? "0" : "-1");
        });

        var visibleCount = 0;
        badgeTiles.forEach(function (tile) {
            var group = tile.getAttribute("data-badge-group");
            var matches = (activeKey === "all" || group === activeKey);
            tile.classList.toggle("d-none", !matches);
            if (matches) {
                visibleCount++;
            }
        });

        if (badgeEmptyState) {
            badgeEmptyState.classList.toggle("d-none", visibleCount > 0);
        }

        if (updateState) {
            try {
                if (window.sessionStorage) {
                    window.sessionStorage.setItem("goals_badge_group", activeKey);
                }
                var currentUrl = new URL(window.location.href);
                if (activeKey === "all") {
                    currentUrl.searchParams.delete("badgeGroup");
                } else {
                    currentUrl.searchParams.set("badgeGroup", activeKey);
                }
                if (window.history && window.history.replaceState) {
                    window.history.replaceState({ badgeGroup: activeKey }, "", currentUrl.toString());
                }
            } catch (e) {
                // Ignore storage/history exceptions in restricted environments
            }
        }
    }

    if (badgeFilterButtons.length > 0) {
        badgeFilterButtons.forEach(function (btn, index) {
            btn.addEventListener("click", function () {
                var filterKey = btn.getAttribute("data-badge-filter");
                applyBadgeFilter(filterKey, true);
            });

            // Automatic activation and one tab stop for the badge-group tablist.
            btn.addEventListener("keydown", function (e) {
                var nextIndex = -1;
                if (e.key === "ArrowRight") {
                    nextIndex = (index + 1) % badgeFilterButtons.length;
                } else if (e.key === "ArrowLeft") {
                    nextIndex = (index - 1 + badgeFilterButtons.length) % badgeFilterButtons.length;
                } else if (e.key === "Home") {
                    nextIndex = 0;
                } else if (e.key === "End") {
                    nextIndex = badgeFilterButtons.length - 1;
                }
                if (nextIndex >= 0) {
                    e.preventDefault();
                    badgeFilterButtons[nextIndex].focus();
                    var nextKey = badgeFilterButtons[nextIndex].getAttribute("data-badge-filter");
                    applyBadgeFilter(nextKey, true);
                }
            });
        });

        // Restore initial selection from URL or sessionStorage
        var initialGroup = null;
        try {
            var urlParams = new URLSearchParams(window.location.search);
            initialGroup = urlParams.get("badgeGroup");
            if (!initialGroup && window.sessionStorage) {
                initialGroup = window.sessionStorage.getItem("goals_badge_group");
            }
        } catch (e) {
            // Ignore
        }
        applyBadgeFilter(initialGroup, false);

        window.addEventListener("popstate", function (e) {
            var popGroup = (e.state && e.state.badgeGroup) || (new URLSearchParams(window.location.search).get("badgeGroup")) || "all";
            applyBadgeFilter(popGroup, false);
        });
    }

    // Accessible Badge Detail Modal Display
    function openBadgeDetail(tile) {
        if (!badgeDetailModalEl || !tile) return;
        lastFocusedBadgeTile = tile;

        var name = tile.getAttribute("data-badge-name") || "";
        var desc = tile.getAttribute("data-badge-desc") || "";
        var icon = tile.getAttribute("data-badge-icon") || "fa-solid fa-award";
        var groupLabel = tile.getAttribute("data-badge-group-label") || "";
        var statusText = tile.getAttribute("data-badge-status-text") || "";
        var isUnlocked = tile.getAttribute("data-badge-unlocked") === "true";
        var isRedacted = tile.getAttribute("data-badge-redacted") === "true";
        var hasProgress = tile.getAttribute("data-badge-has-progress") === "true";
        var progressLabel = tile.getAttribute("data-badge-progress-label") || "";
        var progressPercent = tile.getAttribute("data-badge-progress-percent") || "";
        var awardedDate = tile.getAttribute("data-badge-awarded") || "";
        var isRetroactive = tile.getAttribute("data-badge-retroactive") === "true";

        var titleEl = document.getElementById("badgeDetailModalTitle");
        var descEl = document.getElementById("badgeDetailDesc");
        var groupBadgeEl = document.getElementById("badgeDetailGroupBadge");
        var statusBadgeEl = document.getElementById("badgeDetailStatusBadge");
        var iconBoxEl = document.getElementById("badgeDetailIconBox");
        var iconEl = document.getElementById("badgeDetailIcon");
        var progressWrapEl = document.getElementById("badgeDetailProgressWrap");
        var metricLabelEl = document.getElementById("badgeDetailMetricLabel");
        var progressTextEl = document.getElementById("badgeDetailProgressText");
        var progressBarEl = document.getElementById("badgeDetailProgressBar");
        var progressFillEl = document.getElementById("badgeDetailProgressFill");
        var awardedWrapEl = document.getElementById("badgeDetailAwardedWrap");
        var awardedTextEl = document.getElementById("badgeDetailAwardedText");

        if (titleEl) titleEl.textContent = name;
        if (descEl) descEl.textContent = desc;
        if (groupBadgeEl) groupBadgeEl.textContent = groupLabel;
        if (statusBadgeEl) {
            statusBadgeEl.textContent = statusText;
            statusBadgeEl.className = isUnlocked
                ? "badge rounded-pill bg-success-subtle text-success border border-success-subtle small"
                : (isRedacted
                    ? "badge rounded-pill bg-secondary-subtle text-secondary border small"
                    : "badge rounded-pill bg-light text-secondary border small");
        }

        if (iconBoxEl && iconEl) {
            iconBoxEl.className = "badge-detail-icon-box mx-auto mb-3 " +
                (isUnlocked ? "badge-detail-icon-box--unlocked" : (isRedacted ? "badge-detail-icon-box--mystery" : "badge-detail-icon-box--locked"));
            iconEl.className = isRedacted ? "fa-solid fa-question" : (isUnlocked ? icon : "fa-solid fa-lock");
        }

        // Progress section (strictly hidden if unlocked or mystery redacted or unknown progress)
        if (progressWrapEl) {
            if (!isUnlocked && !isRedacted && hasProgress && progressPercent !== "") {
                progressWrapEl.classList.remove("d-none");
                if (metricLabelEl) metricLabelEl.textContent = progressLabel;
                if (progressTextEl) progressTextEl.textContent = progressPercent + "%";
                if (progressBarEl) progressBarEl.setAttribute("aria-valuenow", progressPercent);
                if (progressFillEl) progressFillEl.style.width = Math.min(parseInt(progressPercent, 10) || 0, 100) + "%";
            } else {
                progressWrapEl.classList.add("d-none");
            }
        }

        // Awarded Date section
        if (awardedWrapEl && awardedTextEl) {
            if (isUnlocked) {
                awardedWrapEl.classList.remove("d-none");
                if (isRetroactive) {
                    awardedTextEl.textContent = "Đã ghi nhận thành tích trước đây";
                } else if (awardedDate) {
                    awardedTextEl.textContent = "Đạt ngày: " + awardedDate + " (giờ Việt Nam)";
                } else {
                    awardedTextEl.textContent = "Đã mở khóa thành công";
                }
            } else {
                awardedWrapEl.classList.add("d-none");
            }
        }

        if (modalApi && badgeDetailModalEl) {
            modalApi.getOrCreateInstance(badgeDetailModalEl).show();
        }
    }

    badgeTiles.forEach(function (tile) {
        tile.addEventListener("click", function () {
            openBadgeDetail(tile);
        });
        tile.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                openBadgeDetail(tile);
            }
        });
    });

    if (badgeDetailModalEl) {
        badgeDetailModalEl.addEventListener("hidden.bs.modal", function () {
            if (lastFocusedBadgeTile && typeof lastFocusedBadgeTile.focus === "function") {
                lastFocusedBadgeTile.focus();
            }
        });
    }

    // Feedback Toast notification handling
    var goalUpdateToast = document.getElementById("goalUpdateToast");
    if (goalUpdateToast) {
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
    }
});
