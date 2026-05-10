/**
 * listening-index.js
 * Client-side logic for the TCT English Listening Index page.
 */

document.addEventListener('DOMContentLoaded', function () {
    const CONFIG = {
        debounceDelay: 300,
        itemsPerPage: 6,
        animations: {
            fadeTime: 300
        }
    };

    let activeLevel = 'all';
    let activeTopic = 'all';
    let searchQuery = '';

    // Cache selectors
    const searchInput = document.getElementById('li-search');
    const levelTabs = document.querySelectorAll('.btn-tab');
    const topicPills = document.querySelectorAll('.pill-topic');
    const sections = document.querySelectorAll('.li-level-section');
    const emptyState = document.getElementById('li-no-results');
    const totalCountText = document.getElementById('li-total-count');

    // =================================================================
    // INITIALIZATION
    // =================================================================

    function init() {
        // Initial pagination for each level section
        sections.forEach(function (section) {
            paginateSection(section, 1);
        });

        // Event listeners
        if (searchInput) {
            searchInput.addEventListener('keyup', debounce(handleSearch, CONFIG.debounceDelay));
        }
        
        levelTabs.forEach(tab => {
            tab.addEventListener('click', function() {
                const level = this.dataset.level;
                levelTabs.forEach(t => {
                    t.classList.remove('active');
                    t.setAttribute('aria-pressed', 'false');
                });
                this.classList.add('active');
                this.setAttribute('aria-pressed', 'true');
                activeLevel = level;
                applyFilters();
            });
        });

        topicPills.forEach(pill => {
            pill.addEventListener('click', function() {
                const topic = this.dataset.topic;
                topicPills.forEach(p => {
                    p.classList.remove('active');
                    p.setAttribute('aria-pressed', 'false');
                });
                this.classList.add('active');
                this.setAttribute('aria-pressed', 'true');
                activeTopic = topic;
                applyFilters();
            });
        });
    }

    // =================================================================
    // FILTERING LOGIC
    // =================================================================

    function handleSearch() {
        searchQuery = searchInput.value.toLowerCase().trim();
        applyFilters();
    }

    function applyFilters() {
        let visibleTotal = 0;

        sections.forEach(function (section) {
            const sectionLevel = section.dataset.level;
            
            // Level Filtering: if level is 'all' or matches section level
            const levelMatches = (activeLevel === 'all' || activeLevel === sectionLevel);

            let visibleInLevel = 0;
            const levelCards = section.querySelectorAll('.li-card-col');

            levelCards.forEach(function (cardCol) {
                const cardTitle = cardCol.dataset.title.toLowerCase();
                const cardTopic = cardCol.dataset.topic;

                // Topic filtering
                const topicMatches = (activeTopic === 'all' || activeTopic === cardTopic);
                
                // Search filtering
                const searchMatches = (searchQuery === '' || cardTitle.includes(searchQuery) || cardTopic.toLowerCase().includes(searchQuery));

                if (topicMatches && searchMatches) {
                    cardCol.classList.remove('card-filtered-out');
                    cardCol.classList.add('card-visible');
                    visibleInLevel++;
                    visibleTotal++;
                } else {
                    cardCol.classList.add('card-filtered-out');
                    cardCol.classList.remove('card-visible');
                }
            });

            // Show/Hide section based on level tab and visibility of cards
            if (levelMatches && visibleInLevel > 0) {
                section.style.display = '';
                paginateSection(section, 1); // Reset to page 1 on filter
            } else {
                section.style.display = 'none';
            }
        });

        // Update counts and empty state
        if (totalCountText) {
            totalCountText.textContent = `${visibleTotal} bài học`;
        }
        if (emptyState) {
            if (visibleTotal === 0) {
                emptyState.style.display = 'block';
            } else {
                emptyState.style.display = 'none';
            }
        }
    }

    // =================================================================
    // PAGINATION LOGIC
    // =================================================================

    function paginateSection(section, page) {
        const visibleCards = Array.from(section.querySelectorAll('.li-card-col.card-visible'));
        const total = visibleCards.length;
        const totalPages = Math.ceil(total / CONFIG.itemsPerPage);

        // Hide all visible cards then show only current page
        visibleCards.forEach(card => card.style.display = 'none');
        
        const start = (page - 1) * CONFIG.itemsPerPage;
        const end = start + CONFIG.itemsPerPage;
        visibleCards.slice(start, end).forEach(card => {
            card.style.display = 'block'; // basic show, no fade needed
        });

        // Build pagination UI
        const paginationContainer = section.querySelector('.li-pagination');
        if (!paginationContainer) return;
        
        paginationContainer.innerHTML = '';

        if (totalPages > 1) {
            for (let i = 1; i <= totalPages; i++) {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'page-link';
                btn.textContent = i;
                btn.dataset.page = i;
                
                if (i === page) btn.classList.add('active');

                btn.addEventListener('click', function() {
                    paginateSection(section, i);
                });

                paginationContainer.appendChild(btn);
            }
        }
    }

    // =================================================================
    // HELPERS
    // =================================================================

    function debounce(func, wait) {
        let timeout;
        return function () {
            const context = this, args = arguments;
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(context, args), wait);
        };
    }

    init();
});
