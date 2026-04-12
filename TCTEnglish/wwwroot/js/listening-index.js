/**
 * listening-index.js
 * Client-side logic for the TCT English Listening Index page.
 */

$(document).ready(function () {
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
    const $searchInput = $('#li-search');
    const $levelTabs = $('.level-filter-btn'); // Changed class name from btn-tab to level-filter-btn to match Speaking ref if needed, but I used btn-tab in CSS. I'll stick to my CSS classes.
    const $topicPills = $('.pill-topic');
    const $sections = $('.li-level-section');
    const $cards = $('.li-card-col'); // Each card wrapper should have this class
    const $emptyState = $('#li-no-results');
    const $totalCountText = $('#li-total-count');

    // =================================================================
    // INITIALIZATION
    // =================================================================

    function init() {
        // Initial pagination for each level section
        $sections.each(function () {
            paginateSection($(this), 1);
        });

        // Event listeners
        $searchInput.on('keyup', debounce(handleSearch, CONFIG.debounceDelay));
        
        $('.btn-tab').on('click', function() {
            const level = $(this).data('level');
            $('.btn-tab').removeClass('active');
            $(this).addClass('active');
            activeLevel = level;
            applyFilters();
        });

        $('.pill-topic').on('click', function() {
            const topic = $(this).data('topic');
            $('.pill-topic').removeClass('active');
            $(this).addClass('active');
            activeTopic = topic;
            applyFilters();
        });
    }

    // =================================================================
    // FILTERING LOGIC
    // =================================================================

    function handleSearch() {
        searchQuery = $searchInput.val().toLowerCase().trim();
        applyFilters();
    }

    function applyFilters() {
        let visibleTotal = 0;

        $sections.each(function () {
            const $section = $(this);
            const sectionLevel = $section.data('level');
            
            // Level Filtering: if level is 'all' or matches section level
            const levelMatches = (activeLevel === 'all' || activeLevel === sectionLevel);

            let visibleInLevel = 0;
            const $levelCards = $section.find('.li-card-col');

            $levelCards.each(function () {
                const $cardCol = $(this);
                const cardTitle = $cardCol.data('title').toLowerCase();
                const cardTopic = $cardCol.data('topic');

                // Topic filtering
                const topicMatches = (activeTopic === 'all' || activeTopic === cardTopic);
                
                // Search filtering
                const searchMatches = (searchQuery === '' || cardTitle.includes(searchQuery) || cardTopic.toLowerCase().includes(searchQuery));

                if (topicMatches && searchMatches) {
                    $cardCol.removeClass('card-filtered-out').addClass('card-visible');
                    visibleInLevel++;
                    visibleTotal++;
                } else {
                    $cardCol.addClass('card-filtered-out').removeClass('card-visible');
                }
            });

            // Show/Hide section based on level tab and visibility of cards
            if (levelMatches && visibleInLevel > 0) {
                $section.fadeIn(CONFIG.animations.fadeTime);
                paginateSection($section, 1); // Reset to page 1 on filter
            } else {
                $section.fadeOut(CONFIG.animations.fadeTime);
            }
        });

        // Update counts and empty state
        $totalCountText.text(`${visibleTotal} lessons`);
        if (visibleTotal === 0) {
            $emptyState.fadeIn(CONFIG.animations.fadeTime);
        } else {
            $emptyState.hide();
        }
    }

    // =================================================================
    // PAGINATION LOGIC
    // =================================================================

    function paginateSection($section, page) {
        const $visibleCards = $section.find('.li-card-col.card-visible');
        const total = $visibleCards.length;
        const totalPages = Math.ceil(total / CONFIG.itemsPerPage);

        // Hide all visible cards then show only current page
        $visibleCards.hide();
        const start = (page - 1) * CONFIG.itemsPerPage;
        const end = start + CONFIG.itemsPerPage;
        $visibleCards.slice(start, end).fadeIn(CONFIG.animations.fadeTime);

        // Build pagination UI
        const $paginationContainer = $section.find('.li-pagination');
        $paginationContainer.empty();

        if (totalPages > 1) {
            for (let i = 1; i <= totalPages; i++) {
                const $btn = $('<button>')
                    .addClass('page-link')
                    .text(i)
                    .attr('data-page', i);
                
                if (i === page) $btn.addClass('active');

                $btn.on('click', function() {
                    paginateSection($section, i);
                });

                $paginationContainer.append($btn);
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

    // Add CSS for filtering
    const style = document.createElement('style');
    style.innerHTML = `
        .card-filtered-out { display: none !important; }
        .card-visible { display: block; }
    `;
    document.head.appendChild(style);

    init();
});
