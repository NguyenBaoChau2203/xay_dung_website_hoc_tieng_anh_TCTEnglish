(function () {
    function init(options) {
        const elForm = document.getElementById('chatForm');
        const elInput = document.getElementById('messageInput');
        const elChatWindow = document.getElementById('chatWindow');
        const elTyping = document.getElementById('typing');
        const elSendBtn = document.getElementById('sendBtn');
        const elStopBtn = document.getElementById('stopBtn');
        const elConversationId = document.getElementById('conversationIdInput');
        const elStatus = document.getElementById('chatStatus');
        const elMetricRequestCount = document.getElementById('aiMetricRequestCount');
        const elMetricErrorRate = document.getElementById('aiMetricErrorRate');
        const elMetricLatency = document.getElementById('aiMetricLatency');
        const elMetricTokenToday = document.getElementById('aiMetricTokenToday');

        if (!elForm || !elInput || !elChatWindow || !elTyping || !elSendBtn || !elStopBtn || !elConversationId) {
            return;
        }

        const runtime = {
            sendUrl: options?.sendUrl || '',
            observabilityUrl: options?.observabilityUrl || '',
            hubUrl: options?.hubUrl || '/classChatHub',
            hubConnection: null,
            hubAvailable: false,
            activeStreamId: null,
            isStreaming: false,
            streamChunkCount: 0,
            streamBuffer: '',
            streamMessageElement: null,
            streamCompletionResolver: null
        };

        hydrateAssistantMessages(elChatWindow);
        scrollToBottom(elChatWindow);
        void initializeHub(runtime, elStatus, elForm, elSendBtn, elStopBtn, elTyping);
        void refreshObservability(runtime.observabilityUrl, {
            requestCount: elMetricRequestCount,
            errorRate: elMetricErrorRate,
            latency: elMetricLatency,
            tokenToday: elMetricTokenToday
        });

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

        elForm.addEventListener('submit', async function (event) {
            event.preventDefault();

            const text = (elInput.value || '').trim();
            if (!text) {
                return;
            }

            appendMessage(elChatWindow, 'user', text);
            elInput.value = '';
            hideStatus(elStatus);

            setUiState('sending', elForm, elSendBtn, elStopBtn, elTyping, false);

            try {
                const streamed = await trySendWithStreaming(runtime, text, elConversationId.value, elChatWindow, elForm, elSendBtn, elStopBtn, elTyping, elStatus);

                if (!streamed) {
                    const payload = await sendWithRetry(runtime.sendUrl, {
                        conversationId: elConversationId.value,
                        message: text
                    });
                    appendMessage(elChatWindow, 'assistant', payload.text || '');
                }

                setUiState('done', elForm, elSendBtn, elStopBtn, elTyping, false);
            } catch (error) {
                setUiState('error', elForm, elSendBtn, elStopBtn, elTyping, false);
                showStatus(elStatus, error?.message || 'Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.');
                appendMessage(elChatWindow, 'system', 'Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại.');
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

            runtime.streamChunkCount += 1;
            runtime.streamBuffer += payload?.chunk || '';
            renderAssistantContent(runtime.streamMessageElement, runtime.streamBuffer);
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
        if (!runtime.hubAvailable || !runtime.hubConnection || runtime.hubConnection.state !== signalR.HubConnectionState.Connected) {
            return false;
        }

        runtime.isStreaming = true;
        runtime.streamChunkCount = 0;
        runtime.streamBuffer = '';
        runtime.streamMessageElement = appendStreamingAssistantMessage(elChatWindow);
        setUiState('sending', elForm, elSendBtn, elStopBtn, elTyping, true);

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
            return false;
        }

        throw new Error(completion.message || 'Realtime tháº¥t báº¡i.');
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

    function appendStreamingAssistantMessage(elChatWindow) {
        const result = appendMessage(elChatWindow, 'assistant', '');
        return result?.contentElement || null;
    }

    function removeStreamingPlaceholder(element) {
        const container = element?.closest('[data-role]');
        if (container && container.parentElement) {
            container.parentElement.removeChild(container);
        }
    }

    async function sendWithRetry(sendUrl, payload) {
        const maxAttempts = 2;

        for (let attempt = 1; attempt <= maxAttempts; attempt++) {
            const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const response = await fetch(sendUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken || ''
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

    function appendMessage(elChatWindow, role, text) {
        const container = document.createElement('div');
        container.className = role === 'user' ? 'd-flex justify-content-end mb-2' : 'd-flex justify-content-start mb-2';
        container.setAttribute('data-role', role);

        const bubble = document.createElement('div');
        bubble.className = role === 'user' ? 'rounded px-3 py-2 bg-primary text-white' : 'rounded px-3 py-2 bg-light';
        bubble.style.maxWidth = '85%';
        bubble.style.whiteSpace = 'pre-wrap';

        if (role === 'assistant') {
            renderAssistantContent(bubble, text);
        } else {
            bubble.textContent = text;
        }

        container.appendChild(bubble);
        elChatWindow.appendChild(container);
        scrollToBottom(elChatWindow);

        return {
            containerElement: container,
            contentElement: bubble
        };
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
        elSendBtn.disabled = state === 'sending';
        elTyping.classList.toggle('d-none', state !== 'sending');
        elStopBtn.classList.toggle('d-none', !isStreaming);
        elStopBtn.disabled = !isStreaming;
    }

    function scrollToBottom(elChatWindow) {
        elChatWindow.scrollTop = elChatWindow.scrollHeight;
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

