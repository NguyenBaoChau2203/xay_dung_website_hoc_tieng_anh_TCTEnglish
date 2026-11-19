(function (root) {
    'use strict';
    class ChatModeController {
        #mode = 'text';
        #transition = null;
        #options;
        constructor(options) { this.#options = options; options.showMode('text'); }
        async request(next) {
            if (!['text', 'voice'].includes(next)) throw new TypeError('Unsupported chat mode');
            if (this.#transition) return next === 'text' ? this.#transition : false;
            if (next === this.#mode) return true;
            if (next === 'voice') { this.#mode = next; this.#options.showMode(next); return true; }
            this.#options.onBusy?.(true);
            this.#transition = Promise.resolve().then(async () => {
                try {
                    if (await this.#options.closeVoice() !== true) return false;
                    this.#mode = 'text'; this.#options.showMode('text'); return true;
                } catch { return false; }
                finally { this.#transition = null; this.#options.onBusy?.(false); }
            });
            return this.#transition;
        }
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = { ChatModeController };
    else root.TctChatMode = { ChatModeController };
})(globalThis);
