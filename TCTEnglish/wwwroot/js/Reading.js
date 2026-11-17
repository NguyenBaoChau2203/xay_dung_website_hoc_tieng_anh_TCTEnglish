/**
 * Reading library — search, CEFR/topic/status filters, tempered pointer tilt.
 */
(function () {
    "use strict";

    window.resetFilters = function () {
        const searchInput = document.getElementById("readingSearch");
        if (searchInput) searchInput.value = "";

        document.querySelectorAll(".filter-chip[data-filter]").forEach(function (btn, index) {
            btn.classList.toggle("active", index === 0);
        });

        document.querySelectorAll(".topic-chip").forEach(function (btn, index) {
            btn.classList.toggle("active", index === 0);
        });

        document.querySelectorAll(".status-btn").forEach(function (btn) {
            btn.classList.remove("active");
            btn.setAttribute("aria-pressed", "false");
        });

        window.dispatchEvent(new CustomEvent("tct:filter-reset"));
    };

    function prefersReducedMotion() {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function canUseTilt() {
        return (
            !prefersReducedMotion() &&
            window.matchMedia("(hover: hover) and (pointer: fine)").matches
        );
    }

    function initTilt(cards) {
        if (!canUseTilt()) return;

        var maxTilt = 4;

        cards.forEach(function (card) {
            var visual = card.querySelector("[data-card-visual]");

            function onMove(event) {
                var rect = card.getBoundingClientRect();
                if (rect.width === 0 || rect.height === 0) return;

                var px = (event.clientX - rect.left) / rect.width;
                var py = (event.clientY - rect.top) / rect.height;
                var rotateY = (px - 0.5) * (maxTilt * 2);
                var rotateX = (0.5 - py) * (maxTilt * 2);

                card.classList.add("is-tilting");
                card.style.transform =
                    "translateY(-6px) scale(1.02) rotateX(" +
                    rotateX.toFixed(2) +
                    "deg) rotateY(" +
                    rotateY.toFixed(2) +
                    "deg)";

                if (visual) {
                    var shiftX = (px - 0.5) * 8;
                    var shiftY = (py - 0.5) * 6;
                    visual.style.transform =
                        "translate3d(" + shiftX.toFixed(1) + "px, " + shiftY.toFixed(1) + "px, 0)";
                }
            }

            function onLeave() {
                card.classList.remove("is-tilting");
                card.style.transform = "";
                if (visual) visual.style.transform = "";
            }

            card.addEventListener("pointermove", onMove);
            card.addEventListener("pointerleave", onLeave);
            card.addEventListener("blur", onLeave);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var searchInput = document.getElementById("readingSearch");
        var levelBtns = document.querySelectorAll(".filter-chip[data-filter]");
        var topicBtns = document.querySelectorAll(".topic-chip[data-topic-filter]");
        var statusBtns = document.querySelectorAll(".status-btn");
        var items = document.querySelectorAll(".reading-item-container");
        var noResults = document.getElementById("noResults");
        var cards = document.querySelectorAll("[data-reading-card]");

        var currentLevel = "all";
        var currentTopic = "all";
        var currentStatus = "all";

        function filterItems() {
            var visibleCount = 0;
            var term = (searchInput ? searchInput.value : "").toLowerCase().trim();

            items.forEach(function (item) {
                var title = item.getAttribute("data-title") || "";
                var level = item.getAttribute("data-level") || "";
                var topic = item.getAttribute("data-topic") || "";
                var isCompleted = item.getAttribute("data-completed") === "true";
                var isInProgress = item.getAttribute("data-inprogress") === "true";

                var matchesSearch = title.indexOf(term) !== -1;
                var matchesLevel = currentLevel === "all" || level === currentLevel;
                var matchesTopic = currentTopic === "all" || topic === currentTopic;

                var matchesStatus = true;
                if (currentStatus === "completed") matchesStatus = isCompleted;
                if (currentStatus === "inprogress") matchesStatus = isInProgress;

                if (matchesSearch && matchesLevel && matchesTopic && matchesStatus) {
                    item.hidden = false;
                    item.style.removeProperty("display");
                    visibleCount++;
                } else {
                    item.hidden = true;
                    item.style.setProperty("display", "none", "important");
                }
            });

            if (noResults) {
                noResults.classList.toggle("d-none", visibleCount > 0);
            }
        }

        if (searchInput) {
            searchInput.addEventListener("input", filterItems);
        }

        levelBtns.forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                levelBtns.forEach(function (b) {
                    b.classList.remove("active");
                });
                btn.classList.add("active");
                currentLevel = btn.getAttribute("data-filter") || "all";
                filterItems();
            });
        });

        topicBtns.forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                topicBtns.forEach(function (b) {
                    b.classList.remove("active");
                });
                btn.classList.add("active");
                currentTopic = btn.getAttribute("data-topic-filter") || "all";
                filterItems();
            });
        });

        statusBtns.forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                var status = btn.getAttribute("data-status");

                if (btn.classList.contains("active")) {
                    btn.classList.remove("active");
                    btn.setAttribute("aria-pressed", "false");
                    currentStatus = "all";
                } else {
                    statusBtns.forEach(function (b) {
                        b.classList.remove("active");
                        b.setAttribute("aria-pressed", "false");
                    });
                    btn.classList.add("active");
                    btn.setAttribute("aria-pressed", "true");
                    currentStatus = status;
                }

                filterItems();
            });
        });

        window.addEventListener("tct:filter-reset", function () {
            currentLevel = "all";
            currentTopic = "all";
            currentStatus = "all";
            filterItems();
        });

        initTilt(cards);
        filterItems();
    });
})();
