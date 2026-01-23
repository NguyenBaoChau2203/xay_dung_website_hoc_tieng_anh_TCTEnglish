document.addEventListener("DOMContentLoaded", function () {
    const menuIcon = document.querySelector(".menu-icon");
    const sidebar = document.querySelector(".sidebar");
    const layout = document.querySelector(".layout");

    if (!menuIcon || !sidebar || !layout) return;

    menuIcon.addEventListener("click", function () {
        // Mobile & Tablet
        if (window.innerWidth <= 768) {
            sidebar.classList.toggle("active");
        }
        // Desktop
        else {
            sidebar.classList.toggle("collapsed");
            layout.classList.toggle("sidebar-collapsed");
        }
    });

    // Đóng sidebar khi click ra ngoài (mobile)
    document.addEventListener("click", function (e) {
        if (
            window.innerWidth <= 768 &&
            sidebar.classList.contains("active") &&
            !sidebar.contains(e.target) &&
            !menuIcon.contains(e.target)
        ) {
            sidebar.classList.remove("active");
        }
    });
});