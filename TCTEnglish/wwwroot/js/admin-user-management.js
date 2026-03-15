(function () {
    const page = document.getElementById("userManagementPage");
    if (!page) {
        return;
    }

    const antiForgeryToken = document.querySelector('#userManagementAntiForgeryForm input[name="__RequestVerificationToken"]')?.value ?? "";
    const editModalElement = document.getElementById("editUserModal");
    const blockModalElement = document.getElementById("blockUserModal");
    const editModal = editModalElement ? new bootstrap.Modal(editModalElement) : null;
    const blockModal = blockModalElement ? new bootstrap.Modal(blockModalElement) : null;
    const filterForm = page.querySelector('form[method="get"]');
    const searchInput = filterForm?.querySelector('input[name="q"]');
    const roleSelect = filterForm?.querySelector('select[name="role"]');
    const statusSelect = filterForm?.querySelector('select[name="status"]');
    const usersTableBody = document.querySelector("#usersTable tbody");

    let blockUserEmail = "";
    let presenceSyncTimeoutId = 0;
    let isSyncingPresence = false;
    let pendingPresenceSync = false;

    const statusLabelMap = {
        0: "Offline",
        1: "Online",
        2: "Blocked"
    };

    function getStatusLabel(status) {
        return statusLabelMap[Number(status)] ?? "Offline";
    }

    function getCurrentStatus(userId) {
        const badge = document.getElementById(`status-badge-${userId}`);
        if (!badge) {
            return null;
        }

        const currentStatus = Number(badge.dataset.status);
        return Number.isNaN(currentStatus) ? null : currentStatus;
    }

    function setCounterValue(counterId, value) {
        const counter = document.getElementById(counterId);
        if (!counter) {
            return;
        }

        counter.textContent = String(Math.max(0, Number(value) || 0));
    }

    function adjustCounter(counterId, delta) {
        const counter = document.getElementById(counterId);
        if (!counter) {
            return;
        }

        const nextValue = Math.max(0, (Number(counter.textContent) || 0) + delta);
        counter.textContent = String(nextValue);
    }

    function updateSummary(summary) {
        if (!summary) {
            return;
        }

        setCounterValue("totalUsersCount", summary.totalUsers);
        setCounterValue("onlineUsersCount", summary.onlineUsers);
        setCounterValue("offlineUsersCount", summary.offlineUsers);
        setCounterValue("blockedUsersCount", summary.blockedUsers);
    }

    function setFilteredUsersCount(value) {
        const filteredUsersCount = document.getElementById("filteredUsersCount");
        if (!filteredUsersCount) {
            return;
        }

        filteredUsersCount.textContent = String(Math.max(0, Number(value) || 0));
    }

    function counterIdForStatus(status) {
        switch (Number(status)) {
            case 1:
                return "onlineUsersCount";
            case 2:
                return "blockedUsersCount";
            default:
                return "offlineUsersCount";
        }
    }

    function getActiveStatusFilter() {
        return (statusSelect?.value ?? "").trim().toLowerCase();
    }

    function statusMatchesActiveFilter(status) {
        const activeStatusFilter = getActiveStatusFilter();
        if (!activeStatusFilter) {
            return true;
        }

        return getStatusLabel(status).toLowerCase() === activeStatusFilter;
    }

    function getTrackedRows() {
        return Array.from(usersTableBody?.querySelectorAll("tr[data-user-id]") ?? []);
    }

    function getTrackedUserIds() {
        return getTrackedRows()
            .map((row) => Number(row.dataset.userId))
            .filter((userId) => Number.isInteger(userId) && userId > 0);
    }

    function getOrCreateEmptyStateRow() {
        if (!usersTableBody) {
            return null;
        }

        let emptyStateRow = usersTableBody.querySelector("tr[data-empty-state]");
        if (emptyStateRow) {
            return emptyStateRow;
        }

        emptyStateRow = usersTableBody.querySelector("tr:not([data-user-id])");
        if (emptyStateRow) {
            emptyStateRow.dataset.emptyState = "existing";
            return emptyStateRow;
        }

        emptyStateRow = document.createElement("tr");
        emptyStateRow.dataset.emptyState = "dynamic";
        emptyStateRow.innerHTML = `
            <td colspan="8" class="text-center text-muted py-5">
                <i class="bi bi-inbox fs-1 d-block mb-2"></i>
                Không tìm thấy người dùng nào phù hợp với bộ lọc hiện tại.
            </td>`;
        usersTableBody.appendChild(emptyStateRow);

        return emptyStateRow;
    }

    function syncEmptyState() {
        const emptyStateRow = getOrCreateEmptyStateRow();
        if (!emptyStateRow) {
            return;
        }

        const hasVisibleRows = getTrackedRows().some((row) => !row.hidden);
        emptyStateRow.hidden = hasVisibleRows;
    }

    function applyRowVisibility(userId, status) {
        const row = document.getElementById(`user-row-${userId}`);
        if (!row) {
            return;
        }

        row.hidden = !statusMatchesActiveFilter(status);
    }

    function applyStatusUpdate(statusUpdate, options = {}) {
        if (!statusUpdate) {
            return;
        }

        const skipCounters = options.skipCounters === true;
        const nextStatus = Number(statusUpdate.status);
        if (Number.isNaN(nextStatus)) {
            return;
        }

        const currentStatus = getCurrentStatus(statusUpdate.userId);
        const previousStatus = currentStatus ?? Number(statusUpdate.previousStatus);

        if (!skipCounters
            && !Number.isNaN(previousStatus)
            && previousStatus !== nextStatus) {
            adjustCounter(counterIdForStatus(previousStatus), -1);
            adjustCounter(counterIdForStatus(nextStatus), 1);
        }

        const badge = document.getElementById(`status-badge-${statusUpdate.userId}`);
        if (badge) {
            badge.className = statusUpdate.statusBadgeClass;
            badge.dataset.status = String(nextStatus);
            badge.innerHTML = `<i class="bi ${statusUpdate.statusIconClass} me-1"></i>${statusUpdate.statusLabel}`;
        }

        const row = document.getElementById(`user-row-${statusUpdate.userId}`);
        if (row) {
            row.dataset.status = String(nextStatus);
        }

        const unlockButton = row?.querySelector(".btn-unlock");
        if (unlockButton) {
            unlockButton.disabled = !statusUpdate.canUnlock;
        }

        if (Number(document.getElementById("editUserId")?.value) === Number(statusUpdate.userId)) {
            document.getElementById("editStatusLabel").value = statusUpdate.statusLabel;
        }

        applyRowVisibility(statusUpdate.userId, nextStatus);
        syncEmptyState();
    }

    function applyUserUpdate(user) {
        if (!user) {
            return;
        }

        const fullName = document.getElementById(`user-full-name-${user.userId}`);
        if (fullName) {
            fullName.textContent = user.fullName;
        }

        const email = document.getElementById(`user-email-${user.userId}`);
        if (email) {
            email.textContent = user.email;
        }

        const roleBadge = document.getElementById(`role-badge-${user.userId}`);
        if (roleBadge) {
            roleBadge.className = user.roleBadgeClass;
            roleBadge.innerHTML = `<i class="bi ${user.roleIconClass} me-1"></i>${user.role}`;
        }

        applyStatusUpdate({
            userId: user.userId,
            previousStatus: getCurrentStatus(user.userId) ?? user.status,
            status: user.status,
            statusLabel: user.statusLabel,
            statusBadgeClass: user.statusBadgeClass,
            statusIconClass: user.statusIconClass,
            canUnlock: user.canUnlock
        });
    }

    function setButtonBusy(button, busyHtml) {
        if (!button) {
            return () => {};
        }

        const originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = busyHtml;

        return () => {
            button.disabled = false;
            button.innerHTML = originalHtml;
        };
    }

    function showError(targetId, message) {
        const element = document.getElementById(targetId);
        if (!element) {
            return;
        }

        element.textContent = message;
        element.classList.remove("d-none");
    }

    function hideError(targetId) {
        document.getElementById(targetId)?.classList.add("d-none");
    }

    function buildPresenceSnapshotUrl() {
        const params = new URLSearchParams();
        const searchQuery = searchInput?.value.trim();
        const role = roleSelect?.value ?? "";
        const status = statusSelect?.value ?? "";

        if (searchQuery) {
            params.set("q", searchQuery);
        }

        if (role) {
            params.set("role", role);
        }

        if (status) {
            params.set("status", status);
        }

        getTrackedUserIds().forEach((userId) => {
            params.append("userIds", String(userId));
        });

        const queryString = params.toString();
        return queryString
            ? `/Admin/UserManagement/GetPresenceSnapshot?${queryString}`
            : "/Admin/UserManagement/GetPresenceSnapshot";
    }

    async function fetchPresenceSnapshot() {
        const response = await fetch(buildPresenceSnapshotUrl(), {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        if (!response.ok) {
            throw new Error("Unable to refresh the user presence snapshot.");
        }

        return response.json();
    }

    async function syncPresenceSnapshot() {
        if (isSyncingPresence) {
            pendingPresenceSync = true;
            return;
        }

        isSyncingPresence = true;

        try {
            const snapshot = await fetchPresenceSnapshot();
            (snapshot.statusUpdates ?? []).forEach((statusUpdate) => {
                applyStatusUpdate(statusUpdate, { skipCounters: true });
            });

            updateSummary(snapshot.summary);
            setFilteredUsersCount(snapshot.totalFilteredCount);
            syncEmptyState();
        } catch (error) {
            console.error("Unable to refresh user presence snapshot.", error);
        } finally {
            isSyncingPresence = false;

            if (pendingPresenceSync) {
                pendingPresenceSync = false;
                queuePresenceSync(0);
            }
        }
    }

    function queuePresenceSync(delayMilliseconds = 250) {
        window.clearTimeout(presenceSyncTimeoutId);
        presenceSyncTimeoutId = window.setTimeout(() => {
            void syncPresenceSnapshot();
        }, delayMilliseconds);
    }

    async function loadUser(userId) {
        const response = await fetch(`/Admin/UserManagement/GetUser/${userId}`);
        if (!response.ok) {
            throw new Error("Không thể tải dữ liệu người dùng.");
        }

        return response.json();
    }

    async function postJson(url, payload) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": antiForgeryToken
            },
            body: JSON.stringify(payload)
        });

        const result = await response.json();
        if (!response.ok || !result.success) {
            throw new Error(result.message || "Đã xảy ra lỗi.");
        }

        return result;
    }

    function bindPresenceConnection(connection) {
        if (!connection) {
            return;
        }

        connection.off("UserStatusChanged");
        connection.on("UserStatusChanged", (statusUpdate) => {
            applyStatusUpdate(statusUpdate);
            queuePresenceSync();
        });
    }

    function handlePresenceConnected() {
        queuePresenceSync(0);
    }

    page.querySelectorAll(".btn-edit").forEach((button) => {
        button.addEventListener("click", async () => {
            try {
                const data = await loadUser(button.dataset.userId);

                document.getElementById("editUserId").value = data.userId;
                document.getElementById("editFullName").value = data.fullName;
                document.getElementById("editEmail").value = data.email;
                document.getElementById("editRole").value = data.role;
                document.getElementById("editStatusLabel").value = getStatusLabel(data.status);
                hideError("editErrorAlert");

                editModal?.show();
            } catch (error) {
                await Swal.fire({
                    icon: "error",
                    title: "Lỗi",
                    text: error.message
                });
            }
        });
    });

    document.getElementById("btnSaveEdit")?.addEventListener("click", async function () {
        hideError("editErrorAlert");

        const payload = {
            userId: Number(document.getElementById("editUserId").value),
            fullName: document.getElementById("editFullName").value.trim(),
            email: document.getElementById("editEmail").value.trim(),
            role: document.getElementById("editRole").value
        };

        if (!payload.fullName || !payload.email) {
            showError("editErrorAlert", "Vui lòng nhập đầy đủ thông tin.");
            return;
        }

        const resetButton = setButtonBusy(
            this,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang lưu...'
        );

        try {
            const result = await postJson("/Admin/UserManagement/Edit", payload);
            applyUserUpdate(result.user);
            editModal?.hide();

            await Swal.fire({
                icon: "success",
                title: "Đã lưu!",
                text: result.message,
                timer: 1800,
                showConfirmButton: false
            });
        } catch (error) {
            showError("editErrorAlert", error.message);
        } finally {
            resetButton();
        }
    });

    page.querySelectorAll(".btn-block-user").forEach((button) => {
        button.addEventListener("click", () => {
            blockUserEmail = button.dataset.userEmail ?? "";
            document.getElementById("blockUserId").value = button.dataset.userId ?? "";
            document.getElementById("blockUserName").textContent = button.dataset.userName ?? "";
            document.getElementById("blockReason").value = "";
            document.getElementById("blockDuration").value = "7d";
            hideError("blockErrorAlert");

            blockModal?.show();
        });
    });

    document.getElementById("btnConfirmBlock")?.addEventListener("click", async function () {
        hideError("blockErrorAlert");

        const reason = document.getElementById("blockReason").value.trim();
        if (!reason) {
            showError("blockErrorAlert", "Vui lòng nhập lý do khóa tài khoản.");
            return;
        }

        const payload = {
            userId: Number(document.getElementById("blockUserId").value),
            duration: document.getElementById("blockDuration").value,
            reason
        };

        const resetButton = setButtonBusy(
            this,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang xử lý...'
        );

        try {
            const result = await postJson("/Admin/UserManagement/BlockUser", payload);
            applyStatusUpdate(result.statusUpdate);
            blockModal?.hide();
            queuePresenceSync();

            await Swal.fire({
                icon: "success",
                title: "Thành công!",
                text: `Đã khóa tài khoản ${blockUserEmail} thành công.`,
                timer: 1800,
                showConfirmButton: false
            });
        } catch (error) {
            showError("blockErrorAlert", error.message);
        } finally {
            resetButton();
        }
    });

    page.querySelectorAll(".btn-unlock").forEach((button) => {
        button.addEventListener("click", async () => {
            const userId = Number(button.dataset.userId);
            const userName = button.dataset.userName ?? "";

            const result = await Swal.fire({
                icon: "warning",
                title: "Mở khóa tài khoản?",
                text: `Bạn có chắc muốn mở khóa tài khoản "${userName}"?`,
                showCancelButton: true,
                confirmButtonText: "Mở khóa",
                cancelButtonText: "Hủy",
                confirmButtonColor: "#f0ad4e",
                showLoaderOnConfirm: true,
                preConfirm: async () => {
                    try {
                        return await postJson("/Admin/UserManagement/UnlockUser", { userId });
                    } catch (error) {
                        Swal.showValidationMessage(error.message);
                        return null;
                    }
                },
                allowOutsideClick: () => !Swal.isLoading()
            });

            if (result.isConfirmed && result.value) {
                applyStatusUpdate(result.value.statusUpdate);
                queuePresenceSync();

                await Swal.fire({
                    icon: "success",
                    title: "Đã mở khóa!",
                    text: `Tài khoản "${userName}" đã được mở khóa thành công.`,
                    timer: 1800,
                    showConfirmButton: false
                });
            }
        });
    });

    bindPresenceConnection(window.userActivityHubConnection);

    if (window.userActivityHubReady) {
        window.userActivityHubReady.then((connection) => {
            bindPresenceConnection(connection);
            handlePresenceConnected();
        });
    } else {
        handlePresenceConnected();
    }

    window.addEventListener("user-activity:connected", handlePresenceConnected);
    window.addEventListener("user-activity:reconnected", handlePresenceConnected);

    syncEmptyState();
    queuePresenceSync(0);
})();
