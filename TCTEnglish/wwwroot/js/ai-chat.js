(function () {
    const MAX_CONVERSATION_TITLE_LENGTH = 80;
    const SCROLL_BOTTOM_THRESHOLD = 80;
    const ACTIVE_CONVERSATION_STATUS = 'Đang mở từ lịch sử hội thoại.';
    const LAUNCHER_QUOTA_SYNC_EVENT = 'tct-ai-launcher-quota-consumed';

    function init(options) {
        const elForm = document.getElementById('chatForm');
        const elInput = document.getElementById('messageInput');
        const elChatWindow = document.getElementById('chatWindow');
        const elScrollToBottomBtn = document.getElementById('scrollToBottomBtn');
        const elTyping = document.getElementById('typing');
        const elSendBtn = document.getElementById('sendBtn');
        const elStopBtn = document.getElementById('stopBtn');
        const elConversationId = document.getElementById('conversationIdInput');
        const elConversationMetaValue = document.getElementById('conversationMetaValue');
        const elConversationMetaStatus = document.getElementById('conversationMetaStatus');
        const elStatus = document.getElementById('chatStatus');
        const elMetricRequestCount = document.getElementById('aiMetricRequestCount');
        const elMetricErrorRate = document.getElementById('aiMetricErrorRate');
        const elMetricLatency = document.getElementById('aiMetricLatency');
        const elMetricTokenToday = document.getElementById('aiMetricTokenToday');
        const elChatShell = elForm.closest('[data-ai-chat-shell]') || document.querySelector('[data-ai-chat-shell]');
        const elQuickActions = elChatShell?.querySelector('[data-ai-quick-actions]');
        const elHistoryRoot = document.querySelector('[data-ai-history-root]');
        const historyElements = {
            list: document.querySelector('[data-ai-history-list]'),
            empty: document.querySelector('[data-ai-history-empty]'),
            chatPageUrl: elHistoryRoot?.dataset.aiChatBaseUrl || '/AI/Chat'
        };

        if (!elForm || !elInput || !elChatWindow || !elTyping || !elSendBtn || !elStopBtn || !elConversationId) {
            return;
        }

        const runtime = {
            sendUrl: options?.sendUrl || '',
            observabilityUrl: options?.observabilityUrl || '',
            hubUrl: options?.hubUrl || '/classChatHub',
            chatWindowElement: elChatWindow,
            hubConnection: null,
            hubAvailable: false,
            activeStreamId: null,
            isStreaming: false,
            scrollToBottomButton: elScrollToBottomBtn,
            streamChunkCount: 0,
            streamBuffer: '',
            streamMessageElement: null,
            streamCompletionResolver: null
        };

        hydrateAssistantMessages(elChatWindow);
        hydrateTimestamps(historyElements);

        if (historyElements.list) {
            let pendingDeleteBtn = null;
            let pendingConversationId = null;
            const deleteModalEl = document.getElementById('deleteConversationModal');
            const deleteModal = deleteModalEl && window.bootstrap ? new bootstrap.Modal(deleteModalEl) : null;
            const confirmDeleteBtn = document.getElementById('confirmDeleteConversationBtn');

            if (confirmDeleteBtn) {
                confirmDeleteBtn.addEventListener('click', async function () {
                    if (!pendingConversationId || !pendingDeleteBtn) return;

                    const errorEl = document.getElementById('deleteConversationError');
                    if (errorEl) errorEl.classList.add('d-none');
                    confirmDeleteBtn.disabled = true;

                    try {
                        await deleteConversationContext(pendingConversationId, options?.deleteUrl, elConversationId.value, historyElements, pendingDeleteBtn);
                        if (deleteModal) deleteModal.hide();
                    } catch (err) {
                        if (errorEl) {
                            errorEl.textContent = err.message || 'Đã xảy ra lỗi khi xóa hội thoại.';
                            errorEl.classList.remove('d-none');
                        }
                    } finally {
                        confirmDeleteBtn.disabled = false;
                        pendingConversationId = null;
                        pendingDeleteBtn = null;
                    }
                });
            }

            historyElements.list.addEventListener('click', function (e) {
                const deleteBtn = e.target.closest('[data-ai-history-delete]');
                if (deleteBtn) {
                    e.preventDefault();
                    e.stopPropagation();

                    const conversationId = deleteBtn.getAttribute('data-ai-history-delete');
                    if (!deleteModal) {
                        showStatus(elStatus, 'Không thể mở hộp thoại xác nhận xóa. Vui lòng tải lại trang và thử lại.');
                        return;
                    }

                    pendingConversationId = conversationId;
                    pendingDeleteBtn = deleteBtn;
                    const errorEl = document.getElementById('deleteConversationError');
                    if (errorEl) errorEl.classList.add('d-none');
                    hideStatus(elStatus);
                    deleteModal.show();
                }
            });
        }

        scrollToBottom(elChatWindow, false, elScrollToBottomBtn);
        syncHistoryVisibility(historyElements);
        syncScrollAffordance(elChatWindow, elScrollToBottomBtn);
        void initializeHub(runtime, elStatus, elForm, elSendBtn, elStopBtn, elTyping);
        void refreshObservability(runtime.observabilityUrl, {
            requestCount: elMetricRequestCount,
            errorRate: elMetricErrorRate,
            latency: elMetricLatency,
            tokenToday: elMetricTokenToday
        });

        if (elScrollToBottomBtn && elChatWindow) {
            const syncScrollButtonState = function () {
                syncScrollAffordance(elChatWindow, elScrollToBottomBtn);
            };

            elChatWindow.addEventListener('scroll', syncScrollButtonState);
            window.addEventListener('resize', syncScrollButtonState);

            elScrollToBottomBtn.addEventListener('click', function () {
                scrollToBottom(elChatWindow, true, elScrollToBottomBtn);
            });
        }

        elStopBtn.addEventListener('click', async function () {
            if (!runtime.activeStreamId || !runtime.hubConnection) {
                return;
            }

            elStopBtn.disabled = true;

            try {
                await runtime.hubConnection.invoke('StopAiStream', runtime.activeStreamId);
            } catch {
                showStatus(elStatus, 'Không thể dừng phản hồi realtime. Bạn có thể gửi lại bằng AJAX.');
            } finally {
                elStopBtn.disabled = false;
            }
        });

        if (elQuickActions) {
            elQuickActions.addEventListener('click', function (event) {
                const btn = event.target.closest('[data-ai-quick-action]');
                if (!btn || btn.disabled) {
                    return;
                }

                if (elSendBtn.disabled || elForm.dataset.quickActionPending === 'true') {
                    return;
                }

                const question = (btn.getAttribute('data-question') || btn.textContent || '').trim();
                if (!question) {
                    return;
                }

                elInput.value = question;
                hideStatus(elStatus);
                elForm.dataset.quickActionPending = 'true';
                setQuickActionsBusy(elQuickActions, true);

                if (typeof elForm.requestSubmit === 'function') {
                    elForm.requestSubmit();
                } else {
                    elForm.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
                }
            });
        }

        elForm.addEventListener('submit', async function (event) {
            event.preventDefault();

            const quickActionPending = elForm.dataset.quickActionPending === 'true';
            const quickActionTriggered = quickActionPending;
            const text = (elInput.value || '').trim();
            if (!text) {
                if (quickActionPending) {
                    elForm.dataset.quickActionPending = 'false';
                    setQuickActionsBusy(elQuickActions, false);
                }

                return;
            }

            if (quickActionPending) {
                elForm.dataset.quickActionPending = 'false';
            }

            appendMessage(elChatWindow, 'user', text, elScrollToBottomBtn);
            elInput.value = '';
            hideStatus(elStatus);

            setUiState('sending', elForm, elSendBtn, elStopBtn, elTyping, false);

            try {
                const currentConversationId = getConversationIdValue(elConversationId);
                const draftConversationTitle = buildConversationTitle(text, elConversationMetaValue?.textContent || 'Đoạn chat mới');
                const streamed = await trySendWithStreaming(runtime, text, currentConversationId, elChatWindow, elForm, elSendBtn, elStopBtn, elTyping, elStatus);

                if (!streamed) {
                    const antiForgeryToken = elForm.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                    const payload = await sendWithRetry(runtime.sendUrl, {
                        conversationId: currentConversationId || null,
                        message: text
                    }, {
                        allowRetry: Boolean(currentConversationId),
                        antiForgeryToken: antiForgeryToken
                    });

                    updateConversationContext(
                        elConversationId,
                        elConversationMetaValue,
                        elConversationMetaStatus,
                        historyElements,
                        payload?.conversationId,
                        payload?.conversationTitle || draftConversationTitle);
                    appendMessage(elChatWindow, 'assistant', payload.text || '', elScrollToBottomBtn);
                } else if (currentConversationId) {
                    touchConversationHistory(
                        historyElements,
                        currentConversationId,
                        buildConversationTitle(elConversationMetaValue?.textContent || '', 'Đoạn chat hiện tại'));
                }

                notifyLauncherQuotaConsumed();

                if (quickActionTriggered) {
                    markQuickActionsAccepted(elQuickActions);
                }

                setUiState('done', elForm, elSendBtn, elStopBtn, elTyping, false);
            } catch (error) {
                setUiState('error', elForm, elSendBtn, elStopBtn, elTyping, false);
                showStatus(elStatus, error?.message || 'Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.');
                appendMessage(elChatWindow, 'system', 'Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.', elScrollToBottomBtn);

                if (quickActionTriggered && !getConversationIdValue(elConversationId)) {
                    restoreQuickActions(elQuickActions, elChatWindow);
                }
            } finally {
                if (elForm.dataset.state !== 'error') {
                    setUiState('idle', elForm, elSendBtn, elStopBtn, elTyping, false);
                }

                await refreshObservability(runtime.observabilityUrl, {
                    requestCount: elMetricRequestCount,
                    errorRate: elMetricErrorRate,
                    latency: elMetricLatency,
                    tokenToday: elMetricTokenToday
                });

                elInput.focus();
            }
        });

        setUiState('idle', elForm, elSendBtn, elStopBtn, elTyping, false);
    }

    async function initializeHub(runtime, elStatus, elForm, elSendBtn, elStopBtn, elTyping) {
        const connectionInfo = resolveHubConnection(runtime.hubUrl);
        if (!connectionInfo) {
            runtime.hubAvailable = false;
            showStatus(elStatus, 'Realtime tạm thời không khả dụng. Hệ thống sẽ dùng AJAX.');
            return;
        }

        const connection = connectionInfo.connection;
        runtime.hubConnection = connection;

        registerHubEvents(connection, runtime, {
            onReconnecting: function () {
                runtime.hubAvailable = false;
                if (runtime.isStreaming) {
                    finalizeStreaming(runtime);
                    setUiState('idle', elForm, elSendBtn, elStopBtn, elTyping, false);
                }

                showStatus(elStatus, 'Kết nối realtime đang gián đoạn. Hệ thống sẽ fallback sang AJAX.');
            },
            onReconnected: function () {
                runtime.hubAvailable = true;
                hideStatus(elStatus);
            },
            onClosed: function () {
                runtime.hubAvailable = false;
                showStatus(elStatus, 'Realtime đã ngắt kết nối. Hệ thống sẽ dùng AJAX.');
            }
        });

        try {
            if (connectionInfo.readyPromise) {
                await connectionInfo.readyPromise;
            }

            if (connection.state === signalR.HubConnectionState.Disconnected && !connectionInfo.external) {
                await connection.start();
            }

            runtime.hubAvailable = connection.state === signalR.HubConnectionState.Connected;
            if (runtime.hubAvailable) {
                hideStatus(elStatus);
            }
        } catch {
            runtime.hubAvailable = false;
            showStatus(elStatus, 'Không thể khởi tạo realtime. Hệ thống sẽ dùng AJAX.');
        }
    }

    function resolveHubConnection(hubUrl) {
        if (window.userActivityHubConnection) {
            return {
                connection: window.userActivityHubConnection,
                readyPromise: window.userActivityHubReady || Promise.resolve(window.userActivityHubConnection),
                external: true
            };
        }

        if (!window.signalR) {
            return null;
        }

        const reconnectDelays = [0, 2000, 5000, 10000];
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect(reconnectDelays)
            .build();

        return {
            connection: connection,
            readyPromise: null,
            external: false
        };
    }

    function registerHubEvents(connection, runtime, lifecycleHandlers) {
        connection.off('AiStreamStarted');
        connection.off('AiStreamChunk');
        connection.off('AiStreamCompleted');
        connection.off('AiStreamFailed');

        connection.on('AiStreamStarted', function (payload) {
            runtime.activeStreamId = payload?.streamId || null;
        });

        connection.on('AiStreamChunk', function (payload) {
            if (!runtime.isStreaming || !runtime.streamMessageElement) {
                return;
            }

            if (runtime.activeStreamId && payload?.streamId && runtime.activeStreamId !== payload.streamId) {
                return;
            }

            const chatWindow = runtime.streamMessageElement.closest('.ai-chat-window');
            const isNearBottom = isChatWindowNearBottom(chatWindow);

            if (runtime.streamChunkCount === 0) {
                const elTyping = document.getElementById('typing');
                if (elTyping) elTyping.classList.add('d-none');
                if (runtime.streamMessageElement && runtime.streamMessageElement.closest('.ai-chat-message')) {
                    runtime.streamMessageElement.closest('.ai-chat-message').classList.remove('d-none');
                }
            }

            runtime.streamChunkCount += 1;
            runtime.streamBuffer += payload?.chunk || '';
            renderAssistantContent(runtime.streamMessageElement, runtime.streamBuffer);

            if (isNearBottom && chatWindow) {
                scrollToBottom(chatWindow, false, runtime.scrollToBottomButton);
            } else {
                syncScrollAffordance(chatWindow, runtime.scrollToBottomButton);
            }
        });

        connection.on('AiStreamCompleted', function (payload) {
            if (!runtime.isStreaming) {
                return;
            }

            if (runtime.activeStreamId && payload?.streamId && runtime.activeStreamId !== payload.streamId) {
                return;
            }

            finalizeStreaming(runtime);
            resolveStreamCompletion(runtime, true);
        });

        connection.on('AiStreamFailed', function (payload) {
            if (!runtime.isStreaming) {
                return;
            }

            if (runtime.activeStreamId && payload?.streamId && runtime.activeStreamId !== payload.streamId) {
                return;
            }

            const errorCode = payload?.errorCode || '';
            if (errorCode === 'cancelled') {
                finalizeStreaming(runtime);
                resolveStreamCompletion(runtime, true);
                return;
            }

            const shouldFallback = !runtime.streamChunkCount && (errorCode === 'transport_error' || errorCode === 'stream_conflict');

            finalizeStreaming(runtime);
            resolveStreamCompletion(runtime, false, {
                message: payload?.message || 'Realtime không khả dụng.',
                shouldFallback: shouldFallback
            });
        });

        connection.onreconnecting(function () {
            lifecycleHandlers.onReconnecting();
        });

        connection.onreconnected(function () {
            lifecycleHandlers.onReconnected();
        });

        connection.onclose(function () {
            lifecycleHandlers.onClosed();
        });
    }

    async function trySendWithStreaming(runtime, text, conversationId, elChatWindow, elForm, elSendBtn, elStopBtn, elTyping, elStatus) {
        if (!conversationId) {
            return false;
        }

        if (!runtime.hubAvailable || !runtime.hubConnection || runtime.hubConnection.state !== signalR.HubConnectionState.Connected) {
            return false;
        }

        setUiState('sending', elForm, elSendBtn, elStopBtn, elTyping, true);
        runtime.isStreaming = true;
        runtime.streamChunkCount = 0;
        runtime.streamBuffer = '';
        runtime.streamMessageElement = appendStreamingAssistantMessage(elChatWindow, runtime.scrollToBottomButton);
        if (runtime.streamMessageElement && runtime.streamMessageElement.closest('.ai-chat-message')) {
            runtime.streamMessageElement.closest('.ai-chat-message').classList.add('d-none');
        }

        const completionPromise = new Promise(function (resolve) {
            runtime.streamCompletionResolver = resolve;
        });

        runtime.hubConnection.invoke('StartAiStream', conversationId, text)
            .catch(function () {
                if (!runtime.isStreaming) {
                    return;
                }

                const canFallback = runtime.streamChunkCount === 0;
                finalizeStreaming(runtime);
                resolveStreamCompletion(runtime, false, {
                    message: 'Không thể stream realtime. Hệ thống sẽ chuyển sang AJAX.',
                    shouldFallback: canFallback
                });
            });

        const completion = await completionPromise;

        if (completion.success) {
            hideStatus(elStatus);
            return true;
        }

        if (completion.message) {
            showStatus(elStatus, completion.message);
        }

        if (completion.shouldFallback) {
            removeStreamingPlaceholder(runtime.streamMessageElement);
            runtime.streamMessageElement = null;
            syncScrollAffordance(runtime.chatWindowElement, runtime.scrollToBottomButton);
            return false;
        }

        throw new Error(completion.message || 'Realtime thất bại.');
    }

    function finalizeStreaming(runtime) {
        runtime.isStreaming = false;
        runtime.activeStreamId = null;
    }

    function resolveStreamCompletion(runtime, success, details) {
        if (!runtime.streamCompletionResolver) {
            return;
        }

        const resolver = runtime.streamCompletionResolver;
        runtime.streamCompletionResolver = null;
        resolver({
            success: success,
            message: details?.message || '',
            shouldFallback: details?.shouldFallback === true
        });
    }

    function appendStreamingAssistantMessage(elChatWindow, scrollButton) {
        const result = appendMessage(elChatWindow, 'assistant', '', scrollButton);
        return result?.contentElement || null;
    }

    function removeStreamingPlaceholder(element) {
        const container = element?.closest('[data-role]');
        if (container && container.parentElement) {
            container.parentElement.removeChild(container);
        }
    }

    async function sendWithRetry(sendUrl, payload, options) {
        const maxAttempts = options?.allowRetry === false ? 1 : 2;
        const antiForgeryToken = options?.antiForgeryToken || '';

        for (let attempt = 1; attempt <= maxAttempts; attempt++) {
            const response = await fetch(sendUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken
                },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                return await response.json();
            }

            const isRetryable = response.status === 429 || response.status === 503 || response.status >= 500;
            if (attempt < maxAttempts && isRetryable) {
                const retryAfterHeader = response.headers.get('Retry-After');
                const retryAfterSeconds = Number.parseInt(retryAfterHeader || '1', 10);
                await delay(Number.isFinite(retryAfterSeconds) && retryAfterSeconds > 0 ? retryAfterSeconds * 1000 : 1000);
                continue;
            }

            const errorPayloadMessage = await tryGetErrorMessage(response);
            throw new Error(errorPayloadMessage || getFriendlyErrorMessage(response.status));
        }

        throw new Error('Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.');
    }

    function hydrateAssistantMessages(elChatWindow) {
        const assistantNodes = elChatWindow.querySelectorAll('.js-assistant-markdown');
        assistantNodes.forEach(function (node) {
            renderAssistantContent(node, node.textContent || '');
        });
    }

    async function deleteConversationContext(conversationId, deleteUrl, activeConversationId, historyElements, deleteBtn) {
        if (!deleteUrl || !conversationId) throw new Error("Missing parameters");

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const formData = new FormData();
        formData.append('conversationId', conversationId);

        const btnOriginalHtml = deleteBtn.innerHTML;
        deleteBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        deleteBtn.disabled = true;

        const resp = await fetch(deleteUrl, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token,
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: formData
        });

        if (!resp.ok) {
            deleteBtn.innerHTML = btnOriginalHtml;
            deleteBtn.disabled = false;
            throw new Error(resp.status === 404 ? 'Không tìm thấy hội thoại để xóa.' : 'Đã xảy ra lỗi khi xóa hội thoại.');
        }

        const itemWrapper = deleteBtn.closest('.ai-chat-history-item-wrapper');
        if (itemWrapper) {
            itemWrapper.remove();
        }

        if (historyElements.list && Array.from(historyElements.list.querySelectorAll('.ai-chat-history-item-wrapper')).length === 0) {
            historyElements.list.classList.add('d-none');
            if (historyElements.empty) {
                historyElements.empty.classList.remove('d-none');
            }
        }

        if (activeConversationId === conversationId) {
            window.location.href = historyElements.chatPageUrl;
        } else {
            const toastEl = document.getElementById('deleteSuccessToast');
            if (toastEl && window.bootstrap) {
                const toast = new bootstrap.Toast(toastEl);
                toast.show();
            }
        }
    }

    function hydrateTimestamps(historyElements) {
        if (!historyElements?.list) {
            return;
        }

        const timeNodes = historyElements.list.querySelectorAll('[data-timestamp]');
        timeNodes.forEach(function (node) {
            const timestamp = node.getAttribute('data-timestamp');
            if (timestamp) {
                const date = new Date(timestamp);
                if (!isNaN(date.getTime())) {
                    node.textContent = formatConversationTimestamp(date);
                }
            }
        });
    }

    function appendMessage(elChatWindow, role, text, scrollButton) {
        removeEmptyState(elChatWindow);

        const container = document.createElement('div');
        container.className = getMessageContainerClassName(role);
        container.setAttribute('data-role', role);

        const shell = document.querySelector('.ai-chat-shell');
        const userAvatar = shell?.dataset.userAvatar || '';
        const userInitial = shell?.dataset.userInitial || 'U';
        const aiAvatar = shell?.dataset.aiAvatar || '/images/ai/tct-ai-launcher.png';

        if (role === 'assistant' || role === 'system') {
            const avatarDiv = document.createElement('div');
            avatarDiv.className = 'ai-chat-avatar ai-chat-avatar-assistant';
            avatarDiv.setAttribute('aria-hidden', 'true');
            avatarDiv.innerHTML = `<img src="${aiAvatar}" alt="AI Avatar" loading="lazy" />`;
            container.appendChild(avatarDiv);
        }

        const bubble = document.createElement('div');
        bubble.className = getBubbleClassName(role);

        if (role === 'assistant') {
            const wrapper = document.createElement('div');
            wrapper.className = 'js-assistant-markdown';
            renderAssistantContent(wrapper, text);
            bubble.appendChild(wrapper);
        } else {
            bubble.textContent = text;
        }

        container.appendChild(bubble);

        if (role === 'user') {
            const avatarDiv = document.createElement('div');
            avatarDiv.className = 'ai-chat-avatar ai-chat-avatar-user';
            avatarDiv.setAttribute('aria-hidden', 'true');
            if (userAvatar) {
                avatarDiv.innerHTML = `<img src="${userAvatar}" alt="User Avatar" loading="lazy" />`;
            } else {
                avatarDiv.innerHTML = `<div class="ai-chat-avatar-fallback">${userInitial}</div>`;
            }
            container.appendChild(avatarDiv);
        }

        elChatWindow.appendChild(container);
        scrollToBottom(elChatWindow, false, scrollButton);

        return {
            containerElement: container,
            contentElement: bubble.querySelector('.js-assistant-markdown') || bubble
        };
    }

    function removeEmptyState(elChatWindow) {
        setEmptyStateVisibility(elChatWindow, false);
    }

    function setEmptyStateVisibility(elChatWindow, isVisible) {
        const emptyStateElement = elChatWindow?.querySelector('[data-chat-empty-state]');
        if (!emptyStateElement) {
            return;
        }

        emptyStateElement.classList.toggle('d-none', !isVisible);
    }

    function getMessageContainerClassName(role) {
        if (role === 'user') {
            return 'ai-chat-message ai-chat-message-user';
        }

        if (role === 'system') {
            return 'ai-chat-message ai-chat-message-system';
        }

        return 'ai-chat-message ai-chat-message-assistant';
    }

    function getBubbleClassName(role) {
        if (role === 'user') {
            return 'ai-chat-bubble ai-chat-bubble-user';
        }

        if (role === 'system') {
            return 'ai-chat-bubble ai-chat-bubble-system';
        }

        return 'ai-chat-bubble ai-chat-bubble-assistant';
    }

    function renderAssistantContent(element, markdownText) {
        if (window.marked && window.DOMPurify) {
            const rawHtml = window.marked.parse(markdownText || '');
            const safeHtml = window.DOMPurify.sanitize(rawHtml);
            element.innerHTML = safeHtml;
            return;
        }

        element.textContent = markdownText;
    }

    function setUiState(state, elForm, elSendBtn, elStopBtn, elTyping, isStreaming) {
        elForm.dataset.state = state;
        elForm.dataset.streaming = isStreaming ? 'true' : 'false';
        elSendBtn.disabled = state === 'sending';

        if (state === 'sending' && elTyping) {
            const chatWindow = document.getElementById('chatWindow');
            if (chatWindow) {
                chatWindow.appendChild(elTyping);
            }
            elTyping.classList.remove('d-none');
            scrollToBottom(chatWindow, false, document.getElementById('scrollToBottomBtn'));
        } else if (elTyping) {
            elTyping.classList.add('d-none');
        }

        elStopBtn.classList.toggle('d-none', !isStreaming);
        elStopBtn.disabled = !isStreaming;
    }

    function setQuickActionsBusy(elQuickActions, isBusy) {
        if (!elQuickActions) {
            return;
        }

        elQuickActions.setAttribute('aria-busy', isBusy ? 'true' : 'false');
        elQuickActions.querySelectorAll('[data-ai-quick-action]').forEach(function (button) {
            button.disabled = isBusy;
        });
    }

    function markQuickActionsAccepted(elQuickActions) {
        if (!elQuickActions) {
            return;
        }

        setQuickActionsBusy(elQuickActions, false);
        elQuickActions.classList.add('d-none');
        elQuickActions.setAttribute('aria-hidden', 'true');
    }

    function restoreQuickActions(elQuickActions, elChatWindow) {
        if (!elQuickActions) {
            return;
        }

        setEmptyStateVisibility(elChatWindow, true);
        setQuickActionsBusy(elQuickActions, false);
        elQuickActions.classList.remove('d-none');
        elQuickActions.setAttribute('aria-hidden', 'false');
    }

    function scrollToBottom(elChatWindow, smooth, scrollButton) {
        if (!elChatWindow) return;

        if (smooth) {
            elChatWindow.scrollTo({
                top: elChatWindow.scrollHeight,
                behavior: 'smooth'
            });
        } else {
            elChatWindow.scrollTop = elChatWindow.scrollHeight;
        }

        window.requestAnimationFrame(function () {
            syncScrollAffordance(elChatWindow, scrollButton);
        });
    }

    function showStatus(elStatus, message) {
        if (!elStatus) {
            return;
        }

        elStatus.textContent = message;
        elStatus.classList.remove('d-none');
    }

    function hideStatus(elStatus) {
        if (!elStatus) {
            return;
        }

        elStatus.textContent = '';
        elStatus.classList.add('d-none');
    }

    function getFriendlyErrorMessage(statusCode) {
        if (statusCode === 400) {
            return 'Nội dung không hợp lệ. Vui lòng kiểm tra và thử lại.';
        }

        if (statusCode === 401 || statusCode === 403) {
            return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
        }

        if (statusCode === 404) {
            return 'Không tìm thấy cuộc hội thoại hoặc bạn không có quyền truy cập.';
        }

        if (statusCode === 409) {
            return 'Cuộc hội thoại này đang có một yêu cầu AI khác đang xử lý. Vui lòng chờ phản hồi hiện tại hoàn tất.';
        }

        if (statusCode === 429) {
            return 'Bạn đang gửi quá nhanh. Vui lòng chờ một chút rồi thử lại.';
        }

        return 'Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.';
    }

    async function tryGetErrorMessage(response) {
        const contentType = response.headers.get('Content-Type') || '';
        if (!contentType.includes('application/json')) {
            return '';
        }

        try {
            const payload = await response.json();
            return payload?.error || '';
        } catch {
            return '';
        }
    }

    function delay(milliseconds) {
        return new Promise(function (resolve) {
            window.setTimeout(resolve, milliseconds);
        });
    }

    function notifyLauncherQuotaConsumed() {
        if (typeof window === 'undefined' || window.top === window.self) {
            return;
        }

        try {
            window.parent.postMessage({
                type: LAUNCHER_QUOTA_SYNC_EVENT,
                consumed: 1
            }, window.location.origin);
        } catch {
            // no-op
        }
    }

    function isChatWindowNearBottom(elChatWindow) {
        if (!elChatWindow) {
            return true;
        }

        const distanceToBottom = elChatWindow.scrollHeight - elChatWindow.scrollTop - elChatWindow.clientHeight;
        return distanceToBottom <= SCROLL_BOTTOM_THRESHOLD;
    }

    function syncScrollAffordance(elChatWindow, scrollButton) {
        if (!scrollButton) {
            return;
        }

        if (!elChatWindow) {
            setScrollButtonVisibility(scrollButton, false);
            return;
        }

        const hasOverflow = elChatWindow.scrollHeight - elChatWindow.clientHeight > SCROLL_BOTTOM_THRESHOLD;
        const shouldShowButton = hasOverflow && !isChatWindowNearBottom(elChatWindow);
        setScrollButtonVisibility(scrollButton, shouldShowButton);
    }

    function setScrollButtonVisibility(scrollButton, isVisible) {
        if (!scrollButton) {
            return;
        }

        scrollButton.classList.toggle('show', isVisible);
        scrollButton.hidden = !isVisible;
        scrollButton.setAttribute('aria-hidden', isVisible ? 'false' : 'true');
    }

    function getConversationIdValue(elConversationId) {
        return (elConversationId?.value || '').trim();
    }

    function updateConversationContext(elConversationId, elConversationMetaValue, elConversationMetaStatus, historyElements, conversationId, conversationTitle) {
        const normalizedConversationId = (conversationId || '').toString().trim();
        if (!normalizedConversationId) {
            return;
        }

        const normalizedConversationTitle = buildConversationTitle(conversationTitle, 'Đoạn chat mới');

        if (elConversationId) {
            elConversationId.value = normalizedConversationId;
        }

        updateConversationMeta(elConversationMetaValue, elConversationMetaStatus, normalizedConversationTitle);
        touchConversationHistory(historyElements, normalizedConversationId, normalizedConversationTitle);
        syncConversationQueryString(normalizedConversationId);
    }

    function updateConversationMeta(elConversationMetaValue, elConversationMetaStatus, conversationTitle) {
        if (elConversationMetaValue) {
            elConversationMetaValue.textContent = conversationTitle;
            elConversationMetaValue.title = conversationTitle;
        }

        if (elConversationMetaStatus) {
            elConversationMetaStatus.textContent = ACTIVE_CONVERSATION_STATUS;
        }
    }

    function touchConversationHistory(historyElements, conversationId, conversationTitle) {
        if (!historyElements?.list || !conversationId) {
            return;
        }

        const existingHistoryAnchor = historyElements.list.querySelector(`[data-conversation-id="${conversationId}"]`);
        let historyItemWrapper = existingHistoryAnchor?.closest('.ai-chat-history-item-wrapper');

        if (!historyItemWrapper) {
            historyItemWrapper = createConversationHistoryItem(historyElements.chatPageUrl, conversationId);
            historyElements.list.prepend(historyItemWrapper);
        }

        updateConversationHistoryItem(
            historyItemWrapper,
            buildConversationTitle(conversationTitle || getConversationHistoryTitle(historyItemWrapper), 'Đoạn chat mới'),
            formatConversationTimestamp(new Date()));
        historyElements.list.prepend(historyItemWrapper);
        setActiveConversationHistoryItem(historyElements.list, conversationId);
        syncHistoryVisibility(historyElements);
    }

    function createConversationHistoryItem(chatPageUrl, conversationId) {
        const historyItemWrapper = document.createElement('div');
        historyItemWrapper.className = 'ai-chat-history-item-wrapper';
        historyItemWrapper.dataset.aiHistoryItemWrapper = '';

        const historyItem = document.createElement('a');
        historyItem.className = 'ai-chat-history-item';
        historyItem.dataset.aiHistoryItem = '';
        historyItem.dataset.conversationId = conversationId;
        historyItem.href = buildConversationHref(chatPageUrl, conversationId);

        const titleElement = document.createElement('span');
        titleElement.className = 'ai-chat-history-item-title';

        const timeElement = document.createElement('span');
        timeElement.className = 'ai-chat-history-item-time';

        historyItem.appendChild(titleElement);
        historyItem.appendChild(timeElement);

        const deleteButton = document.createElement('button');
        deleteButton.type = 'button';
        deleteButton.className = 'ai-chat-history-delete-btn';
        deleteButton.setAttribute('data-ai-history-delete', conversationId);
        deleteButton.setAttribute('aria-label', 'Xóa hội thoại');
        deleteButton.innerHTML = '<i class="fas fa-trash-alt"></i>';

        historyItemWrapper.appendChild(historyItem);
        historyItemWrapper.appendChild(deleteButton);

        return historyItemWrapper;
    }

    function updateConversationHistoryItem(historyItem, title, timestampLabel) {
        const titleElement = historyItem.querySelector('.ai-chat-history-item-title');
        const timeElement = historyItem.querySelector('.ai-chat-history-item-time');

        if (titleElement) {
            titleElement.textContent = title;
        }

        if (timeElement) {
            timeElement.textContent = timestampLabel;
            timeElement.setAttribute('data-timestamp', new Date().toISOString());
        }
    }

    function getConversationHistoryTitle(historyItem) {
        return historyItem?.querySelector('.ai-chat-history-item-title')?.textContent || '';
    }

    function setActiveConversationHistoryItem(historyList, conversationId) {
        if (!historyList) {
            return;
        }

        historyList.querySelectorAll('[data-ai-history-item]').forEach(function (historyItem) {
            const isActive = historyItem.dataset.conversationId === conversationId;
            historyItem.classList.toggle('active', isActive);

            if (isActive) {
                historyItem.setAttribute('aria-current', 'page');
            } else {
                historyItem.removeAttribute('aria-current');
            }
        });
    }

    function syncHistoryVisibility(historyElements) {
        const historyCount = historyElements?.list?.children?.length || 0;
        const hasHistory = historyCount > 0;

        if (historyElements?.list) {
            historyElements.list.classList.toggle('d-none', !hasHistory);
        }

        if (historyElements?.empty) {
            historyElements.empty.classList.toggle('d-none', hasHistory);
        }
    }

    function buildConversationTitle(rawTitle, fallbackTitle) {
        const normalizedTitle = normalizeWhitespace(rawTitle);
        if (!normalizedTitle) {
            return fallbackTitle || '';
        }

        return normalizedTitle.length <= MAX_CONVERSATION_TITLE_LENGTH
            ? normalizedTitle
            : normalizedTitle.slice(0, MAX_CONVERSATION_TITLE_LENGTH);
    }

    function normalizeWhitespace(value) {
        return (value || '')
            .trim()
            .replace(/\s+/g, ' ');
    }

    function formatConversationTimestamp(date) {
        try {
            const pad = function (n) { return String(n).padStart(2, '0'); };
            const day = pad(date.getDate());
            const month = pad(date.getMonth() + 1);
            const year = date.getFullYear();
            const hours = pad(date.getHours());
            const minutes = pad(date.getMinutes());

            return `${day}/${month}/${year} ${hours}:${minutes}`;
        } catch {
            return '';
        }
    }

    function buildConversationHref(chatPageUrl, conversationId) {
        return `${chatPageUrl}?conversationId=${encodeURIComponent(conversationId)}`;
    }

    function syncConversationQueryString(conversationId) {
        if (!conversationId || typeof window === 'undefined') {
            return;
        }

        if (window.top !== window.self) {
            return;
        }

        const currentUrl = new URL(window.location.href);
        if (currentUrl.searchParams.get('embed') === 'true') {
            return;
        }

        if (currentUrl.searchParams.get('conversationId') === conversationId) {
            return;
        }

        currentUrl.searchParams.set('conversationId', conversationId);
        window.history.replaceState(window.history.state, '', currentUrl.toString());
    }

    async function refreshObservability(observabilityUrl, elements) {
        if (!observabilityUrl) {
            return;
        }

        try {
            const response = await fetch(`${observabilityUrl}?days=7`, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                return;
            }

            const payload = await response.json();

            if (elements.requestCount) {
                elements.requestCount.textContent = payload.requestCount ?? 0;
            }

            if (elements.errorRate) {
                const errorRate = Number(payload.errorRatePercent ?? 0).toFixed(2);
                elements.errorRate.textContent = `${errorRate}% (${payload.errorCount ?? 0})`;
            }

            if (elements.latency) {
                const p50 = payload.latencyP50Ms ?? '-';
                const p95 = payload.latencyP95Ms ?? '-';
                elements.latency.textContent = `${p50}/${p95} ms`;
            }

            if (elements.tokenToday) {
                const used = payload.tokensUsedToday ?? 0;
                const budget = payload.dailyTokenBudget ?? 0;
                const percent = Number(payload.budgetUsagePercent ?? 0).toFixed(2);
                elements.tokenToday.textContent = budget > 0
                    ? `${used}/${budget} (${percent}%)`
                    : `${used}`;
            }
        } catch {
            // no-op
        }
    }

    window.tctAiChat = {
        init: init
    };
})();
