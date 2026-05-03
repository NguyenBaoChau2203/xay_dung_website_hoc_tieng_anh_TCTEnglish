/* 
   TRƯỚC KHI NHÚNG FILE NÀY, HÃY KHAI BÁO CÁC BIẾN SAU TRONG FILE CSHTML:
   const classId = @Model.Class.ClassId;
   const currentUserId = @Model.CurrentUserId;
   const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
*/

let pendingClassId = null;
let connection = null;
const classDetailTabStorageKey = `class-detail-tab-${classId}`;

function getActiveTab() {
    const activeButton = document.querySelector(".tab-btn.active");
    return activeButton?.dataset.tab || "chat";
}

function persistActiveTab(tab) {
    try {
        sessionStorage.setItem(classDetailTabStorageKey, tab);
    } catch {
    }

    const url = new URL(window.location.href);
    url.searchParams.set("tab", tab);
    window.history.replaceState({}, "", url);
}

function reloadWithTab(tab) {
    persistActiveTab(tab);

    const url = new URL(window.location.href);
    url.searchParams.set("tab", tab);
    window.location.href = url.toString();
}

function reloadCurrentTab() {
    reloadWithTab(getActiveTab());
}

function restoreInitialTab() {
    const url = new URL(window.location.href);
    const queryTab = url.searchParams.get("tab");

    if (queryTab) {
        showTab(queryTab, false);
        return;
    }

    try {
        const savedTab = sessionStorage.getItem(classDetailTabStorageKey);
        if (savedTab) {
            showTab(savedTab, false);
        }
    } catch {
    }
}

// ==========================================
// 1. QUẢN LÝ TAB & UI CHUNG
// ==========================================

function showTab(tab, shouldPersist = true) {
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });

    document.querySelectorAll('.tab-content').forEach(content => {
        content.classList.remove('active');
    });

    const activeBtn = document.querySelector(`.tab-btn[data-tab="${tab}"]`);
    if (activeBtn) {
        activeBtn.classList.add('active');
    }

    const activeContent = document.getElementById(`tab-${tab}`);
    if (activeContent) {
        activeContent.classList.add('active');
    }

    if (shouldPersist) {
        persistActiveTab(tab);
    }
}

function scrollToBottom() {
    const box = document.getElementById("chatMessages");
    if (box) {
        box.scrollTop = box.scrollHeight;
    }
}

function toggleEmoji() {
    const panel = document.getElementById("emojiPanel");
    if (!panel) return;

    if (
        panel.style.display === "none" ||
        panel.style.display === ""
    ) {
        panel.style.display = "grid";
    } else {
        panel.style.display = "none";
    }
}

// ==========================================
// 2. QUẢN LÝ MODAL (FIX HOÀN CHỈNH)
// ==========================================

function getModal(id) {
    return document.getElementById(id);
}

function openModal(id) {
    const modal = getModal(id);

    if (modal) {
        modal.classList.add("active");
        document.body.style.overflow = "hidden";
    }
}

function closeModal(id) {
    const modal = getModal(id);

    if (modal) {
        modal.classList.remove("active");
        document.body.style.overflow = "";
    }
}

function openAddFolderModal() {
    openModal("addFolderModal");
}

function closeAddFolderModal() {
    closeModal("addFolderModal");
}

function openClassActionModal() {
    openModal("classActionModal");
}

function closeClassActionModal() {
    closeModal("classActionModal");
}

function openEditClassModal() {
    closeClassActionModal();
    openModal("editClassModal");
}

function closeEditClassModal() {
    closeModal("editClassModal");
}

function closeJoinClassModal() {
    closeModal("joinClassModal");
    pendingClassId = null;
}

// ==========================================
// 3. LOGIC XỬ LÝ LỚP HỌC (FETCH API)
// ==========================================

function handleJoinClick(id, hasPassword) {
    closeClassActionModal();

    if (hasPassword) {
        pendingClassId = id;
        openModal("joinClassModal");
    } else {
        joinClass(id, "");
    }
}

function confirmJoinClass() {
    const password = document.getElementById("joinClassPassword").value;
    joinClass(pendingClassId, password);
}

function joinClass(id, password) {
    fetch("/Home/JoinClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${id}&password=${encodeURIComponent(password)}`
    })
        .then(async response => {
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Không thể tham gia lớp học.");
            }

            reloadCurrentTab();
        })
        .catch(error => {
            alert(error.message);
        });
}

function saveClassInfo() {
    const name = document.getElementById("editClassName").value.trim();
    const desc = document.getElementById("editClassDescription").value.trim();

    const requiresApproval =
        document.getElementById("editRequiresApproval")?.checked || false;

    const isChatLocked =
        document.getElementById("editIsChatLocked")?.checked || false;

    const allowMemberToPost =
        document.getElementById("editAllowMemberToPost")?.checked || false;

    if (!name) {
        alert("Tên lớp không được để trống");
        return;
    }

    fetch("/Home/EditClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body:
            `classId=${classId}` +
            `&className=${encodeURIComponent(name)}` +
            `&description=${encodeURIComponent(desc)}` +
            `&requiresApproval=${requiresApproval}` +
            `&isChatLocked=${isChatLocked}` +
            `&allowMemberToPost=${allowMemberToPost}`
    })
        .then(async response => {
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Không thể cập nhật lớp.");
            }

            reloadCurrentTab();
        })
        .catch(error => {
            alert(error.message);
        });
}

