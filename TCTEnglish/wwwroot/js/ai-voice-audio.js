// Copyright (c) 2025 OpenAI, MIT. Adapted from revision 1215a24fb4725742c7b88094812b7ba18c46041c.
// See third_party/openai-realtime-server-controlled/NOTICE.md.
(function(root) {
    "use strict";
    class AudioOnlyWebRtc {
        #abortController = new AbortController();
        #onError;
        #remoteAudio;
        #getUserMedia;
        #createPeerConnection;
        #started = false;
        #peerConnection = null;
        #localStream = null;
        #disconnectTimer = null;
        #muted = false;
        #listeningEnabled = true;
        constructor(options){
            this.#onError = options.onError;
            this.#remoteAudio = options.remoteAudio;
            this.#getUserMedia = options.getUserMedia ?? ((constraints)=>navigator.mediaDevices.getUserMedia(constraints));
            this.#createPeerConnection = options.createPeerConnection ?? (()=>new RTCPeerConnection());
        }
        get muted() {
            return this.#muted;
        }
        async connect(exchangeSdp) {
            if (this.#started) {
                throw new Error('Create a new audio connection for each voice session.');
            }
            const { signal } = this.#abortController;
            signal.throwIfAborted();
            this.#started = true;
            try {
                const localStream = await this.#getUserMedia({
                    audio: {
                        autoGainControl: true,
                        echoCancellation: true,
                        noiseSuppression: true
                    },
                    video: false
                });
                if (signal.aborted) {
                    for (const track of localStream.getTracks()){
                        track.stop();
                    }
                    signal.throwIfAborted();
                }
                this.#localStream = localStream;
                const audioTracks = localStream.getAudioTracks();
                if (audioTracks.length !== 1) {
                    throw new Error('Expected exactly one microphone audio track.');
                }
                const audioTrack = audioTracks[0];
                this.#applyInputState();
                if (audioTrack.readyState === 'ended') {
                    throw new Error('The microphone is no longer available.');
                }
                const fail = ()=>{
                    if (!signal.aborted) {
                        this.close();
                        this.#onError();
                    }
                };
                audioTrack.onended = fail;
                const peerConnection = this.#createPeerConnection();
                this.#peerConnection = peerConnection;
                peerConnection.onconnectionstatechange = ()=>{
                    if (signal.aborted) {
                        return;
                    }
                    if (peerConnection.connectionState === 'disconnected') {
                        this.#disconnectTimer ??= setTimeout(()=>{
                            this.#disconnectTimer = null;
                            if (peerConnection.connectionState === 'disconnected') {
                                fail();
                            }
                        }, 10_000);
                        return;
                    }
                    this.#clearDisconnectTimer();
                    if (peerConnection.connectionState === 'failed') {
                        fail();
                    }
                };
                peerConnection.ontrack = (event)=>{
                    const [remoteStream] = event.streams;
                    if (signal.aborted || !remoteStream) {
                        return;
                    }
                    this.#remoteAudio.srcObject = remoteStream;
                    void this.#remoteAudio.play().catch(fail);
                };
                peerConnection.addTrack(audioTrack, localStream);
                const offer = await peerConnection.createOffer();
                signal.throwIfAborted();
                await peerConnection.setLocalDescription(offer);
                signal.throwIfAborted();
                const offerSdp = peerConnection.localDescription?.sdp ?? offer.sdp;
                if (!offerSdp) {
                    throw new Error('The browser did not create an SDP offer.');
                }
                const answerSdp = await exchangeSdp(offerSdp, signal);
                signal.throwIfAborted();
                await peerConnection.setRemoteDescription({
                    type: 'answer',
                    sdp: answerSdp
                });
                signal.throwIfAborted();
            } catch (error) {
                if (!signal.aborted) {
                    this.close();
                }
                throw error;
            }
        }
        setMuted(muted) {
            this.#muted = Boolean(muted);
            this.#applyInputState();
        }
        setListeningEnabled(enabled) {
            this.#listeningEnabled = Boolean(enabled);
            this.#applyInputState();
        }
        #applyInputState() {
            for (const track of this.#localStream?.getAudioTracks() ?? []){
                // A turn gate sends silence without releasing microphone ownership.
                // Reopening a turn must never undo the learner's explicit mute.
                track.enabled = this.#listeningEnabled && !this.#muted;
            }
        }
        close() {
            if (this.#abortController.signal.aborted) {
                return;
            }
            this.#abortController.abort(new Error('The audio connection was closed.'));
            this.#clearDisconnectTimer();
            const peerConnection = this.#peerConnection;
            this.#peerConnection = null;
            if (peerConnection) {
                peerConnection.ontrack = null;
                peerConnection.onconnectionstatechange = null;
                peerConnection.close();
            }
            for (const track of this.#localStream?.getTracks() ?? []){
                track.onended = null;
                track.stop();
            }
            this.#localStream = null;
            this.#remoteAudio.srcObject = null;
        }
        #clearDisconnectTimer() {
            if (this.#disconnectTimer !== null) {
                clearTimeout(this.#disconnectTimer);
                this.#disconnectTimer = null;
            }
        }
    }
    if (typeof module !== "undefined" && module.exports) module.exports = {
        AudioOnlyWebRtc
    };
    else root.TctVoiceAudio = {
        AudioOnlyWebRtc
    };
})(globalThis);
