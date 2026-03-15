document.addEventListener("DOMContentLoaded", function () {
    // 1. XỬ LÝ SIDEBAR (COLLAPSE/ACTIVE)
    const menuIcon = document.querySelector(".menu-icon");
    const sidebar = document.querySelector(".sidebar");
    const layout = document.querySelector(".layout");

    if (menuIcon && sidebar && layout) {
        menuIcon.addEventListener("click", function () {
            if (window.innerWidth <= 768) {
                sidebar.classList.toggle("active");
            } else {
                sidebar.classList.toggle("collapsed");
                layout.classList.toggle("sidebar-collapsed");
            }
        });

        document.addEventListener("click", function (e) {
            if (window.innerWidth <= 768 && sidebar.classList.contains("active") &&
                !sidebar.contains(e.target) && !menuIcon.contains(e.target)) {
                sidebar.classList.remove("active");
            }
        });
    }

    // 2. HÀM DÙNG CHUNG ĐỂ XỬ LÝ DROPDOWN (COURSES, FOLDERS, CLASSES)
    function setupSidebarDropdown(btnId, containerId, menuId) {
        const btn = document.getElementById(btnId);
        const container = document.getElementById(containerId);
        const menu = document.getElementById(menuId);

        if (btn && container && menu) {
            // Tự động mở nếu có link con đang được active
            if (menu.querySelector('.active')) {
                container.classList.add("expanded");
                menu.classList.add("show");
            }

            btn.addEventListener("click", () => {
                container.classList.toggle("expanded");
                menu.classList.toggle("show");
            });
        }
    }

    // Gọi hàm cho 3 cụm dropdown của bạn
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

        cancelFolderBtn?.addEventListener("click", () => folderModal.classList.remove("active"));

        folderModal.addEventListener("click", (e) => {
            if (e.target === folderModal) folderModal.classList.remove("active");
        });
    }

    // 4. XỬ LÝ USER DROPDOWN (AVATAR)
    const userProfileBtn = document.getElementById("userProfileBtn");
    const userDropdown = document.getElementById("userDropdown");

    if (userProfileBtn && userDropdown) {
        userProfileBtn.addEventListener("click", (e) => {
            e.stopPropagation();
            userDropdown.classList.toggle("active");
        });

        document.addEventListener("click", (e) => {
            if (!userDropdown.contains(e.target) && !userProfileBtn.contains(e.target)) {
                userDropdown.classList.remove("active");
            }
        });
    }
});