function leaveClass() {
    if (!confirm("Bạn chắc chắn muốn rời lớp?")) return;

    fetch("/Home/LeaveClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}`
    })
        .then(async response => {
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            window.location.href = "/Home/Class";
        })
        .catch(error => {
            alert(error.message);
        });
}

function deleteClass() {
    if (!confirm("Bạn có chắc muốn GIẢI TÁN lớp này?")) return;

    fetch("/Home/DeleteClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}`
    })
        .then(async response => {
            if (!response.ok) {
                throw new Error("Không thể xoá lớp.");
            }

            window.location.href = "/Home/Class";
        })
        .catch(error => {
            alert(error.message);
        });
}

// ==========================================
// 4. QUẢN LÝ FOLDER & THÀNH VIÊN
// ==========================================

function addFolder(folderId) {
    fetch("/Home/AddFolderToClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}&folderId=${folderId}`
    })
        .then(async response => {
            if (response.ok) {
                reloadWithTab("folders");
            } else {
                const errorText = await response.text();
                alert(errorText || "Không thể thêm folder.");
            }
        });
}

function removeFolder(event, folderId) {
    event.preventDefault();
    event.stopPropagation();

    if (!confirm("Bạn có chắc muốn xoá folder khỏi lớp?")) return;

    fetch("/Home/RemoveFolderFromClass", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}&folderId=${folderId}`
    })
        .then(async response => {
            if (response.ok) {
                reloadWithTab("folders");
            } else {
                const errorText = await response.text();
                alert(errorText || "Lỗi khi xoá folder.");
            }
        });
}

function kickMember(userId) {
    if (!confirm("Bạn có chắc muốn xoá thành viên này?")) return;

    fetch("/Home/KickMember", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}&userId=${userId}`
    })
        .then(async response => {
            if (response.ok) {
                reloadWithTab("members");
            } else {
                const errorText = await response.text();
                alert(errorText || "Lỗi khi xoá thành viên.");
            }
        });
}

function blockMember(userId) {
    if (!confirm("Bạn có chắc muốn xoá và chặn thành viên này?")) return;

    fetch("/Home/BlockMember", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: `classId=${classId}&targetUserId=${userId}`
    })
        .then(async response => {
            if (response.ok) {
                reloadWithTab("members");
            } else {
                const errorText = await response.text();
                alert(errorText || "Lỗi khi chặn thành viên.");
            }
        });
}

function toggleMemberDropdown(event, userId) {
    event.preventDefault();
    event.stopPropagation();

    const targetDropdown = document.getElementById(`dropdown-${userId}`);

    document.querySelectorAll(".dropdown-content-member").forEach(dropdown => {
        if (dropdown.id !== `dropdown-${userId}`) {
            dropdown.classList.remove("show");
        }
    });

    if (targetDropdown) {
        targetDropdown.classList.toggle("show");
    }
}

// ==========================================
// 5. SIGNALR CHAT
// ==========================================

function initSignalR() {
    if (typeof signalR === "undefined") return;

    connection = new signalR.HubConnectionBuilder()
        .withUrl("/classChatHub")
        .build();

    connection.on("ReceiveMessage", function (msg) {
        appendMessage(msg, "text");
    });

    connection.on("ReceiveImage", function (msg) {
        appendMessage(msg, "image");
    });

    connection.start()
        .then(function () {
            connection.invoke("JoinClass", classId);
            scrollToBottom();
        })
        .catch(function (err) {
            console.error(err.toString());
        });
}

function appendMessage(msg, type = "text") {
    const box = document.getElementById("chatMessages");
    if (!box) return;

    const isMine = msg.userId === currentUserId;

    let contentHtml = "";

    if (type === "image") {
        contentHtml =
            `<img 
                src="${msg.imageUrl}" 
                class="chat-image"
                onclick="window.open(this.src)"
                onload="scrollToBottom()"
            />`;
    } else {
        contentHtml = msg.content;
    }

    box.insertAdjacentHTML("beforeend", `
        <div class="chat-message ${isMine ? "mine" : "other"}">
            <div class="chat-bubble">
                ${(!isMine && type === "text")
            ? `<div class="chat-name">${msg.fullName}</div>`
            : ""
        }
                <div class="chat-content">
                    ${contentHtml}
                </div>
            </div>
        </div>
    `);

    scrollToBottom();
}

function sendMessage() {
    const input = document.getElementById("chatInput");
    if (!input || !connection) return;

    const text = input.value.trim();

    if (!text) return;

    connection.invoke("SendMessage", classId, text);
    input.value = "";
}

// ==========================================
// 6. EVENT LISTENERS
// ==========================================

