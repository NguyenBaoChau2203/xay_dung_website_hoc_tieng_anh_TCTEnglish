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

        let isSubmitting = false;

        const runtime = {
            sendUrl: options?.sendUrl || '',
            observabilityUrl: options?.observabilityUrl || '',
            hubUrl: options?.hubUrl || '/classChatHub',
            chatWindowElement: elChatWindow,
            hubConnection: null,
            hubAvailable: false,
            activeStreamId: null,
            activeConversationId: null,
            isStreaming: false,
            scrollToBottomButton: elScrollToBottomBtn,
            streamGeneration: 0,
            streamAttempt: null,
            retiredStreamIds: new Set(),
            streamChunks: new Map(),
            streamMessageElement: null,
            pendingDraft: '',
            streamCompletionResolver: null
        };

        hydrateAssistantMessages(elChatWindow);
        hydrateTimestamps(historyElements);
        bindLoadOlderControls(elChatWindow, historyElements, elChatShell);

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

        const elHistoryDisclosure = document.getElementById('aiHistoryDisclosure');
        if (elHistoryDisclosure && window.innerWidth >= 992) {
            elHistoryDisclosure.open = true;
        }

        adjustTextareaHeight(elInput);

        elInput.addEventListener('input', function () {
            adjustTextareaHeight(elInput);
        });

        elInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                if (e.isComposing || e.keyCode === 229) {
                    return;
                }
                e.preventDefault();
                if (isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending') {
                    return;
                }
                if (typeof elForm.requestSubmit === 'function') {
                    elForm.requestSubmit();
                } else {
                    elForm.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
                }
            }
        });
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

        const elRetryBtn = document.getElementById('chatRetryBtn');
        if (elRetryBtn) {
            elRetryBtn.addEventListener('click', function () {
                if (isSubmitting || elForm.dataset.state === 'sending') {
                    return;
                }

                if (typeof elForm.requestSubmit === 'function') {
                    elForm.requestSubmit();
                } else {
                    elForm.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
                }
            });
        }

        elStopBtn.addEventListener('click', async function () {
            if (!runtime.isStreaming || !runtime.hubConnection) {
                return;
            }

            const stoppedStreamId = runtime.activeStreamId;
            if (runtime.streamAttempt) {
                runtime.streamAttempt.cancelled = true;
            }
            elStopBtn.disabled = true;
            runtime.streamGeneration += 1;
            discardUnacceptedStream(runtime);
            finalizeStreaming(runtime);
            resolveStreamCompletion(runtime, true);
            restoreDraftIfEmpty(elInput, runtime.pendingDraft);

            if (!stoppedStreamId) {
                elStopBtn.disabled = false;
                return;
            }

            try {
                await runtime.hubConnection.invoke('StopAiStream', stoppedStreamId);
            } catch {
                showStatus(elStatus, 'Không thể xác nhận dừng phản hồi. Hãy kiểm tra lịch sử trước khi gửi lại.', { allowRetry: true });
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

                if (isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending' || elForm.dataset.quickActionPending === 'true') {
                    return;
                }

                const question = (btn.getAttribute('data-question') || btn.textContent || '').trim();
                if (!question) {
                    return;
                }

                elInput.value = question;
                adjustTextareaHeight(elInput);
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

            if (isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending') {
                return;
            }

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

            isSubmitting = true;
            runtime.pendingDraft = text;

            if (quickActionPending) {
                elForm.dataset.quickActionPending = 'false';
            }

            removeTrailingTransportNotice(elChatWindow);
            if (!shouldReuseUserBubble(elChatWindow, text)) {
                appendMessage(elChatWindow, 'user', text, elScrollToBottomBtn);
            }
            elInput.value = '';
            adjustTextareaHeight(elInput);
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
                    const appended = appendMessage(elChatWindow, 'assistant', payload.text || '', elScrollToBottomBtn);
                    attachGrounding(appended?.contentElement?.parentElement, payload.grounding);
                } else if (currentConversationId) {
                    touchConversationHistory(
                        historyElements,
                        currentConversationId,
                        buildConversationTitle(elConversationMetaValue?.textContent || '', 'Đoạn chat hiện tại'));
                }

                notifyLauncherQuotaConsumed();
                runtime.pendingDraft = '';

                if (quickActionTriggered) {
                    markQuickActionsAccepted(elQuickActions);
                }

                setUiState('done', elForm, elSendBtn, elStopBtn, elTyping, false);
            } catch (error) {
                setUiState('error', elForm, elSendBtn, elStopBtn, elTyping, false);
                const learnerMessage = describeSendError(error);
                restoreDraftIfEmpty(elInput, runtime.pendingDraft);
                showStatus(elStatus, learnerMessage, { allowRetry: true });
                removeTrailingTransportNotice(elChatWindow);
                const notice = appendMessage(elChatWindow, 'system', learnerMessage, elScrollToBottomBtn);
                if (notice?.containerElement) {
                    notice.containerElement.setAttribute('data-answer-state', 'temporary_unavailability');
                }

                if (quickActionTriggered && !getConversationIdValue(elConversationId)) {
                    restoreQuickActions(elQuickActions, elChatWindow);
                }
            } finally {
                isSubmitting = false;
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
                    resolveStreamCompletion(runtime, false, { message: 'Kết nối bị gián đoạn. Hãy kiểm tra lịch sử trước khi gửi lại.', shouldFallback: false });
                }

                showStatus(elStatus, 'Kết nối realtime đang gián đoạn. Hãy kiểm tra lịch sử trước khi gửi lại.');
            },
            onReconnected: function () {
                runtime.hubAvailable = true;
                hideStatus(elStatus);
            },
            onClosed: function () {
                runtime.hubAvailable = false;
                if (runtime.isStreaming) {
                    finalizeStreaming(runtime);
                    resolveStreamCompletion(runtime, false, { message: 'Kết nối đã ngắt. Hãy kiểm tra lịch sử trước khi gửi lại.', shouldFallback: false });
                }
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
            const attempt = runtime.streamAttempt;
            if (!attempt || !payload?.streamId || runtime.retiredStreamIds.has(payload.streamId)) {
                return;
            }

            if (payload.conversationId && String(attempt.conversationId) !== String(payload.conversationId)) {
                return;
            }

            if (attempt.streamId && attempt.streamId !== payload.streamId) {
                return;
            }

            attempt.streamId = payload.streamId;
            if (attempt.cancelled) {
                retireStream(runtime, payload.streamId);
                // Stop can precede admission. Cancel as soon as the server supplies its id.
                connection.invoke('StopAiStream', payload.streamId).catch(function () {});
                return;
            }

            if (!runtime.isStreaming || attempt.generation !== runtime.streamGeneration) {
                return;
            }

            runtime.activeStreamId = payload.streamId;
            const early = runtime.earlyChunks?.get(payload.streamId);
            if (!early) {
                return;
            }

            early.forEach(function (chunk, index) {
                if (!runtime.streamChunks.has(index)) {
                    runtime.streamChunks.set(index, chunk);
                }
            });
            runtime.earlyChunks.delete(payload.streamId);
        });

        connection.on('AiStreamChunk', function (payload) {
            if (!runtime.isStreaming || !runtime.streamMessageElement || !payload?.streamId) {
                return;
            }

            if (runtime.activeConversationId && payload.conversationId && String(runtime.activeConversationId) !== String(payload.conversationId)) {
                return;
            }

            if (runtime.activeStreamId && payload.streamId !== runtime.activeStreamId) {
                return;
            }

            const index = Number.isInteger(payload?.chunkIndex) ? payload.chunkIndex : runtime.streamChunks.size;
            if (runtime.retiredStreamIds.has(payload.streamId)) {
                return;
            }

            const bucket = chunkBucket(runtime, payload.streamId);
            if (bucket.has(index)) {
                return;
            }

            bucket.set(index, payload?.chunk || '');
        });

        connection.on('AiStreamCompleted', function (payload) {
            if (!streamEventCurrent(runtime, payload) || !runtime.streamMessageElement) {
                return;
            }

            const text = acceptedStreamText(runtime);
            renderAssistantContent(runtime.streamMessageElement, text);
            const row = messageRow(runtime.streamMessageElement);
            if (row) {
                row.classList.remove('d-none');
                row.setAttribute('data-accepted', 'true');
            }
            attachGrounding(runtime.streamMessageElement.parentElement || runtime.streamMessageElement.parentNode, payload?.grounding);
            finalizeStreaming(runtime);
            resolveStreamCompletion(runtime, true);
        });

        connection.on('AiStreamFailed', function (payload) {
            if (!streamEventCurrent(runtime, payload, false)) {
                return;
            }

            discardUnacceptedStream(runtime);
            const errorCode = payload?.errorCode || '';
            if (errorCode === 'cancelled') {
                finalizeStreaming(runtime);
                resolveStreamCompletion(runtime, true);
                return;
            }

            const shouldFallback = false;

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
        // The server's invocation finishes after its final event. Do not let a
        // stopped invocation's late admission/failure attach to another turn.
        if (runtime.streamAttempt && !runtime.streamAttempt.settled) {
            throw new Error('Yêu cầu trước đang kết thúc. Hãy chờ một chút và kiểm tra lịch sử trước khi gửi lại.');
        }

        if (!conversationId) {
            return false;
        }

        if (!runtime.hubAvailable || !runtime.hubConnection || runtime.hubConnection.state !== signalR.HubConnectionState.Connected) {
            return false;
        }

        setUiState('sending', elForm, elSendBtn, elStopBtn, elTyping, true);
        runtime.streamGeneration += 1;
        const attempt = {
            generation: runtime.streamGeneration,
            conversationId: conversationId,
            streamId: null,
            cancelled: false,
            settled: false
        };
        runtime.streamAttempt = attempt;
        runtime.isStreaming = true;
        runtime.activeConversationId = conversationId;
        runtime.streamChunks = new Map();
        runtime.earlyChunks = new Map();
        runtime.streamMessageElement = appendStreamingAssistantMessage(elChatWindow, runtime.scrollToBottomButton);
        const pendingRow = messageRow(runtime.streamMessageElement);
        if (pendingRow) {
            pendingRow.classList.add('d-none');
        }

        const completionPromise = new Promise(function (resolve) {
            runtime.streamCompletionResolver = resolve;
        });

        runtime.hubConnection.invoke('StartAiStream', conversationId, text)
            .then(function () { attempt.settled = true; })
            .catch(function () {
                attempt.settled = true;
                if (!runtime.isStreaming || runtime.streamAttempt !== attempt || attempt.generation !== runtime.streamGeneration) {
                    return;
                }

                finalizeStreaming(runtime);
                resolveStreamCompletion(runtime, false, {
                    message: 'Kết nối bị gián đoạn sau khi gửi. Hãy kiểm tra lịch sử trước khi gửi lại.',
                    shouldFallback: false
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
        retireStream(runtime, runtime.activeStreamId);
        runtime.isStreaming = false;
        runtime.activeStreamId = null;
        if (runtime.earlyChunks) {
            runtime.earlyChunks.clear();
        }
    }

    function retireStream(runtime, streamId) {
        if (!streamId) return;
        runtime.retiredStreamIds.add(streamId);
        if (runtime.retiredStreamIds.size > 128) {
            runtime.retiredStreamIds.delete(runtime.retiredStreamIds.values().next().value);
        }
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
        // An HTTP error does not prove that the provider did not process the turn.
        // Retry only through an explicit new user action until transport intent IDs exist.
        const maxAttempts = 1;
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
        const aiAvatar = shell?.dataset.aiAvatar || '/images/ai/tct-ai-mark.svg';

        if (role === 'assistant' || role === 'system') {
            const avatarDiv = document.createElement('div');
            avatarDiv.className = 'ai-chat-avatar ai-chat-avatar-assistant';
            avatarDiv.setAttribute('aria-hidden', 'true');
            avatarDiv.innerHTML = `<img src="${aiAvatar}" alt="TCT AI" loading="lazy" />`;
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

    function attachGrounding(bubble, grounding) {
        if (!bubble) {
            return;
        }

        const existing = typeof bubble.querySelector === 'function' ? bubble.querySelector('.ai-chat-sources') : null;
        if (existing && typeof existing.remove === 'function') {
            existing.remove();
        }

        if (!grounding || typeof grounding !== 'object') {
            return;
        }

        const status = typeof grounding.status === 'string' ? grounding.status : '';
        const outcome = typeof grounding.outcome === 'string' ? grounding.outcome : '';
        const container = messageContainer(bubble);
        if (container) {
            container.setAttribute('data-grounding-status', status);
            container.setAttribute('data-grounding-outcome', outcome);
        }

        const label = learnerStateLabel(grounding);
        const showNotice = (status === 'changed' || status === 'snapshot')
            && typeof grounding.notice === 'string'
            && grounding.notice.trim();
        const sources = Array.isArray(grounding.sources) ? grounding.sources : [];
        if (!label && !showNotice && sources.length === 0) {
            return;
        }

        const section = document.createElement('section');
        section.className = 'ai-chat-sources';
        section.setAttribute('aria-label', 'Nguồn và trạng thái câu trả lời');

        if (label) {
            const state = document.createElement('p');
            state.className = 'ai-chat-state';
            state.setAttribute('data-answer-state', outcome || status);
            state.textContent = label;
            section.appendChild(state);
        }

        if (showNotice) {
            const note = document.createElement('p');
            note.className = 'ai-chat-source-notice';
            note.textContent = grounding.notice;
            section.appendChild(note);
        }

        const visible = sources.filter((source) => source && typeof source.sourceKey === 'string' && source.sourceKey);
        if (visible.length > 0) {
            const heading = document.createElement('p');
            heading.className = 'ai-chat-source-label';
            heading.textContent = 'Nguồn';
            const list = document.createElement('ul');
            list.className = 'ai-chat-source-list';
            list.setAttribute('aria-label', 'Nguồn');
            visible.forEach((source) => {
                const item = document.createElement('li');
                item.setAttribute('data-source-key', source.sourceKey);
                item.setAttribute('data-source-freshness', typeof source.freshness === 'string' ? source.freshness : '');
                const title = typeof source.title === 'string' && source.title.trim()
                    ? source.title.trim()
                    : 'Nguồn đã lưu';
                if (isSafeCitationRoute(source.route)) {
                    const link = document.createElement('a');
                    link.className = 'ai-chat-source-link';
                    link.href = source.route;
                    link.textContent = title;
                    item.appendChild(link);
                } else {
                    const span = document.createElement('span');
                    span.className = 'ai-chat-source-title';
                    span.textContent = title;
                    item.appendChild(span);
                }

                if (source.freshness === 'changed' || source.freshness === 'snapshot') {
                    const fresh = document.createElement('span');
                    fresh.className = 'ai-chat-source-freshness';
                    fresh.textContent = source.freshness === 'changed'
                        ? 'Nguồn đã thay đổi sau khi trả lời'
                        : 'Số liệu tại thời điểm trả lời';
                    item.appendChild(fresh);
                }

                list.appendChild(item);
            });
            section.appendChild(heading);
            section.appendChild(list);
        }

        bubble.appendChild(section);
    }

    function learnerStateLabel(grounding) {
        const supplied = grounding.stateLabel;
        if (typeof supplied === 'string' && supplied.trim()) {
            return supplied.trim();
        }

        const status = grounding.status || '';
        if (status === 'unavailable') return 'Tạm thời không xem được nguồn';
        if (status === 'unverified') return 'Chưa xác minh được nguồn';
        if (status === 'changed') return 'Nguồn đã thay đổi sau khi trả lời';
        if (status === 'snapshot') return 'Số liệu tại thời điểm trả lời';
        switch (grounding.outcome) {
            case 'answered': return 'Đã trả lời từ nguồn được phép xem';
            case 'needs_clarification': return 'Cần làm rõ thêm';
            case 'insufficient_evidence': return 'Chưa đủ căn cứ';
            case 'out_of_scope': return 'Ngoài phạm vi trợ lý';
            case 'provider_unavailable': return 'Trợ lý tạm thời không trả lời được';
            case 'access_denied': return 'Không còn quyền xem nguồn này';
            case 'quota_exceeded': return 'Đã hết lượt hỏi hôm nay';
            case 'cancelled': return 'Đã dừng câu trả lời';
            default: return '';
        }
    }

    function isSafeCitationRoute(route) {
        if (typeof route !== 'string' || !route.startsWith('/') || route.startsWith('//') || route.toLowerCase().startsWith('/admin')) {
            return false;
        }

        if (route.includes('://') || route.includes('\\') || route.includes('<') || route.includes('>') || route.includes('"') || route.includes('\n') || route.includes('\r') || route.includes(' ') || route.includes('..')) {
            return false;
        }

        let path = route;
        const queryIndex = route.indexOf('?');
        if (queryIndex >= 0) {
            path = route.slice(0, queryIndex);
            const query = route.slice(queryIndex + 1);
            const match = /^exerciseId=([1-9]\d*)$/.exec(query);
            if (!match || String(Number(match[1])) !== match[1]) {
                return false;
            }
        }

        return path.length > 1 && path.length <= 200 && /^[A-Za-z0-9/_-]+$/.test(path);
    }

    function streamEventCurrent(runtime, payload, requireStreamId) {
        if (!runtime.isStreaming || !payload) {
            return false;
        }

        if (payload.streamId) {
            if (!runtime.activeStreamId || payload.streamId !== runtime.activeStreamId) {
                return false;
            }
        } else if (requireStreamId !== false) {
            return false;
        }

        if (runtime.activeConversationId && payload.conversationId && String(runtime.activeConversationId) !== String(payload.conversationId)) {
            return false;
        }

        return true;
    }

    function chunkBucket(runtime, streamId) {
        if (runtime.activeStreamId === streamId) {
            return runtime.streamChunks;
        }

        if (!runtime.earlyChunks) {
            runtime.earlyChunks = new Map();
        }

        if (!runtime.earlyChunks.has(streamId)) {
            if (runtime.earlyChunks.size >= 4) {
                return new Map();
            }

            runtime.earlyChunks.set(streamId, new Map());
        }

        return runtime.earlyChunks.get(streamId);
    }

    function acceptedStreamText(runtime) {
        return Array.from(runtime.streamChunks.keys())
            .sort((left, right) => left - right)
            .map((index) => runtime.streamChunks.get(index) || '')
            .join('');
    }

    function messageRow(element) {
        let node = element;
        while (node) {
            const className = typeof node.className === 'string' ? node.className : '';
            if (className.split(/\s+/).includes('ai-chat-message')) {
                return node;
            }

            node = node.parentNode || null;
        }

        return null;
    }

    function messageContainer(bubble) {
        let node = bubble;
        while (node) {
            if (typeof node.getAttribute === 'function' && node.getAttribute('data-role')) {
                return node;
            }

            node = node.parentNode || null;
        }

        return null;
    }

    function discardUnacceptedStream(runtime) {
        const row = messageRow(runtime.streamMessageElement);
        if (row && row.getAttribute('data-accepted') !== 'true') {
            removeStreamingPlaceholder(runtime.streamMessageElement);
            runtime.streamMessageElement = null;
            runtime.streamChunks = new Map();
            if (runtime.earlyChunks) {
                runtime.earlyChunks.clear();
            }
        }
    }

    function adjustTextareaHeight(textarea) {
        if (!textarea || textarea.tagName !== 'TEXTAREA') {
            return;
        }

        if (!textarea.value || !textarea.value.trim()) {
            textarea.style.height = '36px';
            textarea.style.overflowY = 'hidden';
            return;
        }

        textarea.style.height = 'auto';
        const maxHeight = 120;
        const newHeight = Math.min(textarea.scrollHeight, maxHeight);
        textarea.style.height = Math.max(newHeight, 36) + 'px';
        textarea.style.overflowY = textarea.scrollHeight > maxHeight ? 'auto' : 'hidden';
    }

    function restoreDraftIfEmpty(input, draft) {
        if (!input || (input.value || '').trim() || !draft) {
            return;
        }

        input.value = draft;
        adjustTextareaHeight(input);
    }

    function removeTrailingTransportNotice(chatWindow) {
        if (!chatWindow || typeof chatWindow.querySelectorAll !== 'function') {
            return;
        }

        const rows = Array.from(chatWindow.querySelectorAll('[data-role]'));
        for (let index = rows.length - 1; index >= 0; index -= 1) {
            const row = rows[index];
            if (row.id === 'typing') {
                continue;
            }

            if (row.getAttribute('data-role') !== 'system' || row.getAttribute('data-answer-state') !== 'temporary_unavailability') {
                return;
            }

            if (typeof row.remove === 'function') {
                row.remove();
            }
        }
    }

    function shouldReuseUserBubble(chatWindow, text) {
        if (!chatWindow || typeof chatWindow.querySelectorAll !== 'function') {
            return false;
        }

        const rows = Array.from(chatWindow.querySelectorAll('[data-role]'));
        for (let index = rows.length - 1; index >= 0; index -= 1) {
            if (rows[index].id === 'typing') {
                continue;
            }

            const role = rows[index].getAttribute('data-role');
            if (role === 'system' && rows[index].getAttribute('data-answer-state') === 'temporary_unavailability') {
                continue;
            }

            if (role === 'assistant') {
                return false;
            }

            if (role === 'user') {
                const bubble = typeof rows[index].querySelector === 'function'
                    ? rows[index].querySelector('.ai-chat-bubble')
                    : null;
                return ((bubble && bubble.textContent) || '').trim() === text;
            }
        }

        return false;
    }

    function describeSendError(error) {
        const message = (error && error.message) || '';
        if (!message || message === 'Failed to fetch' || message === 'NetworkError when attempting to fetch resource.' || message === 'Load failed') {
            return 'Mạng bị gián đoạn. Bản nháp vẫn còn để bạn gửi lại.';
        }

        return message;
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

        const prefersReducedMotion = typeof window !== 'undefined'
            && typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (smooth && !prefersReducedMotion) {
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

    function showStatus(elStatus, message, options) {
        if (!elStatus) {
            return;
        }

        const textNode = typeof elStatus.querySelector === 'function'
            ? elStatus.querySelector('[data-ai-status-text]')
            : null;
        if (textNode) {
            textNode.textContent = message;
        } else {
            elStatus.textContent = message;
        }

        elStatus.classList.remove('d-none');
        const retry = document.getElementById('chatRetryBtn');
        if (retry) {
            retry.classList.toggle('d-none', options?.allowRetry !== true);
        }
    }

    function hideStatus(elStatus) {
        if (!elStatus) {
            return;
        }

        const textNode = typeof elStatus.querySelector === 'function'
            ? elStatus.querySelector('[data-ai-status-text]')
            : null;
        if (textNode) {
            textNode.textContent = '';
        } else {
            elStatus.textContent = '';
        }

        const retry = document.getElementById('chatRetryBtn');
        if (retry) {
            retry.classList.add('d-none');
        }

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

        if (statusCode === 503 || statusCode === 504) {
            return 'Trợ lý tạm thời không trả lời được. Bản nháp vẫn còn để bạn gửi lại.';
        }

        return 'Trợ lý tạm thời không trả lời được. Bản nháp vẫn còn để bạn gửi lại.';
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

    function bindLoadOlderControls(chatWindow, historyElements, chatShell) {
        document.querySelectorAll('[data-ai-load-older]').forEach((button) => {
            button.addEventListener('click', async () => {
                if (button.disabled) {
                    return;
                }

                const kind = button.getAttribute('data-ai-load-older');
                const url = button.getAttribute('data-ai-load-older-url');
                if (!url) {
                    return;
                }

                button.disabled = true;
                try {
                    if (kind === 'conversations') {
                        await loadOlderConversations(button, historyElements);
                    } else if (kind === 'messages') {
                        await loadOlderMessages(button, chatWindow, chatShell);
                    }
                } finally {
                    button.disabled = false;
                }
            });
        });
    }

    async function loadOlderConversations(button, historyElements) {
        const list = historyElements.list;
        if (!list) {
            return;
        }

        const beforeUpdatedAtUtc = list.getAttribute('data-ai-history-cursor-time') || '';
        const beforeId = list.getAttribute('data-ai-history-cursor-id') || '';
        if (!beforeUpdatedAtUtc || !beforeId) {
            return;
        }

        const url = new URL(button.getAttribute('data-ai-load-older-url'), window.location.origin);
        url.searchParams.set('beforeUpdatedAtUtc', beforeUpdatedAtUtc);
        url.searchParams.set('beforeId', beforeId);

        const response = await fetch(url.toString(), {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });
        if (!response.ok) {
            return;
        }

        const payload = await response.json();
        const items = Array.isArray(payload.items) ? payload.items : [];
        const baseUrl = historyElements.chatPageUrl || '/AI/Chat';

        for (const item of items) {
            if (!item?.id || list.querySelector(`[data-conversation-id="${item.id}"]`)) {
                continue;
            }

            const wrapper = document.createElement('div');
            wrapper.className = 'ai-chat-history-item-wrapper';
            wrapper.setAttribute('data-ai-history-item-wrapper', '');

            const link = document.createElement('a');
            link.className = 'ai-chat-history-item';
            link.href = `${baseUrl}?conversationId=${encodeURIComponent(item.id)}`;
            link.setAttribute('data-ai-history-item', '');
            link.setAttribute('data-conversation-id', item.id);
            link.setAttribute('data-updated-at', item.updatedAtUtc || '');

            const title = document.createElement('span');
            title.className = 'ai-chat-history-item-title';
            title.textContent = item.title || 'Cuộc hội thoại';

            const time = document.createElement('span');
            time.className = 'ai-chat-history-item-time';
            time.setAttribute('data-timestamp', item.updatedAtUtc || '');
            time.textContent = item.updatedAtUtc ? new Date(item.updatedAtUtc).toLocaleString() : '';

            link.appendChild(title);
            link.appendChild(time);
            wrapper.appendChild(link);
            list.appendChild(wrapper);
        }

        if (items.length > 0) {
            const oldest = items[items.length - 1];
            list.setAttribute('data-ai-history-cursor-time', oldest.updatedAtUtc || '');
            list.setAttribute('data-ai-history-cursor-id', oldest.id || '');
        }

        if (!payload.hasMore) {
            button.remove();
        }

        hydrateTimestamps(historyElements);
    }

    async function loadOlderMessages(button, chatWindow, chatShell) {
        const conversationId = button.getAttribute('data-conversation-id') || '';
        const beforeCreatedAtUtc = button.getAttribute('data-ai-message-cursor-time') || '';
        const beforeId = button.getAttribute('data-ai-message-cursor-id') || '';
        if (!conversationId || !beforeCreatedAtUtc || !beforeId) {
            return;
        }

        const url = new URL(button.getAttribute('data-ai-load-older-url'), window.location.origin);
        url.searchParams.set('conversationId', conversationId);
        url.searchParams.set('beforeCreatedAtUtc', beforeCreatedAtUtc);
        url.searchParams.set('beforeId', beforeId);

        const previousHeight = chatWindow.scrollHeight;
        const response = await fetch(url.toString(), {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });
        if (!response.ok) {
            return;
        }

        const payload = await response.json();
        const items = Array.isArray(payload.items) ? payload.items : [];
        const insertBefore = button.closest('.ai-chat-load-older-row') || button;
        const aiAvatar = chatShell?.getAttribute('data-ai-avatar') || '/images/ai/tct-ai-mark.svg';
        // Insert a chronological fragment once — per-row insertBefore(nextSibling) reverses the page.
        const fragment = document.createDocumentFragment();

        for (const item of items) {
            if (!item?.id || chatWindow.querySelector(`[data-message-id="${item.id}"]`)) {
                continue;
            }

            const role = (item.role || 'assistant').toLowerCase();
            const row = document.createElement('div');
            row.className = `ai-chat-message ai-chat-message-${role}`;
            row.setAttribute('data-role', role);
            row.setAttribute('data-message-id', item.id);
            row.setAttribute('data-created-at', item.createdAtUtc || '');

            if (role === 'assistant' || role === 'system') {
                const avatar = document.createElement('div');
                avatar.className = 'ai-chat-avatar ai-chat-avatar-assistant';
                avatar.setAttribute('aria-hidden', 'true');
                const img = document.createElement('img');
                img.src = aiAvatar;
                img.alt = 'TCT AI';
                img.loading = 'lazy';
                avatar.appendChild(img);
                row.appendChild(avatar);
            }

            const bubble = document.createElement('div');
            bubble.className = `ai-chat-bubble ai-chat-bubble-${role}`;
            if (role === 'assistant') {
                const markdown = document.createElement('div');
                markdown.className = 'js-assistant-markdown';
                markdown.textContent = item.content || '';
                bubble.appendChild(markdown);
            } else {
                bubble.textContent = item.content || '';
            }

            row.appendChild(bubble);
            attachGrounding(bubble, item.grounding);
            fragment.appendChild(row);
        }

        // Keep the load-older control/anchor at the top of history. Insert the
        // chronological page after the control (before the oldest already-visible message).
        // Inserting before the control pushed it below page1 so page2 landed between pages.
        chatWindow.insertBefore(fragment, insertBefore.nextSibling);

        if (items.length > 0) {
            const oldest = items[0];
            button.setAttribute('data-ai-message-cursor-time', oldest.createdAtUtc || '');
            button.setAttribute('data-ai-message-cursor-id', oldest.id || '');
        }

        if (!payload.hasMore) {
            const row = button.closest('.ai-chat-load-older-row');
            if (row) {
                row.remove();
            } else {
                button.remove();
            }
        }

        chatWindow.scrollTop = chatWindow.scrollHeight - previousHeight;
        hydrateAssistantMessages(chatWindow);
    }

    window.tctAiChat = {
        init: init,
        // Exported for focused DOM order regression (C4); production still uses bindLoadOlderControls.
        loadOlderMessages: loadOlderMessages
    };
})();
