document.addEventListener("DOMContentLoaded", function () {
    var modalElement = document.getElementById("goalEditorModal");
    var shouldOpenGoalEditor = modalElement?.dataset.openOnLoad === "true";
    var modalApi = window.bootstrap?.Modal;
    var lastFocusedGoalTrigger = null;

    document.querySelectorAll('[data-bs-target="#goalEditorModal"]').forEach(function (triggerButton) {
        triggerButton.addEventListener("click", function () {
            lastFocusedGoalTrigger = triggerButton;
        });
    });

    if (modalElement) {
        modalElement.addEventListener("shown.bs.modal", function () {
            var goalInput = modalElement.querySelector(".goal-input")
                || modalElement.querySelector("input:not([type='hidden']), select, textarea, button");
            goalInput?.focus();
        });

        modalElement.addEventListener("hidden.bs.modal", function () {
            (lastFocusedGoalTrigger || document.querySelector('[data-testid="goal-header-cta"]'))?.focus();
        });
    }

    if (shouldOpenGoalEditor && modalElement && modalApi) {
        modalApi.getOrCreateInstance(modalElement).show();
    }

    var goalUpdateToast = document.getElementById("goalUpdateToast");
    if (!goalUpdateToast) {
        return;
    }

    var hideToast = function () {
        goalUpdateToast.classList.remove("is-visible");
    };

    window.setTimeout(function () {
        goalUpdateToast.classList.add("is-visible");
    }, 10);

    window.setTimeout(hideToast, 3800);

    document.querySelectorAll('[data-goals-dismiss="goalUpdateToast"]').forEach(function (dismissButton) {
        dismissButton.addEventListener("click", hideToast);
    });
});
