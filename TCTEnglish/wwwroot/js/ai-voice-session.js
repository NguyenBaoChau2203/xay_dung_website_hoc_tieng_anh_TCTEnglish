// Copyright (c) 2025 OpenAI, MIT. Adapted from revision 1215a24fb4725742c7b88094812b7ba18c46041c.
// See third_party/openai-realtime-server-controlled/NOTICE.md.
(function(root) {
    "use strict";
    // A 202 is an accepted close while another cleanup owns the lease. Keep
    // checking the same session; never create a replacement before confirmation.
    async function confirmRemoteSessionClosed({ close, onPending, timeoutMs = 30000,
        retryDelayMs = 1000, now = Date.now, wait = ms => new Promise(resolve => setTimeout(resolve, ms)) }) {
        const deadline = now() + timeoutMs;
        while (now() < deadline) {
            const result = await close(Math.min(15000, deadline - now()));
            if (result?.closed === true) return;
            if (result?.closed !== false || result.cleanupPending !== true) throw new Error('invalid_close_confirmation');
            onPending?.();
            const remaining = deadline - now();
            if (remaining > 0) await wait(Math.min(retryDelayMs, remaining));
        }
        throw Object.assign(new Error('cleanup_pending'), { code: 'cleanup_pending' });
    }
    function isAgentState(value) {
        return [
            'idle',
            'listening',
            'thinking',
            'speaking'
        ].includes(String(value));
    }
    class VoiceSessionCoordinator {
        #activeAttempt = null;
        #options;
        constructor(options){
            this.#options = options;
            this.#publishControls();
        }
        async start() {
            if (this.#activeAttempt) {
                return;
            }
            const attempt = {
                closed: false,
                connection: null,
                eventStream: null,
                cleanupPromise: null,
                sessionId: crypto.randomUUID(),
                token: null
            };
            this.#activeAttempt = attempt;
            this.#options.onStatus('connecting');
            this.#publishControls();
            const onError = ()=>{
                if (!attempt.closed && this.#activeAttempt === attempt) {
                    void this.#finish(attempt, 'error');
                }
            };
            try {
                const connection = this.#options.createConnection({
                    onError
                });
                attempt.connection = connection;
                connection.setListeningEnabled(false);
                await connection.connect(async (offerSdp, signal)=>{
                    const token = await this.#options.getCsrfToken(signal);
                    if (attempt.closed || this.#activeAttempt !== attempt) {
                        throw new Error('The voice-session attempt is no longer active.');
                    }
                    attempt.token = token;
                    return this.#options.exchangeOffer({
                        offerSdp,
                        sessionId: attempt.sessionId,
                        signal,
                        token
                    });
                });
                if (attempt.closed || this.#activeAttempt !== attempt) {
                    return;
                }
                attempt.eventStream = this.#options.openEvents({
                    sessionId: attempt.sessionId,
                    onMessage: (value)=>this.#handlePublicEvent(attempt, value),
                    onError
                });
                this.#publishControls();
            } catch (error) {
                if (attempt.closed || this.#activeAttempt !== attempt) {
                    return;
                }
                this.#options.onFailure?.(error);
                await this.#finish(attempt, 'error');
            }
        }
        async stop() {
            const attempt = this.#activeAttempt;
            if (!attempt) {
                this.#options.onStatus('ready');
                this.#publishControls();
                return;
            }
            await this.#finish(attempt, 'ready');
        }
        toggleMuted() {
            const connection = this.#activeAttempt?.connection;
            if (!connection) {
                return;
            }
            connection.setMuted(!connection.muted);
            this.#publishControls();
        }
        #finish(attempt, status) {
            if (attempt.cleanupPromise) {
                return attempt.cleanupPromise;
            }
            attempt.closed = true;
            if (status === 'error') {
                this.#options.onStatus('error');
            } else {
                this.#options.onStatus('closing');
            }
            attempt.eventStream?.close();
            attempt.eventStream = null;
            attempt.connection?.close();
            attempt.connection = null;
            attempt.cleanupPromise = Promise.resolve().then(async ()=>{
                try {
                    if (attempt.token) {
                        await this.#options.closeRemoteSession(attempt.sessionId, attempt.token);
                    }
                } catch  {
                    attempt.cleanupPromise = null;
                    if (this.#activeAttempt === attempt) {
                        this.#options.onStatus('error');
                        this.#publishControls();
                    }
                    return;
                }
                if (this.#activeAttempt === attempt) {
                    this.#activeAttempt = null;
                    this.#options.onStatus(status);
                    this.#publishControls();
                }
            });
            this.#publishControls();
            return attempt.cleanupPromise;
        }
        #handlePublicEvent(attempt, value) {
            if (this.#activeAttempt !== attempt || attempt.closed || typeof value !== 'object' || value === null || !('type' in value)) {
                return;
            }
            const event = value;
            this.#options.onEvent?.(event);
            if (event.type === 'app.session.snapshot' && isAgentState(event.state)) {
                this.#setAgentState(attempt, event.state);
                return;
            }
            if (event.type === 'app.session.ready') {
                this.#setAgentState(attempt, 'idle');
            } else if (event.type === 'app.agent.state' && isAgentState(event.state)) {
                this.#setAgentState(attempt, event.state);
            } else if (event.type === 'app.error') {
                void this.#finish(attempt, 'error');
            } else if (event.type === 'app.session.closed') {
                void this.stop();
            }
        }
        #setAgentState(attempt, state) {
            // Generation completion is not playback completion. Only the server's
            // listening/idle snapshot reopens input after its output buffer drains.
            attempt.connection?.setListeningEnabled(state === 'idle' || state === 'listening');
            this.#options.onStatus(state);
        }
        #publishControls() {
            const connection = this.#activeAttempt?.connection ?? null;
            const active = Boolean(this.#activeAttempt);
            this.#options.onControls({
                canMute: Boolean(connection && this.#activeAttempt?.eventStream),
                canStart: !active,
                canStop: active && !this.#activeAttempt?.cleanupPromise,
                muted: connection?.muted ?? false
            });
        }
    }
    if (typeof module !== "undefined" && module.exports) module.exports = {
        VoiceSessionCoordinator, confirmRemoteSessionClosed
    };
    else root.TctVoiceSession = {
        VoiceSessionCoordinator, confirmRemoteSessionClosed
    };
})(globalThis);
