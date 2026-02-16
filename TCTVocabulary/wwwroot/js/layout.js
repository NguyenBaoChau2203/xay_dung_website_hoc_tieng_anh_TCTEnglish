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
    const newFolderBtn = document.getElementById("newFolderBtn");
    const modal = document.getElementById("folderModal");
    const cancelBtn = document.getElementById("cancelFolderBtn");

    if (!newFolderBtn || !modal) return;

    // Open modal
    newFolderBtn.addEventListener("click", () => {
        modal.classList.add("active");
        const input = modal.querySelector("input[name='folderName']");
        if (input) input.focus();
    });

    // Close modal
    cancelBtn.addEventListener("click", () => {
        modal.classList.remove("active");
    });

    // Click outside
    modal.addEventListener("click", (e) => {
        if (e.target === modal) {
            modal.classList.remove("active");
        }
    });

    // --- TOPIC MODAL LOGIC (AUTO SHOW) ---
    const topicModal = document.getElementById("topicModal");
    const closeTopicBtn = document.getElementById("closeTopicModal");

    if (topicModal) {
        // Check local storage
        const hasSeenTopicModal = localStorage.getItem("TCT_TopicModal_Seen");

        if (!hasSeenTopicModal) {
            // Show after short delay for effect
            setTimeout(() => {
                topicModal.classList.add("active");
            }, 600);
        }

        // Close logic
        if (closeTopicBtn) {
            closeTopicBtn.addEventListener("click", () => {
                topicModal.classList.remove("active");
                // Mark as seen
                localStorage.setItem("TCT_TopicModal_Seen", "true");
            });
        }

        // Also mark as seen if clicked outside? 
        // Or keep it stricter? Let's allow closing by clicking outside too.
        topicModal.addEventListener("click", (e) => {
            if (e.target === topicModal) {
                topicModal.classList.remove("active");
                localStorage.setItem("TCT_TopicModal_Seen", "true");
            }
        });
    }

    // --- USER DROPDOWN TOGGLE ---
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