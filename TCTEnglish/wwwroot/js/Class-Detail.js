/* 
   TRƯỚC KHI NHÚNG FILE NÀY, HÃY KHAI BÁO CÁC BIẾN SAU TRONG FILE CSHTML:
   const classId = @Model.Class.ClassId;
   const currentUserId = @Model.CurrentUserId;
   const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
*/

let pendingClassId = null;
let connection = null;
const classDetailTabStorageKey = `class-detail-tab-${classId}`;

function classToast(options) {
    if (window.TctFeedback && typeof window.TctFeedback.toast === "function") {
        window.TctFeedback.toast(options);
        return;
    }
    window.alert([options.title, options.message].filter(Boolean).join("\n"));
}

async function classConfirm(options) {
    if (window.TctFeedback && typeof window.TctFeedback.confirm === "function") {
        return window.TctFeedback.confirm(options);
    }
    return window.confirm([options.title, options.message].filter(Boolean).join("\n"));
}

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

            const data = await response.json();
            if (data.status === "needsPassword") {
                pendingClassId = id;
                openModal("joinClassModal");
                return;
            }

            if (data.status === "pending") {
                closeModal("joinClassModal");
                classToast({
                    type: "success",
                    title: "Đã gửi yêu cầu tham gia",
                    message: data.message || "Vui lòng chờ quản trị lớp phê duyệt."
                });
                return;
            }

            if (data.redirectUrl) {
                window.location.href = data.redirectUrl;
                return;
            }

            window.location.reload();
        })
        .catch(error => {
            classToast({
                type: "danger",
                title: "Không tham gia được lớp",
                message: error.message || "Thử lại sau hoặc kiểm tra mã/mật khẩu lớp."
            });
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

    const isPublic =
        document.getElementById("editIsPublic")?.checked || false;

    if (!name) {
        classToast({
            type: "warning",
            title: "Thiếu tên lớp",
            message: "Nhập tên lớp trước khi lưu."
        });
        return;
    }

    const coverInput = document.getElementById("editClassCover");
    const coverFile = coverInput?.files?.[0];

    const saveInfo = fetch("/Home/EditClass", {
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
            `&allowMemberToPost=${allowMemberToPost}` +
            `&isPublic=${isPublic}`
    }).then(async response => {
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || "Không thể cập nhật lớp.");
        }
    });

    const saveCover = coverFile
        ? (() => {
            const formData = new FormData();
            formData.append("classId", classId);
            formData.append("cover", coverFile);
            formData.append("__RequestVerificationToken", token);
            return fetch("/Home/UpdateClassCover", {
                method: "POST",
                body: formData
            }).then(async response => {
                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(errorText || "Không thể cập nhật ảnh bìa.");
                }
            });
        })()
        : Promise.resolve();

    Promise.all([saveInfo, saveCover])
        .then(() => reloadCurrentTab())
        .catch(error => classToast({
            type: "danger",
            title: "Không lưu được thông tin lớp",
            message: error.message || "Kiểm tra mạng rồi thử lại."
        }));
}

async function leaveClass() {
    const ok = await classConfirm({
        title: "Rời lớp?",
        message: "Bạn sẽ mất quyền truy cập nội dung và chat của lớp này.",
        confirmLabel: "Rời lớp",
        danger: true
    });
    if (!ok) return;

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
            classToast({
                type: "danger",
                title: "Không rời được lớp",
                message: error.message || "Thử lại sau."
            });
        });
}

async function deleteClass() {
    const ok = await classConfirm({
        title: "Giải tán lớp?",
        message: "Thành viên mất quyền truy cập lớp này. Thao tác không hoàn tác từ đây.",
        confirmLabel: "Giải tán lớp",
        danger: true
    });
    if (!ok) return;

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
            classToast({
                type: "danger",
                title: "Không giải tán được lớp",
                message: error.message || "Thử lại sau."
            });
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
                classToast({
                    type: "danger",
                    title: "Không thêm được folder",
                    message: errorText || "Folder có thể đã có trong lớp hoặc bạn không đủ quyền."
                });
            }
        });
}

async function removeFolder(event, folderId) {
    event.preventDefault();
    event.stopPropagation();

    const ok = await classConfirm({
        title: "Xóa folder khỏi lớp?",
        message: "Folder chỉ bị gỡ khỏi lớp; nội dung folder của bạn vẫn giữ nguyên.",
        confirmLabel: "Xóa khỏi lớp",
        danger: true
    });
    if (!ok) return;

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
                classToast({
                    type: "danger",
                    title: "Không xóa được folder khỏi lớp",
                    message: errorText || "Thử lại sau."
                });
            }
        });
}

