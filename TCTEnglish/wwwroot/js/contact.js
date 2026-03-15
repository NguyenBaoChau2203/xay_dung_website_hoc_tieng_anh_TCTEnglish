(() => {
    const form = document.getElementById("contactForm");
    const submitButton = document.getElementById("contactSubmitBtn");

    if (!form || !submitButton) {
        return;
    }

    const setLoadingState = (isLoading) => {
        submitButton.classList.toggle("is-loading", isLoading);
        submitButton.disabled = isLoading;
        submitButton.setAttribute("aria-busy", isLoading ? "true" : "false");
    };

    form.addEventListener("submit", (event) => {
        if (submitButton.disabled) {
            event.preventDefault();
            return;
        }

        if (window.jQuery && typeof window.jQuery(form).valid === "function" && !window.jQuery(form).valid()) {
            return;
        }

        setLoadingState(true);

        window.setTimeout(() => {
            if (event.defaultPrevented) {
                setLoadingState(false);
            }
        }, 0);
    });

    window.addEventListener("pageshow", () => {
        setLoadingState(false);
    });
})();
