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

    // 3. HỘP THOẠI TẠO THƯ MỤC (thẻ <dialog> gốc: Esc, khóa focus và nền mờ do trình duyệt xử lý)
    const folderModal = document.getElementById("folderModal");

    if (folderModal) {
        const folderForm = folderModal.querySelector("form");
        const folderInput = folderModal.querySelector("input[name='name']");
        const folderCount = folderModal.querySelector("[data-folder-count]");
        const folderError = folderModal.querySelector("[data-folder-error]");
        const folderSubmit = folderModal.querySelector("button[type='submit']");
        const cancelFolderBtn = document.getElementById("cancelFolderBtn");
        const folderSubmitNodes = folderSubmit ? Array.from(folderSubmit.childNodes, (n) => n.cloneNode(true)) : [];
        let folderTrigger = null;

        const showFolderError = (message) => {
            if (!folderError) return;
            folderError.textContent = message;
            folderError.hidden = !message;
        };
        const syncFolderCount = () => {
            if (folderCount && folderInput) folderCount.textContent = folderInput.value.length + "/100";
        };
        const resetFolderSubmit = () => {
            if (!folderSubmit) return;
            folderSubmit.disabled = false;
            folderSubmit.replaceChildren(...folderSubmitNodes.map((n) => n.cloneNode(true)));
        };
        const openFolderDialog = (trigger) => {
            folderTrigger = trigger || document.getElementById("newFolderBtn");
            if (!folderModal.open) {
                if (typeof folderModal.showModal === "function") folderModal.showModal();
                else folderModal.setAttribute("open", "");
            }
            syncFolderCount();
            if (folderInput) {
                folderInput.focus();
                if (typeof folderInput.select === "function") folderInput.select();
            }
        };
        // The phone drawer closes (and becomes inert) behind the dialog; then the menu button is the place to return to.
        const returnFolderFocus = () => {
            let target = folderTrigger;
            if (!target || (typeof target.closest === "function" && target.closest("[inert]"))) {
                target = document.getElementById("menuToggleBtn");
            }
            if (target && typeof target.focus === "function") target.focus();
        };
        // "Tạo thư mục" in a page header is a plain link to /Sets#tao-thu-muc (it works without JavaScript); the hash is dropped once the dialog closes.
        const clearFolderHash = () => {
            if (window.location && window.location.hash === "#tao-thu-muc" && window.history && typeof window.history.replaceState === "function") {
                window.history.replaceState(null, "", window.location.pathname + window.location.search);
            }
        };
        const closeFolderDialog = () => {
            if (typeof folderModal.close === "function") folderModal.close();
            else folderModal.removeAttribute("open");
        };

        document.querySelectorAll("#newFolderBtn, [data-open-folder-dialog], a[href$='#tao-thu-muc']").forEach((trigger) => {
            trigger.addEventListener("click", (e) => {
                e.preventDefault();
                openFolderDialog(trigger);
            });
        });

        cancelFolderBtn?.addEventListener("click", closeFolderDialog);
        folderModal.addEventListener("click", (e) => {
            if (e.target === folderModal) closeFolderDialog(); // click on the backdrop
        });
        folderModal.addEventListener("close", () => {
            resetFolderSubmit();
            clearFolderHash();
            returnFolderFocus();
        });
        folderInput?.addEventListener("input", () => {
            syncFolderCount();
            showFolderError("");
        });
        folderForm?.addEventListener("submit", (e) => {
            if (!folderInput || !folderInput.value.trim()) {
                e.preventDefault();
                showFolderError("Hãy nhập tên thư mục.");
                if (folderInput) folderInput.focus();
                return;
            }
            if (folderSubmit) {
                folderSubmit.disabled = true; // one click, one folder
                folderSubmit.textContent = folderSubmit.getAttribute("data-pending") || "Đang tạo…";
            }
        });
        window.addEventListener("pageshow", resetFolderSubmit);
        // Back/forward or a changed hash on the same page.
        window.addEventListener("hashchange", () => {
            if (window.location && window.location.hash === "#tao-thu-muc") openFolderDialog(null);
        });

        // After a failed create the server redirects back and asks for the dialog to reopen with the message.
        if (folderModal.hasAttribute("data-open-on-load") || (window.location && window.location.hash === "#tao-thu-muc")) openFolderDialog(null);
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
