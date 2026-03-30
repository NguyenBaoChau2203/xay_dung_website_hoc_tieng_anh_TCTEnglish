document.addEventListener("DOMContentLoaded", function () {
    var modalElement = document.getElementById("goalEditorModal");
    var shouldOpenGoalEditor = modalElement?.dataset.openOnLoad === "true";

    if (shouldOpenGoalEditor && modalElement && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(modalElement).show();
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
