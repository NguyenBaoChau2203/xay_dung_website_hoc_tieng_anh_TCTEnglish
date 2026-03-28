document.addEventListener('DOMContentLoaded', function () {
    const levelCards = Array.from(document.querySelectorAll('[data-writing-level-card]'));
    const contentLinks = Array.from(document.querySelectorAll('[data-writing-content-link]'));
    const elSelectedLevelCopy = document.querySelector('[data-writing-selected-level-copy]');

    if (levelCards.length === 0 || contentLinks.length === 0 || !elSelectedLevelCopy) {
        return;
    }

    function setSelectedLevel(levelKey, levelTitle, levelHref) {
        levelCards.forEach(function (card) {
            const isSelected = card.getAttribute('data-level-key') === levelKey;
            card.classList.toggle('is-selected', isSelected);
            card.setAttribute('aria-current', isSelected ? 'true' : 'false');
        });

        elSelectedLevelCopy.textContent = `Chế độ ${levelTitle} đã sẵn sàng. Hãy chọn dạng bài để bắt đầu luyện viết.`;

        contentLinks.forEach(function (link) {
            const href = link.getAttribute('href');
            if (!href) {
                return;
            }

            const url = new URL(href, window.location.origin);
            url.searchParams.set('level', levelKey);
            link.setAttribute('href', `${url.pathname}${url.search}`);
        });

        if (levelHref) {
            const historyUrl = new URL(levelHref, window.location.origin);
            window.history.replaceState({ writingLevel: levelKey }, '', `${historyUrl.pathname}${historyUrl.search}`);
        }
    }

    levelCards.forEach(function (card) {
        card.addEventListener('click', function (event) {
            event.preventDefault();

            const levelKey = card.getAttribute('data-level-key');
            const levelTitle = card.getAttribute('data-level-title');
            const levelHref = card.getAttribute('href');

            if (!levelKey || !levelTitle) {
                return;
            }

            setSelectedLevel(levelKey, levelTitle, levelHref);
        });
    });
});
