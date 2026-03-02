// wwwroot/js/speaking.js

(function () {
    // DOM Elements
    const playerWrapper = document.getElementById('youtube-player');
    const playPauseBtn = document.getElementById('btn-play-pause');
    const playPauseIcon = document.getElementById('icon-play-pause');
    const replayBtn = document.getElementById('btn-replay');
    const sentenceItems = document.querySelectorAll('.sentence-item');

    // State
    let player;
    let currentSentenceIndex = -1;
    let isLooping = false;
    let loopStart = 0;
    let loopEnd = 0;
    let checkInterval;

    // Load YouTube API
    const tag = document.createElement('script');
    tag.src = "https://www.youtube.com/iframe_api";
    const firstScriptTag = document.getElementsByTagName('script')[0];
    firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);

    window.onYouTubeIframeAPIReady = function () {
        if (!typeof youtubeVideoId !== 'undefined' && youtubeVideoId) {
            player = new YT.Player('youtube-player', {
                videoId: youtubeVideoId,
                playerVars: {
                    'controls': 0, // Hide YouTube controls for custom UI
                    'rel': 0,
                    'modestbranding': 1
                },
                events: {
                    'onReady': onPlayerReady,
                    'onStateChange': onPlayerStateChange
                }
            });
        }
    };

    function onPlayerReady(event) {
        // Init controls
        playPauseBtn.addEventListener('click', togglePlayPause);
        replayBtn.addEventListener('click', toggleReplay);

        // Bind clicks on sentence items
        sentenceItems.forEach((item, index) => {
            const btnListen = item.querySelector('.btn-listen');
            const btnRecord = item.querySelector('.btn-record');

            // Allow clicking the entire item to seek
            item.addEventListener('click', (e) => {
                // Ignore if clicked on the action buttons
                if (e.target.closest('.btn-listen') || e.target.closest('.btn-record')) {
                    return;
                }
                const start = parseFloat(item.getAttribute('data-start'));
                seekTo(start);
            });

            // Listen button also seeks
            if (btnListen) {
                btnListen.addEventListener('click', (e) => {
                    const start = parseFloat(item.getAttribute('data-start'));
                    seekTo(start);
                });
            }

            // Record Button
            if (btnRecord) {
                btnRecord.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const textToMatch = item.querySelector('.english-text').innerText;
                    startRecording(item, textToMatch);
                });
            }
        });

        // Start tracking time
        checkInterval = setInterval(trackTime, 100);
    }

    function onPlayerStateChange(event) {
        if (event.data === YT.PlayerState.PLAYING) {
            playPauseIcon.classList.remove('fa-play');
            playPauseIcon.classList.add('fa-pause');
        } else {
            playPauseIcon.classList.remove('fa-pause');
            playPauseIcon.classList.add('fa-play');
        }
    }

    function togglePlayPause() {
        if (!player) return;
        const state = player.getPlayerState();
        if (state === YT.PlayerState.PLAYING) {
            player.pauseVideo();
        } else {
            player.playVideo();
        }
    }

    function seekTo(time) {
        if (!player) return;
        player.seekTo(time, true);
        player.playVideo();
    }

    function toggleReplay() {
        isLooping = !isLooping;
        if (isLooping) {
            replayBtn.classList.remove('btn-outline-secondary');
            replayBtn.classList.add('btn-primary'); // Highlight state

            // Set loop boundaries based on the currently active sentence
            if (currentSentenceIndex >= 0 && currentSentenceIndex < sentenceItems.length) {
                const item = sentenceItems[currentSentenceIndex];
                loopStart = parseFloat(item.getAttribute('data-start'));
                loopEnd = parseFloat(item.getAttribute('data-end'));
            }
        } else {
            replayBtn.classList.add('btn-outline-secondary');
            replayBtn.classList.remove('btn-primary');
        }
    }

    // --- Time Tracking & Highlighting ---
    function trackTime() {
        if (!player || player.getPlayerState() !== YT.PlayerState.PLAYING) return;

        const currentTime = player.getCurrentTime();

        // Handle Looping
        if (isLooping && loopEnd > 0) {
            if (currentTime >= loopEnd) {
                player.seekTo(loopStart, true);
            }
        }

        // Highlight matching sentence
        let foundIndex = -1;
        for (let i = 0; i < sentenceItems.length; i++) {
            const start = parseFloat(sentenceItems[i].getAttribute('data-start'));
            const end = parseFloat(sentenceItems[i].getAttribute('data-end'));

            if (currentTime >= start && currentTime < end) {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex !== -1 && foundIndex !== currentSentenceIndex) {
            // Remove active from previous
            if (currentSentenceIndex !== -1) {
                sentenceItems[currentSentenceIndex].classList.remove('active');
            }
            // Set active to new
            sentenceItems[foundIndex].classList.add('active');

            // Auto-scroll logic if needed
            sentenceItems[foundIndex].scrollIntoView({ behavior: 'smooth', block: 'center' });

            currentSentenceIndex = foundIndex;

            // If we're looping, auto-update the loop points if we naturally flowed into the next sentence before looping was enabled
            if (isLooping) {
                const newActiveItem = sentenceItems[currentSentenceIndex];
                loopStart = parseFloat(newActiveItem.getAttribute('data-start'));
                loopEnd = parseFloat(newActiveItem.getAttribute('data-end'));
            }
        }
    }

    // --- Web Speech API (Shadowing) ---
    // Make sure we resolve prefix issues
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

    function startRecording(itemElement, expectedText) {
        if (!SpeechRecognition) {
            alert('Your browser does not support Speech Recognition. Please use Chrome.');
            return;
        }

        // Auto-pause video when speaking
        if (player && player.getPlayerState() === YT.PlayerState.PLAYING) {
            player.pauseVideo();
        }

        const btnRecord = itemElement.querySelector('.btn-record');
        const icon = btnRecord.querySelector('i');
        const textElement = itemElement.querySelector('.english-text');

        // Setup visual recording state
        btnRecord.classList.remove('btn-outline-danger');
        btnRecord.classList.add('btn-danger'); // Solid red
        icon.classList.add('fa-beat-fade'); // Add animation

        const recognition = new SpeechRecognition();
        recognition.lang = 'en-US';
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;

        recognition.onresult = function (event) {
            const transcript = event.results[0][0].transcript;

            // Compare text
            const similarity = calculateSimilarity(expectedText.toLowerCase(), transcript.toLowerCase());

            // Reset existing feedback classes
            textElement.classList.remove('text-success', 'text-danger');

            // Threshold for success
            if (similarity > 0.8) {
                textElement.classList.add('text-success'); // Green
            } else {
                textElement.classList.add('text-danger'); // Red
            }

            // Optionally, show what they said to help debug
            console.log("Expected: ", expectedText);
            console.log("Heard: ", transcript);
            console.log("Accuracy: ", (similarity * 100).toFixed(2) + "%");
        };

        recognition.onerror = function (event) {
            console.error('Speech recognition error: ', event.error);
            alert('Error detecting speech: ' + event.error);
        };

        recognition.onend = function () {
            // Revert button visual state
            btnRecord.classList.add('btn-outline-danger');
            btnRecord.classList.remove('btn-danger');
            icon.classList.remove('fa-beat-fade');
        };

        recognition.start();
    }

    // --- Similarity Calculator Helper ---
    function calculateSimilarity(s1, s2) {
        // Clean punctuation for better matching
        s1 = s1.replace(/[^\w\s]|_/g, "").replace(/\s+/g, " ").trim();
        s2 = s2.replace(/[^\w\s]|_/g, "").replace(/\s+/g, " ").trim();

        if (s1 === s2) return 1.0;

        // Levenshtein distance
        const longer = s1.length > s2.length ? s1 : s2;
        const shorter = s1.length > s2.length ? s2 : s1;
        const maxLen = longer.length;

        if (maxLen === 0) return 1.0;

        return (maxLen - editDistance(longer, shorter)) / maxLen;
    }

    function editDistance(s1, s2) {
        let costs = new Array();
        for (let i = 0; i <= s1.length; i++) {
            let lastValue = i;
            for (let j = 0; j <= s2.length; j++) {
                if (i == 0)
                    costs[j] = j;
                else {
                    if (j > 0) {
                        let newValue = costs[j - 1];
                        if (s1.charAt(i - 1) != s2.charAt(j - 1))
                            newValue = Math.min(Math.min(newValue, lastValue),
                                costs[j]) + 1;
                        costs[j - 1] = lastValue;
                        lastValue = newValue;
                    }
                }
            }
            if (i > 0)
                costs[s2.length] = lastValue;
        }
        return costs[s2.length];
    }
})();
