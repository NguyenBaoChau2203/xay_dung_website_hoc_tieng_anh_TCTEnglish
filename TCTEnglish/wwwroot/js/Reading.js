document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('readingSearch');
    const filterBtns = document.querySelectorAll('.filter-btn');
    const statusBtns = document.querySelectorAll('.filter-status');
    const items = document.querySelectorAll('.reading-item');
    const noResults = document.getElementById('noResults');

    let currentLevel = 'all';
    let currentStatus = 'all'; // all, completed, inprogress
    let searchText = '';

    function filterItems() {
        let visibleCount = 0;

        items.forEach(item => {
            const title = item.getAttribute('data-title');
            const level = item.getAttribute('data-level');
            const isCompleted = item.getAttribute('data-completed') === 'true';
            const isInProgress = item.getAttribute('data-inprogress') === 'true';

            const matchesSearch = title.includes(searchText.toLowerCase());
            const matchesLevel = currentLevel === 'all' || level === currentLevel;

            let matchesStatus = true;
            if (currentStatus === 'completed') matchesStatus = isCompleted;
            if (currentStatus === 'inprogress') matchesStatus = isInProgress;

            if (matchesSearch && matchesLevel && matchesStatus) {
                item.classList.remove('d-none');
                visibleCount++;
            } else {
                item.classList.add('d-none');
            }
        });

        noResults.classList.toggle('d-none', visibleCount > 0);
    }

    // Search event
    searchInput.addEventListener('input', (e) => {
        searchText = e.target.value;
        filterItems();
    });

    // Level filter event
    filterBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            filterBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentLevel = btn.getAttribute('data-filter');
            filterItems();
        });
    });

    // Status filter event (Toggle)
    statusBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const status = btn.getAttribute('data-status');
            if (currentStatus === status) {
                currentStatus = 'all';
                btn.classList.remove('ring-active'); // Bạn có thể thêm css class để highlight
            } else {
                currentStatus = status;
            }
            filterItems();
        });
    });
});