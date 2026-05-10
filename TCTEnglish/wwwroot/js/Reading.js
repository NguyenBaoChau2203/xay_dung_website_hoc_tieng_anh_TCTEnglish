/**
 * Reading Practice Index Page Logic
 * Handles searching and filtering (Level, Status)
 */
(function() {
    "use strict";

    // Define reset function in global scope immediately
    window.resetFilters = function() {
        const searchInput = document.getElementById('readingSearch');
        if (searchInput) searchInput.value = '';
        
        const filterBtns = document.querySelectorAll('.filter-chip');
        filterBtns.forEach(b => b.classList.remove('active'));
        if (filterBtns[0]) filterBtns[0].classList.add('active');
        
        const statusBtns = document.querySelectorAll('.status-btn');
        statusBtns.forEach(b => b.classList.remove('active'));

        // Trigger filter update
        window.dispatchEvent(new CustomEvent('tct:filter-reset'));
    };

    document.addEventListener('DOMContentLoaded', function () {
        const searchInput = document.getElementById('readingSearch');
        const filterBtns = document.querySelectorAll('.filter-chip');
        const statusBtns = document.querySelectorAll('.status-btn');
        const items = document.querySelectorAll('.reading-item-container');
        const noResults = document.getElementById('noResults');

        let currentLevel = 'all';
        let currentStatus = 'all'; 
        let searchText = '';

        function filterItems() {
            let visibleCount = 0;
            const term = (searchInput ? searchInput.value : '').toLowerCase().trim();

            // First pass: Filter all items
            items.forEach(item => {
                const title = item.getAttribute('data-title') || '';
                const level = item.getAttribute('data-level') || '';
                const isCompleted = item.getAttribute('data-completed') === 'true';
                const isInProgress = item.getAttribute('data-inprogress') === 'true';

                const matchesSearch = title.includes(term);
                const matchesLevel = currentLevel === 'all' || level === currentLevel;

                let matchesStatus = true;
                if (currentStatus === 'completed') matchesStatus = isCompleted;
                if (currentStatus === 'inprogress') matchesStatus = isInProgress;

                if (matchesSearch && matchesLevel && matchesStatus) {
                    item.style.setProperty('display', 'block', 'important');
                    visibleCount++;
                } else {
                    item.style.setProperty('display', 'none', 'important');
                }
            });

            // Second pass: Toggle level groups based on child visibility
            const groups = document.querySelectorAll('.level-group');
            groups.forEach(group => {
                const groupLevel = group.getAttribute('data-group-level');
                const visibleInGroup = group.querySelectorAll('.reading-item-container[style*="display: block"]').length;
                
                // If filtering by a specific level, hide other groups even if they have items
                const levelMatchesFilter = currentLevel === 'all' || groupLevel === currentLevel;

                if (visibleInGroup > 0 && levelMatchesFilter) {
                    group.classList.remove('d-none');
                } else {
                    group.classList.add('d-none');
                }
            });

            if (noResults) {
                if (visibleCount > 0) {
                    noResults.classList.add('d-none');
                } else {
                    noResults.classList.remove('d-none');
                }
            }
        }

        // Search event
        if (searchInput) {
            searchInput.addEventListener('input', filterItems);
        }

        // Level filter event
        filterBtns.forEach(btn => {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                filterBtns.forEach(b => b.classList.remove('active'));
                this.classList.add('active');
                currentLevel = this.getAttribute('data-filter') || 'all';
                filterItems();
            });
        });

        // Status filter event (Toggle)
        statusBtns.forEach(btn => {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                const status = this.getAttribute('data-status');
                
                if (this.classList.contains('active')) {
                    this.classList.remove('active');
                    currentStatus = 'all';
                } else {
                    statusBtns.forEach(b => b.classList.remove('active'));
                    this.classList.add('active');
                    currentStatus = status;
                }
                
                filterItems();
            });
        });

        // Listen for internal reset event
        window.addEventListener('tct:filter-reset', () => {
            currentLevel = 'all';
            currentStatus = 'all';
            filterItems();
        });

        // Initial filter run (in case search box has persistent value)
        filterItems();
    });
})();