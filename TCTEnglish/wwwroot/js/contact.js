(() => {
    "use strict";

    const form = document.getElementById("contactForm");
    const submitButton = document.getElementById("contactSubmitBtn");
    const messageInput = document.getElementById("Message");
    const messageCounter = document.getElementById("messageCounter");
    const messageCurrentCount = document.getElementById("messageCurrentCount");
    const statusAlert = document.getElementById("contactStatus");
    const successAlert = document.getElementById("contactSuccessAlert");
    const validationSummary = document.getElementById("contactValidationSummary");

    if (!form || !submitButton) {
        return;
    }

    const MAX_MESSAGE_LENGTH = 2000;
    let hasAttemptedSubmit = false;

    // 1. Loading state control
    const setLoadingState = (isLoading) => {
        submitButton.classList.toggle("is-loading", isLoading);
        submitButton.disabled = isLoading;
        submitButton.setAttribute("aria-busy", isLoading ? "true" : "false");
        submitButton.setAttribute("aria-label", isLoading ? "Đang gửi tin nhắn" : "Gửi tin nhắn");
    };

    // 2. Character counter matching server UTF-16 code units (string.Length)
    const updateMessageCounter = () => {
        if (!messageInput || !messageCurrentCount) {
            return;
        }

        const count = messageInput.value ? messageInput.value.length : 0;
        messageCurrentCount.textContent = String(count);

        if (count > MAX_MESSAGE_LENGTH) {
            messageCounter?.classList.add("counter-limit-reached");
            messageInput.setAttribute("aria-invalid", "true");
        } else {
            messageCounter?.classList.remove("counter-limit-reached");
            const hasError = messageInput.classList.contains("input-validation-error");
            if (!hasError) {
                messageInput.removeAttribute("aria-invalid");
            }
        }
    };

    if (messageInput) {
        messageInput.addEventListener("input", updateMessageCounter);
        messageInput.addEventListener("change", updateMessageCounter);
        updateMessageCounter();
    }

    // 3. Mark aria-invalid on fields that have errors
    const syncValidationAria = () => {
        const inputs = form.querySelectorAll("input, select, textarea");
        inputs.forEach((input) => {
            const isInvalid = input.classList.contains("input-validation-error") ||
                Boolean(input.parentElement?.querySelector(".field-validation-error:not(:empty)")) ||
                (hasAttemptedSubmit && input.validity?.valid === false) ||
                (input === messageInput && input.value.length > MAX_MESSAGE_LENGTH);
            if (isInvalid) {
                input.setAttribute("aria-invalid", "true");
            } else {
                input.removeAttribute("aria-invalid");
            }
        });
    };
    syncValidationAria();
    form.addEventListener("input", syncValidationAria);

    // 4. Focus announcement for screen readers / keyboard users without trapping
    const announceStateOnLoad = () => {
        const target = statusAlert || successAlert || (validationSummary && validationSummary.children.length > 0 ? validationSummary : null);
        if (target) {
            target.setAttribute("tabindex", "-1");
            try {
                target.focus({ preventScroll: false });
            } catch {
                // Ignore if focus fails in unsupported test runners
            }
        }
    };
    announceStateOnLoad();

    // 5. Form submission handling
    form.addEventListener("submit", (event) => {
        // Prevent duplicate submits while already submitting
        if (submitButton.disabled) {
            event.preventDefault();
            return;
        }
        hasAttemptedSubmit = true;

        // Validate via jQuery Unobtrusive if loaded
        if (window.jQuery && typeof window.jQuery(form).valid === "function") {
            const isValid = window.jQuery(form).valid();
            syncValidationAria();
            if (!isValid) {
                event.preventDefault();
                const firstInvalid = form.querySelector(".input-validation-error, [aria-invalid='true']");
                if (firstInvalid) {
                    firstInvalid.focus();
                }
                return;
            }
        }

        // Native HTML5 validation fallback
        syncValidationAria();
        if (typeof form.checkValidity === "function" && !form.checkValidity()) {
            event.preventDefault();
            syncValidationAria();
            form.reportValidity?.();
            return;
        }

        // Check message length limit
        if (messageInput && messageInput.value.length > MAX_MESSAGE_LENGTH) {
            event.preventDefault();
            updateMessageCounter();
            messageInput.focus();
            return;
        }

        setLoadingState(true);

        window.setTimeout(() => {
            if (event.defaultPrevented) {
                setLoadingState(false);
            }
        }, 0);
    });

    // 6. Restore controls on bfcache / back navigation
    window.addEventListener("pageshow", () => {
        setLoadingState(false);
    });
})();
