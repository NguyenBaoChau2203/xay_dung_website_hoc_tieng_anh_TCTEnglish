/**
 * PCM16 mono 16kHz WAV recorder.
 * One completion promise for manual and auto stop. Late permission after dispose does not open a graph.
 */
(function (global) {
    'use strict';

    const TARGET_SAMPLE_RATE = 16000;
    const MAX_SECONDS = 15;
    const MAX_SAMPLES = TARGET_SAMPLE_RATE * MAX_SECONDS;

    function floatTo16BitPCM(float32Array) {
        const buffer = new ArrayBuffer(float32Array.length * 2);
        const view = new DataView(buffer);
        for (let i = 0; i < float32Array.length; i++) {
            let s = Math.max(-1, Math.min(1, float32Array[i]));
            view.setInt16(i * 2, s < 0 ? s * 0x8000 : s * 0x7fff, true);
        }
        return view;
    }

    function writeString(view, offset, string) {
        for (let i = 0; i < string.length; i++) {
            view.setUint8(offset + i, string.charCodeAt(i));
        }
    }

    function encodeWav(samples, sampleRate) {
        const dataLength = samples.length * 2;
        const buffer = new ArrayBuffer(44 + dataLength);
        const view = new DataView(buffer);
        writeString(view, 0, 'RIFF');
        view.setUint32(4, 36 + dataLength, true);
        writeString(view, 8, 'WAVE');
        writeString(view, 12, 'fmt ');
        view.setUint32(16, 16, true);
        view.setUint16(20, 1, true);
        view.setUint16(22, 1, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * 2, true);
        view.setUint16(32, 2, true);
        view.setUint16(34, 16, true);
        writeString(view, 36, 'data');
        view.setUint32(40, dataLength, true);
        const pcm = floatTo16BitPCM(samples);
        for (let i = 0; i < pcm.byteLength; i++) {
            view.setUint8(44 + i, pcm.getUint8(i));
        }
        return new Blob([view], { type: 'audio/wav' });
    }

    function downsample(buffer, fromRate, toRate) {
        if (fromRate === toRate) return buffer;
        const ratio = fromRate / toRate;
        const newLength = Math.min(MAX_SAMPLES, Math.round(buffer.length / ratio));
        const result = new Float32Array(newLength);
        for (let i = 0; i < newLength; i++) {
            const start = Math.floor(i * ratio);
            result[i] = buffer[start] || 0;
        }
        return result;
    }

    function createRecorder(options) {
        const onComplete = options && options.onComplete;
        const onError = options && options.onError;
        let mediaStream = null;
        let audioContext = null;
        let processor = null;
        let source = null;
        let chunks = [];
        let startedAt = 0;
        let stopTimer = null;
        let recording = false;
        let generation = 0;
        let settled = false;
        let resultBlob = null;
        let completionPromise = null;
        let resolveCompletion = null;
        let rejectCompletion = null;

        function armPromise() {
            if (completionPromise) return completionPromise;
            completionPromise = new Promise(function (resolve, reject) {
                resolveCompletion = resolve;
                rejectCompletion = reject;
            });
            return completionPromise;
        }

        function stopTracks(stream) {
            if (!stream) return;
            stream.getTracks().forEach(function (track) { track.stop(); });
        }

        function cleanupGraph() {
            if (stopTimer) {
                global.clearTimeout(stopTimer);
                stopTimer = null;
            }
            try { processor && processor.disconnect(); } catch (_) { /* ignore */ }
            try { source && source.disconnect(); } catch (_) { /* ignore */ }
            stopTracks(mediaStream);
            mediaStream = null;
            if (audioContext) {
                audioContext.close().catch(function () { /* ignore */ });
                audioContext = null;
            }
            processor = null;
            source = null;
        }

        function settle(blob, error) {
            if (settled) return completionPromise;
            settled = true;
            recording = false;
            resultBlob = blob || null;
            cleanupGraph();
            chunks = [];
            if (error) {
                if (rejectCompletion) {
                    rejectCompletion(error);
                    if (completionPromise && typeof completionPromise.catch === 'function') {
                        completionPromise.catch(function () { /* caller may only catch start() */ });
                    }
                }
                if (onError) onError(error);
            } else {
                if (resolveCompletion) resolveCompletion(resultBlob);
                if (resultBlob && onComplete) onComplete(resultBlob);
            }
            return completionPromise;
        }

        function buildBlob() {
            if (!chunks.length) return null;
            const sampleRate = audioContext ? audioContext.sampleRate : TARGET_SAMPLE_RATE;
            let total = 0;
            chunks.forEach(function (c) { total += c.length; });
            const merged = new Float32Array(total);
            let offset = 0;
            chunks.forEach(function (c) {
                merged.set(c, offset);
                offset += c.length;
            });
            const downsampled = downsample(merged, sampleRate, TARGET_SAMPLE_RATE);
            const limited = downsampled.length > MAX_SAMPLES ? downsampled.subarray(0, MAX_SAMPLES) : downsampled;
            const blob = encodeWav(limited, TARGET_SAMPLE_RATE);
            if (blob.size > 2 * 1024 * 1024) {
                throw new Error('payload_too_large');
            }
            return blob;
        }

        async function start() {
            if (recording) return armPromise();
            const gen = ++generation;
            settled = false;
            resultBlob = null;
            completionPromise = null;
            resolveCompletion = null;
            rejectCompletion = null;
            armPromise();

            if (!global.navigator?.mediaDevices?.getUserMedia) {
                const error = new Error('mic_unavailable');
                settle(null, error);
                throw error;
            }

            let stream;
            try {
                stream = await global.navigator.mediaDevices.getUserMedia({
                    audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true }
                });
            } catch (error) {
                settle(null, error);
                throw error;
            }

            if (gen !== generation || settled) {
                stopTracks(stream);
                if (!settled) settle(null, null);
                return completionPromise;
            }

            try {
                audioContext = new (global.AudioContext || global.webkitAudioContext)();
                source = audioContext.createMediaStreamSource(stream);
                processor = audioContext.createScriptProcessor(4096, 1, 1);
            } catch (error) {
                stopTracks(stream);
                cleanupGraph();
                settle(null, error);
                throw error;
            }

            mediaStream = stream;
            chunks = [];
            startedAt = Date.now();
            recording = true;

            processor.onaudioprocess = function (event) {
                if (!recording || gen !== generation) return;
                const input = event.inputBuffer.getChannelData(0);
                chunks.push(new Float32Array(input));
                const elapsed = (Date.now() - startedAt) / 1000;
                if (elapsed >= MAX_SECONDS) {
                    stop();
                }
            };

            source.connect(processor);
            processor.connect(audioContext.destination);
            stopTimer = global.setTimeout(function () {
                if (recording && gen === generation) stop();
            }, MAX_SECONDS * 1000);
        }

        function stop() {
            if (settled) return completionPromise || Promise.resolve(resultBlob);
            if (!recording && !mediaStream && chunks.length === 0) {
                return settle(null, null);
            }
            recording = false;
            try {
                return settle(buildBlob(), null);
            } catch (error) {
                return settle(null, error);
            }
        }

        function dispose() {
            generation += 1;
            if (!settled) {
                settle(null, null);
            } else {
                cleanupGraph();
            }
        }

        return {
            start: start,
            stop: stop,
            dispose: dispose,
            isRecording: function () { return recording; }
        };
    }

    global.TctSpeechRecorder = {
        isSupported: function () {
            return !!(global.navigator?.mediaDevices?.getUserMedia
                && (global.AudioContext || global.webkitAudioContext));
        },
        create: createRecorder,
        maxSeconds: MAX_SECONDS,
        maxSamples: MAX_SAMPLES
    };
})(typeof window !== 'undefined' ? window : globalThis);