document.addEventListener("DOMContentLoaded", function () {
    restoreInitialTab();

    // Khởi tạo chat
    if (document.getElementById("chatMessages")) {
        initSignalR();
        scrollToBottom();
    }

    // Emoji click
    document.querySelectorAll(".emoji-item").forEach(item => {
        item.addEventListener("click", function () {
            const input = document.getElementById("chatInput");

            if (input) {
                input.value += this.innerText;
                input.focus();
            }
        });
    });

    // Upload ảnh chat
    const imageInput = document.getElementById("imageInput");

    if (imageInput) {
        imageInput.addEventListener("change", async function () {
            const file = this.files[0];
            if (!file) return;

            const formData = new FormData();
            formData.append("image", file);
            formData.append("classId", classId);

            const response = await fetch("/Chat/UploadImage", {
                method: "POST",
                headers: {
                    "RequestVerificationToken": token
                },
                body: formData
            });

            if (response.ok) {
                const data = await response.json();

                if (connection) {
                    connection.invoke(
                        "SendImageMessage",
                        classId,
                        data.imageUrl
                    );
                }
            }

            this.value = "";
        });
    }

    // Click ngoài modal để đóng
    document.addEventListener("click", function (e) {

        if (e.target.classList.contains("modal-overlay")) {
            e.target.classList.remove("active");
            document.body.style.overflow = "";
        }

        if (!e.target.closest(".dropdown-member")) {
            document.querySelectorAll(".dropdown-content-member")
                .forEach(dropdown => {
                    dropdown.classList.remove("show");
                });
        }
    });

    // ESC đóng modal
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            document.querySelectorAll(".modal-overlay.active")
                .forEach(modal => {
                    modal.classList.remove("active");
                });

            document.body.style.overflow = "";
        }
    });
});
function changeMemberRole(targetUserId, role) {
    let roleText = role == 1
        ? "thăng thành Phó nhóm"
        : "hạ xuống Thành viên";

    if (!confirm(`Bạn có chắc muốn ${roleText}?`)) return;

    fetch("/Home/ChangeMemberRole", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: new URLSearchParams({
            classId: classId,
            targetUserId: targetUserId,
            role: role
        })
    })
        .then(res => {
            if (res.ok) {
                alert("Cập nhật quyền thành công");
                reloadWithTab("members");
                return;
            }

            return res.text().then(err => {
                throw new Error(err || "Lỗi cập nhật quyền");
            });
        })
        .catch(err => {
            alert(err.message);
            console.error(err);
        });
}


function toggleMuteMember(targetUserId, shouldMute) {
    let actionText = shouldMute ? "mute" : "bỏ mute";

    if (!confirm(`Bạn có chắc muốn ${actionText} thành viên này?`)) return;

    let url = shouldMute
        ? "/Home/MuteMember"
        : "/Home/UnMuteMember";

    fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": token
        },
        body: new URLSearchParams({
            classId: classId,
            targetUserId: targetUserId
        })
    })
        .then(res => {
            if (res.ok) {
                alert(`Đã ${actionText} thành công`);
                reloadWithTab("members");
                return;
            }

            return res.text().then(err => {
                throw new Error(err || "Lỗi xử lý mute");
            });
        })
        .catch(err => {
            alert(err.message);
            console.error(err);
        });
}
/* =========================
   JOIN REQUEST MODAL
========================= */

function openJoinRequestsModal() {
    const modal = document.getElementById("joinRequestsModal");

    if (modal) {
        modal.classList.add("active");
    }
}

function closeJoinRequestsModal() {
    const modal = document.getElementById("joinRequestsModal");

    if (modal) {
        modal.classList.remove("active");
    }
}
async function approveJoinRequest(requestId) {
    console.log("approve requestId =", requestId);

    if (!confirm("Chấp nhận yêu cầu này?")) return;

    const formData = new FormData();
    formData.append("requestId", requestId);
    formData.append("__RequestVerificationToken", token);

    try {
        const response = await fetch(approveJoinRequestUrl, {
            method: "POST",
            body: formData
        });

        if (response.ok) {
            alert("Đã chấp nhận yêu cầu.");
            reloadWithTab("members");
            return;
        }

        const error = await response.text();
        console.error(error);
        alert(error || "Có lỗi xảy ra.");
    }
    catch (err) {
        console.error(err);
        alert("Không thể xử lý yêu cầu.");
    }
}

async function declineJoinRequest(requestId) {
    console.log("decline requestId =", requestId);

    if (!confirm("Từ chối yêu cầu này?")) return;

    const formData = new FormData();
    formData.append("requestId", requestId);
    formData.append("__RequestVerificationToken", token);

    try {
        const response = await fetch(declineJoinRequestUrl, {
            method: "POST",
            body: formData
        });

        if (response.ok) {
            alert("Đã từ chối yêu cầu.");
            reloadWithTab("members");
            return;
        }

        const error = await response.text();
        console.error(error);
        alert(error || "Có lỗi xảy ra.");
    }
    catch (err) {
        console.error(err);
        alert("Không thể xử lý yêu cầu.");
    }
}

/* click ra ngoài để đóng */
window.addEventListener("click", function (e) {
    const modal = document.getElementById("joinRequestsModal");

    if (modal && e.target === modal) {
        closeJoinRequestsModal();
    }
});

