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
document.addEventListener("DOMContentLoaded", function () {

    const newFolderBtn = document.querySelector(".workspace-btn");
    const modal = document.getElementById("folderModal");
    const cancelBtn = document.getElementById("cancelFolderBtn");
    const confirmBtn = document.getElementById("confirmFolderBtn");
    const input = document.getElementById("folderNameInput");

    // Open modal
    newFolderBtn.addEventListener("click", () => {
        modal.classList.add("active");
        input.value = "";
        input.focus();
    });

    // Close modal
    function closeModal() {
        modal.classList.remove("active");
    }

    cancelBtn.addEventListener("click", closeModal);

    // Click outside modal to close
    modal.addEventListener("click", (e) => {
        if (e.target === modal) {
            closeModal();
        }
    });

    // Confirm
    confirmBtn.addEventListener("click", () => {
        const folderName = input.value.trim();

        if (!folderName) {
            alert("Folder name is required!");
            return;
        }

        console.log("New folder:", folderName);
        // TODO: call API / submit form / append UI

        closeModal();
    });
});