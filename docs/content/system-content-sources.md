# System content: sources, licences and how to add more

Plan: [2026-11-19-system-content-fill](../superpowers/plans/2026-11-19-system-content-fill.md). Status of the run is in section 12 of that plan.
Everything here is **local authoring**: tools never run at startup, never reset the database and never touch a hosting target.

## What was added

| Area | Content | Where it lives |
| --- | --- | --- |
| Vocabulary folder A "Từ vựng theo chủ đề – Cơ bản (A1–A2)" | 24 sets × 25 cards | `data/content/vocabulary/a-co-ban/` |
| Vocabulary folder B "Từ vựng theo chủ đề – Trung cấp (B1–B2)" | 16 sets × 25 cards | `data/content/vocabulary/b-trung-cap/` |
| Vocabulary folder C "Giao tiếp hằng ngày" | 8 sets × 20 phrases | `data/content/vocabulary/c-giao-tiep-hang-ngay/` |
| Legacy sets (D7) | 92 cards of the 8 old JSON sets now have a real image | `TCTEnglish/wwwroot/images/<topic>/`, `data/content/legacy-images/` |
| Listening | 22 NASA lessons (A2 1, B1 10, B2 6, C1 3, C2 2), with timed transcript, Vietnamese for every line, quiz, vocabulary | `data/listening/system-a1-c2-2026-11.json` |
| Distribution | one shared-data package per set and per lesson | `data/shared/packages/team.vocab.*`, `team.listening.*` |

## Vocabulary text

Every definition, example, translation and collocation was written for this project; nothing was copied from a dictionary. IPA comes from
Wiktionary (rendered HTML first, wikitext second) with the Free Dictionary API as a last resort. A transcription that no source had was
written by hand and flagged `ipaVerified:false` in the manifest (limit 3 % of cards, checked by `validate_vocab.py` and `ContentManifestTests`).
Word choice follows the topic plan and open frequency lists (NGSL/NAWL/CEFR-J) as guidance only.

## Images and licences

The app only shows an image credit for Pixabay, and no Pixabay key was configured, so **only CC0, public-domain and original images are used**
(CC BY / CC BY-SA are excluded because the credit would not be displayed).

| Provider in the manifest | Licence | Cards |
| --- | --- | --- |
| `generated` | "Original work (generated)": drawn by `tools/content/vocab_generate.py` in this repository | 509 |
| `flickr`, `rawpixel`, `wikimedia`, `stocksnap`, `wordpress`, `clevelandmuseum` | CC0, Public Domain Mark or Public domain, found through Openverse and Wikimedia Commons | 651 |

Each card keeps provenance: file, provider, page URL, author when known, licence, query, SHA-256, size and review date (`image` block in the manifest).
Images are never hot-linked: they are downloaded, converted to WebP 480×360 and imported as `/images/cards/sys-<set>-<name>.webp`.
Every image was looked at on a contact sheet before it was accepted. Photos of people, bodies and clothing were avoided on purpose;
those sets use original artwork instead. Terms that cannot be shown by an honest picture were replaced by terms that can (recorded in plan section 12).

## Listening

All 22 lessons come from NASA (official channels). NASA's media policy states that NASA content "generally are not subject to copyright in the United States"
and may be used for educational or informational purposes (https://www.nasa.gov/nasa-brand-center/images-and-media/). Each lesson stores `rights.policyUrl`,
`rights.itemUrl` and that quotation. Third-party material that NASA marks as copyrighted was not selected.

Transcripts come from YouTube captions retrieved with the application's own captions-only service (`StageCaptions`, no audio and no AI), cleaned of sound tags,
merged into lines of at most 25 words, and corrected by hand where captions had typos (recorded per lesson in `transcript.corrections`). The Vietnamese
translation, quiz and vocabulary were written for this project. The level is judged from words per minute while speaking plus vocabulary (`levelEvidence`).

Not done: A1 has no lesson, and A2 has only one. Public VOA "Let's Learn English" videos have no retrievable English captions, so they could not be timed honestly.

## How to add content next time

```powershell
# vocabulary
py tools/content/compact_to_manifest.py <source.txt>            # compact text -> manifest
py tools/content/ipa_check.py fill <manifest>                    # IPA (serial, Wiktionary rate-limits)
py tools/content/vocab_images.py fetch|sheet|pick|requery <manifest>
py tools/content/vocab_generate.py <manifest>                    # original artwork for cards with "generate"
py tools/content/validate_vocab.py --require-images <manifest>
pwsh scripts/content-import.ps1 -Action DryRunVocab -Only A01    # then ImportVocab
pwsh scripts/shared-data.ps1 -Action Export -Kind Set -EntityId <id> -Key team.vocab.<folder>.<set> -PackageId <same>

# listening
pwsh scripts/content-import.ps1 -Action StageCaptions -Ids <id,id>   # captions only
py tools/content/listening_lines.py <videoId> --show
py tools/content/listening_build.py <lesson-source.py>
py tools/content/validate_listening.py
pwsh scripts/content-import.ps1 -Action ImportListening
```

`ImportVocab`/`ImportListening` are idempotent. They never delete a card or lesson; a public lesson with the same YouTube id and different content is reported as a conflict and preserved.