async function kickMember(userId) {
    const ok = await classConfirm({
        title: "Xóa thành viên?",
        message: "Người này sẽ bị gỡ khỏi lớp và mất quyền truy cập.",
        confirmLabel: "Xóa thành viên",
        danger: true
    });
    if (!ok) return;

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
                classToast({
                    type: "danger",
                    title: "Không xóa được thành viên",
                    message: errorText || "Thử lại sau."
                });
            }
        });
}

async function blockMember(userId) {
    const ok = await classConfirm({
        title: "Xóa và chặn thành viên?",
        message: "Người này bị gỡ khỏi lớp và không thể gửi yêu cầu tham gia lại.",
        confirmLabel: "Xóa và chặn",
        danger: true
    });
    if (!ok) return;

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
                classToast({
                    type: "danger",
                    title: "Không chặn được thành viên",
                    message: errorText || "Thử lại sau."
                });
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

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function appendMessage(msg, type = "text", prepend = false) {
    const box = document.getElementById("chatMessages");
    if (!box) return;

    const isMine = msg.userId === currentUserId || msg.UserId === currentUserId;
    const userId = msg.userId ?? msg.UserId;
    const fullName = escapeHtml(msg.fullName ?? msg.FullName ?? "");
    const messageId = msg.messageId ?? msg.MessageId ?? "";
    const imageUrl = msg.imageUrl ?? msg.ImageUrl ?? msg.content ?? msg.Content ?? "";
    const textContent = msg.content ?? msg.Content ?? "";

    let contentHtml = "";

    if (type === "image" || (typeof textContent === "string" && textContent.startsWith("/uploads/"))) {
        contentHtml =
            `<img src="${escapeHtml(imageUrl || textContent)}" class="chat-image" alt="" onclick="window.open(this.src)" onload="scrollToBottom()" />`;
    } else {
        contentHtml = escapeHtml(textContent);
    }

    const html = `
        <div class="chat-message ${isMine ? "mine" : "other"}" data-message-id="${escapeHtml(messageId)}">
            <div class="chat-bubble">
                ${(!isMine && type === "text")
            ? `<div class="chat-name">${fullName}</div>`
            : ""
        }
                <div class="chat-content">
                    ${contentHtml}
                </div>
            </div>
        </div>
    `;

    if (prepend) {
        box.insertAdjacentHTML("afterbegin", html);
    } else {
        box.insertAdjacentHTML("beforeend", html);
        scrollToBottom();
    }
}

function sendMessage() {
    if (typeof canSendChat !== "undefined" && !canSendChat) {
        return;
    }

    const input = document.getElementById("chatInput");
    if (!input || !connection) return;

    const text = input.value.trim();

    if (!text) return;

    connection.invoke("SendMessage", classId, text).catch(function (err) {
        classToast({
            type: "danger",
            title: "Không gửi được tin nhắn",
            message: err?.message || "Kiểm tra kết nối hoặc quyền chat trong lớp."
        });
    });
    input.value = "";
}

async function loadOlderMessages() {
    const box = document.getElementById("chatMessages");
    const btn = document.getElementById("loadMoreMessagesBtn");
    if (!box || typeof classMessagesUrl === "undefined") return;

    const oldest = box.querySelector(".chat-message[data-message-id]");
    const beforeId = oldest?.getAttribute("data-message-id");
    if (!beforeId) return;

    const previousHeight = box.scrollHeight;
    if (btn) btn.disabled = true;

    try {
        const response = await fetch(
            `${classMessagesUrl}?classId=${classId}&beforeMessageId=${encodeURIComponent(beforeId)}&take=50`
        );
        if (!response.ok) {
            throw new Error("Không tải thêm được tin nhắn.");
        }

        const data = await response.json();
        const messages = data.messages || [];
        messages.forEach(function (msg) {
            const isImage = typeof (msg.content || msg.Content) === "string" &&
                (msg.content || msg.Content).startsWith("/uploads/");
            appendMessage(msg, isImage ? "image" : "text", true);
        });

        box.scrollTop = box.scrollHeight - previousHeight;

        if (!data.hasMore && btn) {
            btn.hidden = true;
        }
    } catch (error) {
        classToast({
            type: "danger",
            title: "Không tải thêm tin nhắn",
            message: error.message || "Thử lại sau."
        });
    } finally {
        if (btn) btn.disabled = false;
    }
}

// ==========================================
// 6. EVENT LISTENERS
// ==========================================

function copyTextToClipboard(text) {
    if (!text) return Promise.reject(new Error("empty"));
    if (navigator.clipboard && window.isSecureContext) {
        return navigator.clipboard.writeText(text);
    }

    return new Promise(function (resolve, reject) {
        const area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "absolute";
        area.style.left = "-9999px";
        document.body.appendChild(area);
        area.select();
        try {
            document.execCommand("copy");
            resolve();
        } catch (err) {
            reject(err);
        } finally {
            document.body.removeChild(area);
        }
    });
}

document.addEventListener("DOMContentLoaded", function () {
    restoreInitialTab();

    // Khởi tạo chat
    if (document.getElementById("chatMessages")) {
        initSignalR();
        scrollToBottom();
    }

    document.getElementById("loadMoreMessagesBtn")?.addEventListener("click", loadOlderMessages);

    document.getElementById("copyClassJoinCodeBtn")?.addEventListener("click", function () {
        const code = document.getElementById("classJoinCodeValue")?.textContent?.trim();
        copyTextToClipboard(code).then(function () {
            classToast({
                type: "success",
                title: "Đã sao chép mã lớp",
                message: "Mã nằm trong bộ nhớ tạm để gửi cho thành viên."
            });
        }).catch(function () {
            classToast({
                type: "warning",
                title: "Không sao chép được",
                message: "Hãy chọn mã và copy thủ công."
            });
        });
    });

    document.getElementById("regenClassJoinCodeBtn")?.addEventListener("click", async function () {
        const ok = await classConfirm({
            title: "Tạo mã lớp mới?",
            message: "Mã cũ sẽ không còn dùng được để tham gia lớp.",
            confirmLabel: "Tạo mã mới",
            danger: true
        });
        if (!ok) return;
        fetch("/Home/RegenerateJoinCode", {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": token
            },
            body: `classId=${classId}`
        })
            .then(async function (response) {
                const text = await response.text();
                let data = null;
                try { data = text ? JSON.parse(text) : null; } catch { /* ignore */ }
                if (!response.ok) {
                    throw new Error((data && data.message) || text || "Không tạo lại được mã.");
                }
                const codeEl = document.getElementById("classJoinCodeValue");
                if (codeEl && data?.joinCode) {
                    codeEl.textContent = data.joinCode;
                }
                classToast({
                    type: "success",
                    title: "Đã tạo mã lớp mới",
                    message: "Mã cũ không còn dùng được."
                });
            })
            .catch(function (err) {
                classToast({
                    type: "danger",
                    title: "Không tạo lại được mã lớp",
                    message: err.message || "Thử lại sau."
                });
            });
    });

    document.getElementById("copyCreatedJoinCodeBtn")?.addEventListener("click", function () {
        const btn = this;
        const label = btn.querySelector(".class-created-card__copy-text");
        const code = document.getElementById("createdJoinCodeValue")?.textContent?.trim();
        const done = btn.getAttribute("data-label-done") || "Đã sao chép";
        const def = btn.getAttribute("data-label-default") || "Sao chép";

        copyTextToClipboard(code).then(function () {
            btn.classList.add("is-copied");
            if (label) label.textContent = done;
            btn.setAttribute("aria-label", done);
            classToast({
                type: "success",
                title: "Đã sao chép mã lớp",
                message: "Mã nằm trong bộ nhớ tạm để gửi cho thành viên."
            });
            window.setTimeout(function () {
                btn.classList.remove("is-copied");
                if (label) label.textContent = def;
                btn.setAttribute("aria-label", def);
            }, 1800);
        }).catch(function () {
            classToast({
                type: "warning",
                title: "Không sao chép được",
                message: "Hãy chọn mã và copy thủ công."
            });
        });
    });

    document.getElementById("dismissCreatedCardBtn")?.addEventListener("click", function () {
        const modal = document.getElementById("classCreatedModal");
        if (modal) {
            modal.classList.remove("active");
            modal.hidden = true;
        }
        document.getElementById("dismissCreatedCardBtn")?.blur();
    });

    document.getElementById("classCreatedModal")?.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            this.classList.remove("active");
            this.hidden = true;
        }
    });

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
async function changeMemberRole(targetUserId, role) {
    const promote = role == 1;
    const ok = await classConfirm({
        title: promote ? "Thăng Phó nhóm?" : "Hạ xuống Thành viên?",
        message: promote
            ? "Người này sẽ được quyền quản lý một phần lớp."
            : "Người này sẽ mất quyền Phó nhóm và trở lại thành viên thường.",
        confirmLabel: promote ? "Thăng Phó nhóm" : "Hạ thành viên",
        danger: !promote
    });
    if (!ok) return;

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
                classToast({
                    type: "success",
                    title: "Đã cập nhật quyền thành viên",
                    message: promote
                        ? "Thành viên đã thành Phó nhóm."
                        : "Phó nhóm đã trở lại Thành viên."
                });
                reloadWithTab("members");
                return;
            }

            return res.text().then(err => {
                throw new Error(err || "Lỗi cập nhật quyền");
            });
        })
        .catch(err => {
            classToast({
                type: "danger",
                title: "Không cập nhật được quyền",
                message: err.message || "Thử lại sau."
            });
            console.error(err);
        });
}


