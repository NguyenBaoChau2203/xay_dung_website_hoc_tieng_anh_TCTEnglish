document.addEventListener('DOMContentLoaded', function () {
    const filterForm = document.querySelector('[data-writing-filter-form]');
    const filterSelects = document.querySelectorAll('[data-filter-select]');

    if (!filterForm || filterSelects.length === 0) {
        return;
    }

    filterSelects.forEach(function (select) {
        select.addEventListener('change', function () {
            filterForm.submit();
        });
    });
});
