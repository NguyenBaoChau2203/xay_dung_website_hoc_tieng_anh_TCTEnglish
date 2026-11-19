# Tico voice expressions

Project-bound asset generated on 2026-11-19 with the built-in ImageGen tool,
using the owner's attached TCT fox sheet as an identity/style reference. Existing
brand artwork is preserved. No CLI, API key or package was used for generation.

`tico-voice-expressions.png`: 1536 × 1024 RGBA, real transparent background;
one atlas with three columns and two rows of 512 × 512 cells. Consumed as a CSS
background at 300% × 200%; it is not a model/avatar driven by microphone volume.

| Cell | Expression | UI states |
| --- | --- | --- |
| Top left | Welcome/wave | Ready |
| Top middle | Listening | Idle/listening |
| Top right | Thinking | Connecting/thinking |
| Bottom left | Speaking | Speaking (playback state) |
| Bottom middle | Thumbs up | Confirmed finished |
| Bottom right | Encouraging/reconnect | Muted/closing/error |

The visible status text remains authoritative and accessible. Artwork is
decorative; speaking motion is disabled by prefers-reduced-motion.

## Generation prompt (verbatim)

Use case: stylized-concept. Create ONE production PNG sprite atlas for the TCT English voice-chat interface, using the supplied mascot sheet ONLY as an identity/style reference, not an edit target. Preserve its cute orange fox identity: large dark navy eyes/outlines, cream muzzle and inner ears, cobalt-blue hoodie with small exact white 'TCT' lettering, yellow neckerchief. Clean polished hand-drawn sticker style like the reference, consistent proportions and outline weight. Transparent background with real alpha. Layout: exactly THREE equal square columns by TWO equal square rows, six independent waist-up fox stickers, one centered entirely inside each equal cell, generous 12% transparent padding in every cell, no overlap, no framing, no captions, no panel lines, no header/logo. All cells have the SAME fox size and head height, fill 75% of the cell. Top-left ready: smiling friendly fox waving hello. Top-middle listening: attentive fox holding one paw beside its ear, small blue listening arcs. Top-right thinking: thoughtful fox paw near chin with a small yellow sparkle above. Bottom-left speaking: cheerful fox mouth open speaking, two small blue speech rays. Bottom-middle finished: joyful fox giving thumbs up with a small yellow star. Bottom-right reconnect: calm encouraging fox with slightly tilted head and a small circular blue arrow, never distressed. No microphone hardware/headset needed. No other animals, no photographed texture, no opaque backgrounds, no speech text, no extra limbs. This is a single UI sprite atlas with precise equal cells; favor legibility at 120px.