async function toggleMuteMember(targetUserId, shouldMute) {
    const ok = await classConfirm({
        title: shouldMute ? "Tắt chat của thành viên?" : "Bật lại chat cho thành viên?",
        message: shouldMute
            ? "Thành viên sẽ không gửi được tin nhắn trong lớp."
            : "Thành viên có thể gửi tin nhắn trở lại.",
        confirmLabel: shouldMute ? "Tắt chat" : "Bật chat",
        danger: shouldMute
    });
    if (!ok) return;

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
                classToast({
                    type: "success",
                    title: shouldMute ? "Đã tắt chat thành viên" : "Đã bật lại chat",
                    message: shouldMute
                        ? "Thành viên không gửi được tin trong lớp."
                        : "Thành viên có thể chat trở lại."
                });
                reloadWithTab("members");
                return;
            }

            return res.text().then(err => {
                throw new Error(err || "Lỗi xử lý mute");
            });
        })
        .catch(err => {
            classToast({
                type: "danger",
                title: shouldMute ? "Không tắt được chat" : "Không bật lại được chat",
                message: err.message || "Thử lại sau."
            });
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

    const ok = await classConfirm({
        title: "Chấp nhận yêu cầu tham gia?",
        message: "Người gửi sẽ trở thành thành viên của lớp.",
        confirmLabel: "Chấp nhận",
        danger: false
    });
    if (!ok) return;

    const formData = new FormData();
    formData.append("requestId", requestId);
    formData.append("__RequestVerificationToken", token);

    try {
        const response = await fetch(approveJoinRequestUrl, {
            method: "POST",
            body: formData
        });

        if (response.ok) {
            classToast({
                type: "success",
                title: "Đã chấp nhận yêu cầu",
                message: "Thành viên mới đã được thêm vào lớp."
            });
            reloadWithTab("requests");
            return;
        }

        const error = await response.text();
        console.error(error);
        classToast({
            type: "danger",
            title: "Không chấp nhận được yêu cầu tham gia",
            message: error || "Thử lại sau."
        });
    }
    catch (err) {
        console.error(err);
        classToast({
            type: "danger",
            title: "Không chấp nhận được yêu cầu tham gia",
            message: "Kiểm tra mạng rồi thử lại."
        });
    }
}

async function declineJoinRequest(requestId) {
    console.log("decline requestId =", requestId);

    const ok = await classConfirm({
        title: "Từ chối yêu cầu tham gia?",
        message: "Người gửi sẽ không được thêm vào lớp.",
        confirmLabel: "Từ chối",
        danger: true
    });
    if (!ok) return;

    const formData = new FormData();
    formData.append("requestId", requestId);
    formData.append("__RequestVerificationToken", token);

    try {
        const response = await fetch(declineJoinRequestUrl, {
            method: "POST",
            body: formData
        });

        if (response.ok) {
            classToast({
                type: "success",
                title: "Đã từ chối yêu cầu",
                message: "Người gửi không được thêm vào lớp."
            });
            reloadWithTab("requests");
            return;
        }

        const error = await response.text();
        console.error(error);
        classToast({
            type: "danger",
            title: "Không từ chối được yêu cầu tham gia",
            message: error || "Thử lại sau."
        });
    }
    catch (err) {
        console.error(err);
        classToast({
            type: "danger",
            title: "Không từ chối được yêu cầu tham gia",
            message: "Kiểm tra mạng rồi thử lại."
        });
    }
}

/* click ra ngoài để đóng */
window.addEventListener("click", function (e) {
    const modal = document.getElementById("joinRequestsModal");

    if (modal && e.target === modal) {
        closeJoinRequestsModal();
    }
});

