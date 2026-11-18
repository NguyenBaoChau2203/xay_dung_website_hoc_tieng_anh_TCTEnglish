/* /Premium: keeps the summary and the pay button in step with the chosen plan and method, and guards against a double submit.
   All arithmetic is in billing-core.js. Without JavaScript the form still works: the server renders the preselected choice
   and the button reads "Thanh toán". */
(function () {
    'use strict';

    var core = window.TctBilling;
    var form = document.querySelector('[data-tb-pricing]');
    if (!core || !form) { return; }

    var submit = form.querySelector('[data-tb-submit]');
    var submitLabel = submit ? submit.querySelector('[data-tb-field="button"]') : null;
    var submitIcon = submit ? submit.querySelector('[data-tb-icon]') : null;
    var submitting = false;

    function field(name) { return form.querySelector('[data-tb-field="' + name + '"]'); }
    function setText(name, text) { var el = field(name); if (el && el.textContent !== text) { el.textContent = text; } }
    function checked(name) { return form.querySelector('input[name="' + name + '"]:checked'); }

    function update() {
        if (submitting) { return; }
        var planInput = checked('PlanCode');
        if (!planInput) { return; }
        var plan = planInput.closest('[data-tb-plan]');
        var methodInput = checked('Provider');
        var method = methodInput ? methodInput.closest('[data-tb-method]') : null;
        var s = core.summary({
            planName: plan.getAttribute('data-name'),
            price: Number(plan.getAttribute('data-price')),
            days: Number(plan.getAttribute('data-days')),
            nowMs: Number(form.getAttribute('data-now-ms')) || Date.now(),
            currentEndMs: form.hasAttribute('data-current-end-ms') ? Number(form.getAttribute('data-current-end-ms')) : null,
            providerName: method ? method.getAttribute('data-name') : ''
        });
        setText('plan', s.plan);
        setText('validity', s.validity);
        setText('total', s.total);
        setText('button', s.button);
        if (s.note) { setText('note', s.note); }
    }

    function reset() {
        submitting = false;
        if (submit) {
            submit.disabled = false;
            submit.removeAttribute('aria-busy');
        }
        if (submitIcon) { submitIcon.className = 'fa-solid fa-lock me-2'; }
        update();
    }

    form.addEventListener('change', update);

    form.addEventListener('submit', function (event) {
        if (submitting) { event.preventDefault(); return; }
        submitting = true;
        if (submit) {
            // Disable after the browser has read the form, so the request itself is not affected.
            window.setTimeout(function () {
                submit.disabled = true;
                submit.setAttribute('aria-busy', 'true');
            }, 0);
        }
        if (submitIcon) { submitIcon.className = 'spinner-border spinner-border-sm me-2'; }
        if (submitLabel) { submitLabel.textContent = 'Đang chuyển đến cổng thanh toán…'; }
    });

    // Back button: the browser may restore the page as it was left (disabled button); make it usable again.
    window.addEventListener('pageshow', function (event) { if (event.persisted) { reset(); } });

    var error = document.querySelector('[data-tb-error]');
    if (error) { error.focus(); }

    update();
})();
