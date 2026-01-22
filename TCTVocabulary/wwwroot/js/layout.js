document.addEventListener("DOMContentLoaded", function () {
    const menuIcon = document.querySelector(".menu-icon");
    const sidebar = document.querySelector(".sidebar");
    const layout = document.querySelector(".layout");

    menuIcon.addEventListener("click", function () {
        sidebar.classList.toggle("collapsed");
        layout.classList.toggle("sidebar-collapsed");
    });
});
