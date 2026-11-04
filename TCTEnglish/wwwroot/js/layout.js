document.addEventListener("DOMContentLoaded", function () {
    const searchInput = document.getElementById("globalSearchInput");

    function syncHeaderSearchPlaceholder() {
        if (!searchInput) {
            return;
        }

        const useCompactPlaceholder = window.matchMedia("(max-width: 576px)").matches;
        const defaultPlaceholder = searchInput.dataset.placeholderDefault || "Search";
        const compactPlaceholder = searchInput.dataset.placeholderCompact || "Search";

        searchInput.placeholder = useCompactPlaceholder
            ? compactPlaceholder
            : defaultPlaceholder;
    }

    syncHeaderSearchPlaceholder();
    window.addEventListener("resize", syncHeaderSearchPlaceholder);

    // 1. XỬ LÝ SIDEBAR (COLLAPSE/ACTIVE) VÀ ACCESSIBILITY
    const menuBtn = document.getElementById("menuToggleBtn") || document.querySelector(".menu-icon");
    const sidebar = document.getElementById("mainSidebar") || document.querySelector(".sidebar");
    const layout = document.querySelector(".layout");

    if (menuBtn && sidebar && layout) {
        const mobileMql = window.matchMedia("(max-width: 768px)");

        function syncSidebarState() {
            if (mobileMql.matches) {
                const isOpen = sidebar.classList.contains("active");
                menuBtn.setAttribute("aria-expanded", isOpen ? "true" : "false");
                sidebar.inert = !isOpen;
            } else {
                sidebar.inert = false;
                const isCollapsed = sidebar.classList.contains("collapsed");
                menuBtn.setAttribute("aria-expanded", isCollapsed ? "false" : "true");
            }
        }

        function closeMobileSidebar(returnFocus) {
            if (mobileMql.matches && sidebar.classList.contains("active")) {
                if (returnFocus) {
                    menuBtn.focus();
                }
                sidebar.classList.remove("active");
                syncSidebarState();
            }
        }

        function openMobileSidebar() {
            sidebar.classList.add("active");
            syncSidebarState();
            const firstLink = sidebar.querySelector("a, button, input");
            if (firstLink) {
                firstLink.focus();
            }
        }

        menuBtn.addEventListener("click", function () {
            if (mobileMql.matches) {
                if (sidebar.classList.contains("active")) {
                    closeMobileSidebar(true);
                } else {
                    openMobileSidebar();
                }
            } else {
                const isCollapsed = sidebar.classList.toggle("collapsed");
                layout.classList.toggle("sidebar-collapsed");
                syncSidebarState();
            }
        });

        document.addEventListener("click", function (e) {
            if (mobileMql.matches && sidebar.classList.contains("active") &&
                !sidebar.contains(e.target) && !menuBtn.contains(e.target)) {
                closeMobileSidebar(false);
            }
        });

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && mobileMql.matches && sidebar.classList.contains("active")) {
                closeMobileSidebar(true);
            }
        });

        mobileMql.addEventListener("change", syncSidebarState);
        syncSidebarState();
    }

    // 2. HÀM DÙNG CHUNG ĐỂ XỬ LÝ DROPDOWN (COURSES, FOLDERS, CLASSES)
    function setupSidebarDropdown(btnId, containerId, menuId) {
        const btn = document.getElementById(btnId);
        const container = document.getElementById(containerId);
        const menu = document.getElementById(menuId);

        if (btn && container && menu) {
            // Tự động mở nếu có link con đang được active
            const hasActive = !!menu.querySelector('.active');
            if (hasActive) {
                container.classList.add("expanded");
                menu.classList.add("show");
                btn.setAttribute("aria-expanded", "true");
            } else {
                btn.setAttribute("aria-expanded", "false");
            }

            btn.addEventListener("click", () => {
                const isExpanded = container.classList.toggle("expanded");
                menu.classList.toggle("show");
                btn.setAttribute("aria-expanded", isExpanded ? "true" : "false");
            });

            container.addEventListener("keydown", (e) => {
                if (e.key === "Escape" && container.classList.contains("expanded")) {
                    container.classList.remove("expanded");
                    menu.classList.remove("show");
                    btn.setAttribute("aria-expanded", "false");
                    btn.focus();
                }
            });
        }
    }

    // Khởi tạo 3 cụm dropdown
    setupSidebarDropdown("coursesDropdownBtn", "coursesDropdown", "coursesDropdownMenu");
    setupSidebarDropdown("folderDropdownBtn", "folderDropdown", "folderDropdownMenu");
    setupSidebarDropdown("classDropdownBtn", "classDropdown", "classDropdownMenu");

    // 3. XỬ LÝ MODAL (NEW FOLDER)
    const newFolderBtn = document.getElementById("newFolderBtn");
    const folderModal = document.getElementById("folderModal");
    const cancelFolderBtn = document.getElementById("cancelFolderBtn");

    if (newFolderBtn && folderModal) {
        newFolderBtn.addEventListener("click", () => {
            folderModal.classList.add("active");
            const input = folderModal.querySelector("input[name='folderName']");
            if (input) input.focus();
        });

        cancelFolderBtn?.addEventListener("click", () => {
            folderModal.classList.remove("active");
            newFolderBtn.focus();
        });

        folderModal.addEventListener("click", (e) => {
            if (e.target === folderModal) {
                folderModal.classList.remove("active");
                newFolderBtn.focus();
            }
        });

        folderModal.addEventListener("keydown", (e) => {
            if (e.key === "Escape") {
                folderModal.classList.remove("active");
                newFolderBtn.focus();
            }
        });
    }

    // 4. XỬ LÝ USER DROPDOWN (AVATAR)
    const userProfileBtn = document.getElementById("userProfileBtn");
    const userDropdown = document.getElementById("userDropdown");

    if (userProfileBtn && userDropdown) {
        userProfileBtn.addEventListener("click", (e) => {
            e.stopPropagation();
            const isOpen = userDropdown.classList.toggle("active");
            userProfileBtn.setAttribute("aria-expanded", isOpen ? "true" : "false");
        });

        document.addEventListener("click", (e) => {
            if (!userDropdown.contains(e.target) && !userProfileBtn.contains(e.target)) {
                if (userDropdown.classList.contains("active")) {
                    userDropdown.classList.remove("active");
                    userProfileBtn.setAttribute("aria-expanded", "false");
                }
            }
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && userDropdown.classList.contains("active")) {
                userDropdown.classList.remove("active");
                userProfileBtn.setAttribute("aria-expanded", "false");
                userProfileBtn.focus();
            }
        });
    }
});
