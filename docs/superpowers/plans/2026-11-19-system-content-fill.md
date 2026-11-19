# System content fill: system vocabulary + Listening lessons — implementation plan

Save as `docs/superpowers/plans/2026-11-19-system-content-fill.md`.

- **Owner:** Huỳnh Phú Trọng.
- **Executor:** one Claude Code agent, `IMPLEMENT_LOCAL`, continuous run C0 → C10.
- **Purpose:** the system is nearly empty. This run fills it with **complete, correct, licensed** learning content:
  - **System vocabulary:** about 1,160 cards in 48 sets. Every card has a word, a Vietnamese meaning, IPA, part of speech, CEFR, an example with translation, details, and a reviewed on-topic image.
  - **Listening:** about 48 public lessons, A1–C2. Each has a real embeddable YouTube video, a timed transcript, a Vietnamese translation, a quiz and vocabulary.
- **Out of scope:** personal vocabulary stays user-made.

Everything is authored locally, imported into the local Docker database through reviewed tools, then shared through the team's **shared-data packages** (`docs/operations/shared-data-workflow.md`). Nothing is typed by hand into the admin UI at scale, and nothing is pushed.

---

## 0. Read first

### 0.1 Rules that win over this plan
1. `AGENTS.md`, `CLAUDE.md`, `docs/operations/shared-data-workflow.md`, `docs/operations/database-collaboration.md`.
2. This plan.
3. Licences and site terms of every source. When a licence is unclear, the item is **excluded**, never "probably fine".

### 0.2 Repo skills
`tct-superpowers`, `tct-engineering`, `tct-security-audit`, `tct-commit-message`, plus any shared-data or content skill the repo lists. Install nothing into the repo.

### 0.3 Honesty rules for content (apply to every phase)
- **Never invent a fact you cannot check.** A YouTube id, a channel, a licence, a caption line or an IPA string must come from a source that the agent fetched in this run. Record that source.
- **Write learning text yourself.** Definitions, example sentences, translations, quiz questions and memory tips are written by the agent. Do not copy dictionary definitions or example sentences from any dictionary, website or book. Word *lists* may be informed by open frequency lists (section 2.3), but the content is original.
- **Reject rather than fill.** When no correct image, no licensed video or no reliable caption exists, drop the item and pick another that fits the topic and level. Never put a placeholder, a wrong picture or an untimed fake transcript.
- **Record evidence.** Every image and every lesson carries provenance in its manifest. Every rejected candidate is listed with the reason in the phase log.

---

## 1. Decisions (defaults; Trọng may edit before the run)

| ID | Question | Default |
| --- | --- | --- |
| D1 | Vocabulary size and shape | **48 sets ≈ 1,160 cards** in 3 new system folders (section 3). |
| D2 | Image source order | **Pixabay → Openverse → Wikimedia Commons.**<br>- Pixabay: the app's existing `Pixabay:ApiKey`; Pixabay Content License, no attribution needed, but the author is recorded.<br>- Openverse: only `cc0`, `pdm`, `by`, `by-sa`; attribution recorded and shown.<br>- Wikimedia Commons: same licences.<br>- Never use Google Images, stock sites, e-commerce images or anything with a watermark, logo or visible brand. |
| D3 | Image format | **WebP 480×360** (crop to 4:3, subject centred), quality ≈ 78, target ≤ 45 KB, hard cap 80 KB. |
| D4 | Listening sources | **Public-domain and government sources first:**<br>- VOA Learning English: *Let's Learn English* Level 1 → A1, Level 2 → A2; *English in a Minute*, *Everyday Grammar*, *As It Is* → A2–B1; *Science in the News*, *American Stories*, *What It Takes* → B1–B2.<br>- For C1–C2: full-speed public-domain US government channels (NASA, NOAA, NIH, National Park Service, Library of Congress talks by federal staff).<br>- Any other channel only when its licence page clearly allows reuse of the transcript (CC BY). Never CC BY-NC/ND for transcripts, because Premium is a paid product. |
| D5 | Listening size | **48 lessons:** A1 10, A2 10, B1 10, B2 8, C1 6, C2 4. Minimums if sources run out: A1–B1 8 each, B2 6, C1 4, C2 3, recorded. |
| D6 | Fake seed lessons (`ListeningLessonSeedData`: invented transcripts, unverified ids) | **HIDE LOCALLY.** Set `IsPublished=false` through the admin page on the local database and record the ids. Write a known-issue asking Châu to retire that seeder (its file is under `Models/**`, which this run does not edit). |
| D7 | Legacy JSON sets (`wwwroot/data/system-vocabulary.json`, 9 sets, 92 cards; **all 61 local image paths point to missing files**) | **REPAIR IMAGES ONLY.**<br>- Add correct images at the exact paths the JSON already uses (`/images/<topic>/<word>.jpg`, converted as JPEG 480×360).<br>- For the 23 cards without an image or with a remote e-commerce URL, add new files and change only their `ImageUrl` in the JSON.<br>- Do not rename sets, do not delete cards.<br>- Misleading names ("1000 từ…" with 20 cards, "3000 từ vựng Oxford") go to `known-issues.md` for Châu. |
| D8 | Distribution | **One shared-data package per vocabulary set** (`team.vocab.<folder>.<set>`) **and per listening lesson** (`team.listening.<level>.<slug>`), created with `scripts/shared-data.ps1 -Action Export`. Owner alias **`system`** for vocabulary, if C0 proves it maps to `system@tct.local` (stop condition 9.2.3 otherwise). |

---

## 2. Facts from the static review (2026-11-18/19) — reproduce each at C0

### 2.1 Vocabulary

| # | Fact |
| --- | --- |
| V1 | `wwwroot/data/system-vocabulary.json`: 3 folders, 9 sets, **92 cards**. Set names promise far more ("1000 từ tiếng Anh thông dụng" has 20 cards). |
| V2 | **All 61 local `ImageUrl` paths (`/images/animals/cat.jpg` …) do not exist** in `wwwroot/images`. 30 cards have no image. 1 card uses a Shopee CDN URL. |
| V3 | `JsonVocabularySeeder` runs only when `Database:SeedOnStartup` is true (false in `appsettings.json`). It matches sets by name and **deletes cards that are missing from the JSON**, which would cascade progress. Never remove a term from that JSON. |
| V4 | `Card` has `Term, Definition, ImageUrl, Phonetic, PhoneticUk, Example, ExampleTranslation, Topic, PartsOfSpeech, MeaningEn, Cefr, Emoji, DetailsJson, ImageSourceJson`.<br>`CardInputDto` carries `Examples`, `Synonyms`, `ImageSource`, etc.<br>`CardDetailsJson` holds senses, examples (`Text`, `Vi`), collocations, word family, verb forms, plural, synonyms/antonyms, memory tip. |
| V5 | System owner: `PersonalSetService.SystemOwnerEmail = "system@tct.local"`. |
| V6 | Shared data supports roots `Folder`/`Set` (with `Card`, optional fields included) and `ListeningLesson` (with transcript lines, quiz and vocab). Asset rules:<br>- files referenced under `/images/` or `/uploads/`;<br>- ≤ 25 MB each, ≤ 200 per package;<br>- no SVG;<br>- imported to `/uploads/shared/<hash>`. |
| V7 | Pixabay search exists in `VocabSuggestService.Enrich` (`Pixabay:ApiKey`, `safesearch=true`). Check that the key is **present** without printing it. |

### 2.2 Listening

| # | Fact |
| --- | --- |
| L1 | `ListeningLessonSeedData` holds hand-written transcripts with **no times**, attached to YouTube ids that were never verified. The transcripts do not match the videos (D6). |
| L2 | The REACH batch (`data/listening/reach-a1-c2-catalog.json`) was **moved to Speaking** on 2026-11-13 (disposition file). `scripts/import-listening-catalog.ps1 -Action Import` is disabled. The 90 `listening-youtube-*` packages have version 2 (expected: retired/unpublished). |
| L3 | `Infrastructure/ListeningCatalog/ListeningCatalogImporter.cs` already has caption validation (monotonic times, end ≤ duration + 2 s) and `IYoutubeTranscriptService.GetEnglishCaptionsOnlyAsync`. A runner pattern exists in `tools/ListeningCatalogImport` (references the web project, EF Relational only). |
| L4 | `ListeningLesson` fields: Title, Level, Topic, YoutubeId, AudioUrl, ThumbnailUrl, Duration, Speaker1/2 Name/Country, IsPublished, OwnerUserId, TranscriptSource. Lines: Speaker, Text, VietnameseMeaning, StartTime, EndTime. Quiz: A–D, CorrectAnswer, Explanation. Vocab: Word, Definition, ExampleSentence, ImageUrl. |
| L5 | The Listening redesign (word lookup) looks words up in the lesson vocab first. Good vocab lists matter for every learner. |

### 2.3 Open word-frequency references (lists only, never content)
- NGSL / NAWL (CC BY-SA 4.0).
- CEFR-J Wordlist (free with citation).
- Your own judgement of topic and level.

Record the list used for each set in its manifest (`"wordListBasis"`). Do not use the Oxford 3000/5000 or the English Vocabulary Profile as a source (proprietary).

### 2.4 Unknowns to resolve at C0
1. The counts in the local DB:
   - system folders, sets and cards;
   - public listening lessons by level and published flag;
   - which ones come from the fake seed and which from REACH.
2. Whether the shared-data owner alias `system` maps to the existing `system@tct.local` user. Run `shared-data.ps1 -Action Status`, read the owner-mapping code and test on a throwaway export to a temp branch **without committing**.
3. How the app lists "system" sets for learners (query by owner email, by role, by folder?). New folders must show up the same way.
4. The upload path and URL that card images use (`IFileStorageService`?), so `ImageUrl` points to a file the exporter packs.
5. Whether `IPersonalSetService.CreateSetAsync` / `CreateCardsBatchAsync` can be called in-process as the system user (`isAdmin: true`) to reuse normalisation into `DetailsJson`. If not, use the same normaliser class directly.
6. Tools available on this machine:
   - Python 3 + Pillow (install into an ignored venv under `.appdata/local/content-build/venv` if missing; never into the repo);
   - `ffmpeg`/ImageMagick as a fallback;
   - Node 22.
7. Whether `IYoutubeTranscriptService` returns English captions for one known VOA video (smoke test).
8. Pixabay key present (yes/no only).

---

## 3. Vocabulary curriculum (D1)

Three new folders, owned by the system user, separate from the legacy JSON folders.

Set naming: Vietnamese title, plus a description with tags in the same style as today (`#a1 #animals …`). Every card's `Topic` = the set's topic label exactly (e.g. `Động vật nuôi & trang trại`).

### Folder A: `Từ vựng theo chủ đề – Cơ bản (A1–A2)` — 24 sets × 25 cards

| # | Set | Topic examples (guidance, not the final list) |
| --- | --- | --- |
| A01 | Động vật nuôi & trang trại | cow, goat, chicken, duck, rabbit, puppy… |
| A02 | Động vật hoang dã | tiger, zebra, giraffe, monkey, eagle… |
| A03 | Trái cây | mango, pineapple, watermelon, lychee… |
| A04 | Rau củ | carrot, cabbage, cucumber, garlic… |
| A05 | Món ăn hằng ngày | rice, noodles, bread, egg, soup… |
| A06 | Đồ uống | water, juice, milk tea, coffee, smoothie… |
| A07 | Cơ thể người | head, shoulder, knee, finger, elbow… |
| A08 | Quần áo & phụ kiện | jacket, scarf, sneakers, belt, cap… |
| A09 | Gia đình & con người | grandmother, cousin, baby, neighbour… |
| A10 | Nhà cửa & các phòng | kitchen, balcony, stairs, garage… |
| A11 | Nội thất & đồ gia dụng | sofa, wardrobe, lamp, fan, pillow… |
| A12 | Nhà bếp & dụng cụ nấu ăn | pan, pot, knife, chopsticks, kettle… |
| A13 | Trường học & đồ dùng học tập | ruler, backpack, whiteboard, notebook… |
| A14 | Nghề nghiệp | nurse, farmer, chef, pilot, mechanic… |
| A15 | Địa điểm trong thành phố | bakery, pharmacy, hospital, market… |
| A16 | Phương tiện giao thông | motorbike, ferry, taxi, helicopter… |
| A17 | Thời tiết & các mùa | rain, storm, snow, rainbow, spring… |
| A18 | Thiên nhiên & cảnh quan | river, beach, waterfall, forest… |
| A19 | Thể thao | badminton, swimming, volleyball… |
| A20 | Sở thích & giải trí | painting, fishing, camping, chess… |
| A21 | Nhạc cụ & nghệ thuật | guitar, drum, violin, piano… |
| A22 | Màu sắc & hình dạng | colours + circle, square, triangle, star… |
| A23 | Cảm xúc | happy, sad, angry, surprised, bored… |
| A24 | Sức khỏe & bệnh thường gặp | fever, cough, headache, toothache… |

### Folder B: `Từ vựng theo chủ đề – Trung cấp (B1–B2)` — 16 sets × 25 cards

B01 Công việc & văn phòng · B02 Công nghệ & internet · B03 Môi trường · B04 Du lịch & sân bay · B05 Mua sắm & tiền bạc · B06 Nấu ăn & chế biến · B07 Sức khỏe & thể chất · B08 Giáo dục & học tập · B09 Truyền thông & tin tức · B10 Thành phố & nhà ở · B11 Khoa học & vũ trụ · B12 Thể thao & sự kiện · B13 Nghệ thuật & văn hóa · B14 Pháp luật & xã hội · B15 Các mối quan hệ · B16 Kinh doanh & khởi nghiệp.

**Rule:** only words that can be shown by a clear image of the word or of the example's scene, such as `boarding pass`, `recycle`, `deadline` (a calendar with a circled date), `interview`, `microscope`. If a candidate cannot be pictured honestly, replace it.

### Folder C: `Giao tiếp hằng ngày` — 8 sets × 20 phrases

C01 Chào hỏi & giới thiệu · C02 Lịch sự: cảm ơn, xin lỗi, nhờ vả · C03 Gọi món ở nhà hàng · C04 Hỏi và chỉ đường · C05 Mua sắm & trả giá · C06 Gọi điện thoại · C07 Ở sân bay & khách sạn · C08 Nói chuyện xã giao.

The term is a phrase (`Could I have the bill, please?`). The image shows the situation.

### Card completeness contract (every card, every folder)

| Field | Rule |
| --- | --- |
| `Term` | Lemma form (phrases in C). No duplicates inside a folder. Across folders a word may repeat only with a different sense. |
| `PartsOfSpeech` | From the app's allowed list (read it). Phrases: `phrase`. |
| `Cefr` | A1/A2 in folder A, B1/B2 in folder B, A1–B1 in C. Based on the open lists in 2.3, recorded. |
| `Phonetic` (US) and `PhoneticUk` | IPA between slashes. **Verified** against the Free Dictionary API (`api.dictionaryapi.dev`, Wiktionary data) or Wiktionary directly. Store the source URL in the manifest only. If no source has it, write it carefully and flag `ipaVerified:false`; ≤ 3% of cards may have that flag. Phrases: IPA optional. |
| `Definition` | Short natural Vietnamese meaning in the sense of this set (1–8 words), written by the agent. |
| `MeaningEn` | One short English explanation in the agent's own words (≤ 15 words). |
| `Example` + `ExampleTranslation` | One original sentence that contains the term (or its inflected form) in the set's sense. A-level ≤ 12 words, B-level ≤ 20. Natural Vietnamese translation. |
| `DetailsJson` | Built from the same DTOs as the composer:<br>- ≥ 1 sense with `meaningVi` / `meaningEn` and **2–3 examples** (`Text`, `Vi`), the first equal to the card example;<br>- collocations ≥ 2 (folder B), ≥ 1 when natural (A);<br>- word family when it exists;<br>- verb forms for verbs;<br>- plural for irregular nouns;<br>- synonyms/antonyms when natural;<br>- an optional `memoryTipVi` (one line). |
| `Emoji` | Optional, only when it clearly matches. |
| `ImageUrl` + `ImageSourceJson` | Required (section 4). |

---

## 4. Images (D2, D3)

### 4.1 Pipeline (per set, scripted)
1. **Query.** For each term, build an English query from the term plus a disambiguator (`bat animal`, `mouse computer` only if that sense is in the set). For B and C, query the example's scene.
2. **Search** the providers in order, `safesearch` on, photos/illustrations only. Take up to 8 candidates.
3. **Auto-filter:**
   - min 640 px on the short side;
   - aspect between 1:2 and 2:1;
   - not grayscale-only unless the term needs it;
   - licence in the allowlist.
4. **Download** to `.appdata/local/content-build/vocab-images/raw/<set>/` (ignored). Never hotlink.
5. **Convert** to WebP 480×360 (centre crop with a small top bias for people). Strip metadata.
6. **Contact sheet.** A 5×5 grid PNG per set, labelled `term — Vietnamese meaning`, written to `.appdata/local/content-build/review/<set>.png`.
7. **Visual review by the agent (mandatory).** Open every contact sheet and judge each cell: "a learner who sees this picture and reads the term thinks they match, in this set's sense". Reject when:
   - the picture shows another thing or another sense;
   - it has text, a watermark, a logo or a brand;
   - it is a collage;
   - it is a cartoon of a known character;
   - it is unsafe or frightening;
   - it shows a recognisable public figure;
   - it has low contrast or a tiny subject;
   - it is culturally off for Vietnamese learners (for example a pork image for a halal topic is fine, but avoid gore).

   Pick the next candidate. After 3 failed candidates, **replace the term** with another word of the same topic and level and redo its content.
8. **Record provenance** per card: `provider`, `pageUrl`, `author`, `license`, `licenseUrl`, `query`, `sha256` of the final WebP, `width`, `height`, `reviewedAt`. This becomes `ImageSourceJson` (map to `CardImageSource` fields; extra fields only if the DTO has them, otherwise keep them in the manifest).

### 4.2 Attribution
- Cards from CC BY / BY-SA sources must show credit where the app shows image sources. Check the card detail view at C0.
- If the app shows no credit anywhere, prefer Pixabay/CC0/PDM images. Use BY/BY-SA only if a credit is visible in the card detail view; otherwise exclude them.
- Record what the UI shows.

### 4.3 Size budget
- ≈ 1,160 × ≤ 45 KB ≈ 52 MB of package assets, plus legacy repair ≈ 3 MB.
- If the total exceeds **70 MB**, re-encode the largest files at quality 70. If it is still over, stop and report (stop condition 9.2.5).

---

## 5. Listening content (D4, D5)

### 5.1 Lesson completeness contract

| Part | Rule |
| --- | --- |
| Video | Real YouTube id fetched in this run:<br>- public, embeddable (oEmbed 200 + embed check), not age-restricted, not a Short;<br>- 1–12 min for A1–B1, up to 20 min for B2–C2. |
| Rights | `rightsEvidence` = the URL of the source's licence/terms page and the item's own page, plus a one-line quote of the licence statement. Exclude items marked as third-party (AP, Reuters, Getty, music clips). |
| Title / Topic | Vietnamese-friendly English title (≤ 60 chars). `Topic` from the fixed topic list the catalog uses (read existing topics; add new ones only when needed, in English like the existing ones). |
| Level | From the source's own level label when it has one (VOA level 1/2), otherwise judged by speech rate (words per minute from captions: A1 < 100, A2 100–120, B1 120–140, B2 140–160, C1 160–180, C2 > 180 or dense academic content) **and** vocabulary. Record the wpm and the reason. |
| Transcript | From captions (`GetEnglishCaptionsOnlyAsync`; manual captions preferred). **Cross-checked** against the source's published transcript where one exists, such as VOA web pages. Merged into sentence-level lines:<br>- ≤ 25 words, ≥ 1.2 s;<br>- times monotonic, end ≤ duration + 2 s (existing validator);<br>- no `[Music]` lines;<br>- speaker names from the source transcript when known, otherwise `Narrator`/`Speaker A/B`. |
| Translation | `VietnameseMeaning` on **every** line, natural Vietnamese written by the agent and reviewed for meaning. |
| Quiz | 5–8 questions (A1–A2: 5, B1+: 6–8). Four options, exactly one correct, plausible distractors, answerable from the audio only. `Explanation` in Vietnamese with the quoted line and its time (`"…" — 01:23`). |
| Vocab | 6–10 items that **appear in the transcript**, the most useful for the level. `Definition` = Vietnamese meaning (plus a short English gloss in brackets for B2+). `ExampleSentence` = the transcript sentence that uses it. |
| Speakers | `Speaker1/2Name/Country` when known from the source. Otherwise null. |
| TranscriptSource | `youtube-captions+reviewed`. |

### 5.2 Selection process
1. **Candidates.** Build `data/listening/system-a1-c2-2026-11.candidates.json` with 2–3 candidates per slot. Gather them by browsing the sources' official channels and playlists with web search/fetch. Record channel id, playlist, title, duration, and the licence evidence.
2. **Checks.** For each candidate:
   - embeddability;
   - a caption smoke test;
   - the wpm level estimate;
   - a topic-diversity check (no level should have more than 3 lessons of the same topic).
3. **Final manifest.** `data/listening/system-a1-c2-2026-11.json` (schemaVersion 1) with exactly the chosen items and all content of 5.1. Rejected candidates go to `….rejected.json` with reasons.

### 5.3 Fake seed and REACH leftovers (D6, L2)
- Hide the fake seed lessons locally (`IsPublished=false`) via the admin listening page. Record the ids.
- Verify that no REACH listening lesson is still published in Listening. If one is, record it and leave it (Speaking owns that batch); flag it for Châu.

---

## 6. Tools to build (C1)

All new code lives in Trọng-owned new paths. It is a local authoring tool, never a startup seeder or a public endpoint.

| Path | Purpose |
| --- | --- |
| `tools/content/vocab_images.py` | Search, download, convert and build contact sheets (Python 3, Pillow, `requests` from the ignored venv). Reads the Pixabay key from the **local secret store the app uses** (`dotnet user-secrets` or the local env file the scripts read). Never prints or writes it. |
| `tools/content/ipa_check.py` | Free Dictionary API lookup with a local cache in `.appdata/local/content-build/ipa-cache.json`. Rate-limited to 2 requests/s. |
| `tools/content/validate_vocab.py` | Validates `data/content/vocabulary/*.json` against section 3 (errors fail, warnings listed). |
| `tools/content/validate_listening.py` | Validates the listening manifest against 5.1. |
| `tools/content/lib/*` | Shared helpers (slug, hashing, IO). |
| `tools/ContentImport/ContentImport.csproj` + `Program.cs` | .NET runner modelled on `tools/ListeningCatalogImport`: `ProjectReference` to the web project plus EF Relational **only** (no new NuGet package). |
| `scripts/content-import.ps1` | Wrapper modelled on `import-listening-catalog.ps1`: local Docker DB only, refuses any other connection, restores env vars. |
| `TCTEnglish.Tests/ContentManifestTests.cs` | xUnit tests that load the manifests and assert the contracts (counts, required fields, one correct answer, times), so CI guards the data. |

`ContentImport` actions:
- `ImportVocab`: idempotent by `(system owner, folder name, set name, term)`. It creates the folders and sets, and creates or updates cards through the set service or its normaliser. Images are copied into the app's upload path and `ImageUrl` is set. It never deletes a card that has progress; it reports any extra card instead.
- `ImportListening`: idempotent by public `YoutubeId`. It creates or updates the lesson with lines, quiz and vocab. It refuses when an existing public lesson with that id was edited locally in a different way (same conflict rule as `ListeningCatalogImporter`).
- `DryRun`: prints the plan without writing.

Data files (committed):
- `data/content/vocabulary/<folder>/<set>.json`, one per set (schema in 6.1).
- `data/listening/system-a1-c2-2026-11.json`, `.candidates.json`, `.rejected.json`.
- Images are **not** committed under `data/content`. Final WebP files reach Git only as shared-data package assets (D8). Raw downloads stay in the ignored `.appdata`.

### 6.1 Vocabulary set manifest schema (v1)

```json
{
  "schemaVersion": 1,
  "folder": "Từ vựng theo chủ đề – Cơ bản (A1–A2)",
  "set": { "code": "A03", "name": "Trái cây", "description": "#a1 #a2 #fruit #food", "topic": "Trái cây", "wordListBasis": "NGSL + CEFR-J" },
  "cards": [
    {
      "term": "mango", "partsOfSpeech": ["noun"], "cefr": "A1",
      "ipaUs": "/ˈmæŋɡoʊ/", "ipaUk": "/ˈmæŋɡəʊ/", "ipaSource": "https://api.dictionaryapi.dev/…", "ipaVerified": true,
      "definition": "quả xoài", "meaningEn": "a sweet tropical fruit with yellow flesh",
      "example": "I eat a ripe mango every summer.", "exampleTranslation": "Mùa hè nào tôi cũng ăn một quả xoài chín.",
      "details": { "senses": [ { "pos": ["noun"], "meaningVi": "quả xoài", "meaningEn": "…", "examples": [ {"text":"…","vi":"…"}, {"text":"…","vi":"…"} ] } ],
                   "collocations": [ {"text":"mango juice","vi":"nước ép xoài"} ], "plural": "mangoes", "memoryTipVi": null },
      "emoji": "🥭",
      "image": { "file": "a03/mango.webp", "provider": "pixabay", "pageUrl": "…", "author": "…", "license": "Pixabay Content License", "licenseUrl": "https://pixabay.com/service/license-summary/", "query": "mango fruit", "sha256": "…", "width": 480, "height": 360, "reviewedAt": "2026-11-19" }
    }
  ]
}
```

(Values are illustrative, not instructions; the agent writes its own.)

---

## 7. Phases

Each phase ends with the gate (section 8) and a local commit. Long phases commit **per batch of sets or lessons** after a green mini-gate (G1, G4, G5, G9, G11).

### C0 — Baseline and discovery
1. Pull per the prompt; create `feature/system-content-fill`.
2. Build and run the full tests; record the counts.
3. Answer every unknown in 2.4.
4. Reproduce V1–V7 and L1–L5.
5. Write the verdicts into section 12.
6. Run `shared-data.ps1 -Action Status` and `-Action Validate`; record the results.

### C1 — Tools and validators
Build section 6. Unit-test the validators on good and bad fixtures (each rule has a failing fixture). `ContentImport DryRun` must work on an empty manifest.

### C2 — Vocabulary word lists
For all 48 sets, write the manifests with `term`, `partsOfSpeech`, `cefr` and `definition` only. Then a curriculum review:
- topic fit (every word belongs to the set);
- level fit;
- no duplicates;
- picturability (folders B and C).

Commit per folder.

### C3 — Vocabulary content
Fill every field of the contract:
- run `ipa_check` for all;
- write meanings, examples and details.

Run `validate_vocab` to zero errors. Then self-review a **random 10% sample per set** by reading it aloud in your head for naturalness and checking the translations. Record the sample terms and fixes. Commit every 8 sets.

### C4 — Images
Run the pipeline of section 4 for all 48 sets. Review every contact sheet and record the verdict per set (accepted count, replaced terms). Re-run C3 validation for replaced terms. Commit the manifests every 8 sets. Images are not committed yet.

### C5 — Import and package vocabulary
1. `ContentImport ImportVocab` into local Docker.
2. Open in the app:
   - each folder;
   - 3 random sets (cards, images, card detail with examples/collocations);
   - a study session.
3. Export one package per set (D8) with stable keys. Mind 200 files/package.
4. `shared-data.ps1 -Action Validate` and `-Action Sync` (exit 0, `last-sync.json` updated).
5. Review the package JSON for PII/secrets.
6. Commit packages, catalog and source together.

### C6 — Legacy repair (D7)
- Produce images for the 92 legacy cards through the same pipeline (JPEG at the exact old paths; new paths for the 23 missing/remote).
- Edit only `ImageUrl` in the JSON for those 23. Commit the images under `wwwroot/images/<topic>/`.
- Verify with the local seeder (only if `SeedOnStartup` is already true locally; do not change appsettings) or by opening the legacy sets.

### C7 — Listening sourcing
Run section 5.2 steps 1–2. Commit the candidates and rejected files.

### C8 — Listening content
For the chosen 48, build everything in 5.1. Validate to zero errors. Self-review per lesson:
- play 3 random lines in the app later (C9) to confirm times;
- read the whole translation once;
- answer the quiz from the transcript.

Commit every 8 lessons.

### C9 — Import and package Listening
1. `ContentImport ImportListening`.
2. Hide the fake seed lessons (D6).
3. Open the catalog: counts by level match the manifest.
4. Open 6 lessons (one per level):
   - play;
   - line times sync with the highlight;
   - translation toggle;
   - dictation of 2 lines;
   - submit the quiz (scores saved);
   - vocab words are highlighted and looked up from the lesson (redesign);
   - subtitles overlay.
5. Export one package per lesson, then Validate and Sync. Commit.

### C10 — Docs, full gate, report
- `docs/known-issues.md`:
  - D6 fake seeder;
  - D7 misleading legacy names;
  - attribution display;
  - repo size added;
  - sources that ran short.
- `docs/content/system-content-sources.md` (new): the source list, licences, counts, how to add a set or a lesson next time with these tools.
- Tracker rows; `project-structure.md` for the tools.

Then run the full gate and the final check (8.4). Report.

---

## 8. Gates

### 8.1 Phase gate G1–G12

| Gate | Check |
| --- | --- |
| G1 | `dotnet build` (web + tools) |
| G2 | Full test suite; counts ≥ C0 (new `ContentManifestTests` added) |
| G3 | `has-pending-model-changes` → none |
| G4 | `validate_vocab.py` and `validate_listening.py` → 0 errors (warnings listed) |
| G5 | Licence allowlist: every image and lesson has rights evidence; no excluded licence; no hotlinked URL in `ImageUrl` |
| G6 | Image review recorded for every set; no unreviewed image |
| G7 | `shared-data.ps1 -Action Validate` pass; after export, `-Action Sync` exit 0 with a fresh `last-sync.json` |
| G8 | App smoke checks of the phase (C5/C9 lists) with screenshots in `.tmp/content-fill/` |
| G9 | File scope (8.2); `git diff --check`; `scripts/encoding_guard.py` on new text files; LF line endings |
| G10 | Size report: added bytes by folder; ≤ 70 MB total |
| G11 | Secrets/PII scan of the diff and packages (no keys, emails, tokens, local paths, user names) |
| G12 | Regression: legacy sets, Speaking catalog, Reading, Writing and existing personal sets still open; no learner progress row deleted (count before/after) |

Gate rules:
- A failed item → fix and rerun the whole gate.
- 3 attempts, then deferred or blocked with evidence.
- Never weaken a validator to pass. Never mark a skipped check as passed.

### 8.2 File scope

**May add or edit:**
- `tools/content/**`, `tools/ContentImport/**`, `scripts/content-import.ps1`;
- `data/content/**`, `data/listening/system-a1-c2-2026-11*.json`;
- `data/shared/**` (only through `shared-data.ps1`; never hand-edit a merged version);
- `wwwroot/images/<legacy topic folders>/**` (D7);
- `wwwroot/data/system-vocabulary.json` (`ImageUrl` of the 23 cards only);
- `TCTEnglish.Tests/ContentManifestTests.cs`;
- docs listed in C10;
- this plan.

**Forbidden:**
- `Models/**` (including both seeders), migrations, `.csproj` of the web/tests projects, `Program.cs`, appsettings, services/controllers/views of any module.
- Hand edits of the DB outside the tools, the admin hide action (D6) and the app UI.
- Other owners' files.

### 8.3 Data safety
- Local Docker DB only. Never reset or reseed. Never delete cards or lessons that have progress. Never change other users' data.
- Synthetic accounts from `.appdata/local/access.json` for the app checks, without printing passwords.
- Respect the sources:
  - ≤ 2 requests/s per API;
  - a `User-Agent` naming the project;
  - cache results;
  - stop on HTTP 429 with backoff.

### 8.4 Final acceptance check (C10)
1. Counts. Vocabulary: 48 sets, 1,160 ± 5% cards, all with an image and full fields. Listening: per-level counts ≥ the D5 minimums, all published, all with timed lines, translations, quiz and vocab.
2. 20 random cards and 6 random lessons re-checked end to end against the contracts (record them).
3. A fresh-clone simulation. Check out the branch in a temp worktree, run the `shared-data.ps1 -Action Validate` dry check, and confirm the packages contain every asset they reference.

---

## 9. Run rules

### 9.1 Resume
The run is long. After each commit, update the task list and section 12. If interrupted, resume at the first set or lesson without a "done" mark in section 12.

### 9.2 Stop conditions
1. A change needs a forbidden file.
2. A gate item fails 3 times and blocks all later work.
3. The `system` alias cannot be made to map to `system@tct.local`, or new system sets would not appear to learners as system content without code changes. Stop after C4. The content is still useful and is committed as manifests.
4. Captions cannot be fetched for any candidate (service broken). Stop listening after C7 and report. Vocabulary continues.
5. The size budget is exceeded after re-encoding.
6. Any source's terms turn out to forbid the planned use.

---

## 10. Definition of done
- [ ] Section 3 fully delivered (or replaced terms recorded). Every card meets the contract. Every image reviewed and licensed.
- [ ] Section 5 delivered to at least the D5 minimums. Every lesson meets the contract, with rights evidence.
- [ ] D6 and D7 done and recorded.
- [ ] Packages exported, validated and synced. Catalog updated. No hand-edited package.
- [ ] G1–G12 green per phase. Test counts ≥ C0. No forbidden file touched.
- [ ] Docs written. Section 12 complete. Report delivered.

## 11. Commit plan
Local commits only, via `tct-commit-message`, for example:
- `feat(content): add vocabulary and listening authoring tools`
- `feat(content): add system vocabulary lists for folder A`
- `feat(content): add reviewed images and content for sets A01–A08`
- `feat(content): import and package system vocabulary`
- `fix(content): restore images of the legacy system sets`
- `feat(content): add system listening lessons A1–B1`
- `docs(content): record sources, licences and known issues`

No push, no PR, no merge.

## 12. Execution record
_(Filled by the agent: C0 verdicts; per-phase gate records; per-set and per-lesson done marks; replaced terms; rejected candidates summary; final counts; size report.)_

### 12.1 C0 — baseline and discovery (2026-11-19, in progress)

- **Branch:** `feature/system-content-fill` created from `master` = `origin/master` = `7fff8da` (the Listening redesign is already in `master`, pushed 2026-11-19). `scripts/pull-local.ps1` was not run: local `master` already equals `origin/master`, and the tree is not clean (`appsettings.Development.json`, `Claude outputs/`, regenerated `docs/design-system/email-preview/*`).
- **Baseline tests:** not re-run at C0. HEAD has no code change since the Listening final gate (3552 .NET passed / 0 failed / 54 skipped; 536 node passed; `has-pending-model-changes` none). The first full gate re-measures them.
- **Local tools (unknown 6):** Python 3.14.7 through `py` (`python`/`python3` are the Microsoft Store stubs), Pillow 12.3.0 with WebP, no `requests` (use `urllib` or install into the ignored venv), Node 24.19.0, no `ffmpeg`/ImageMagick, Docker MariaDB `tctenglish-mariadb-local` healthy, 135 GB free on D.
- **Network:** reachable with HTTP 200 for `api.dictionaryapi.dev`, YouTube oEmbed, Pixabay docs page, Openverse API and Wikimedia Commons API.
- **Pixabay key (unknown 8 / V7): NOT configured.** `Pixabay:ApiKey` is empty in `appsettings.json`, absent from User Secrets, and there is no environment variable. D2's first provider is therefore unavailable until the owner stores a key; Openverse and Wikimedia need no key. The card detail view shows image source (`Provider`, `PageUrl`, `Author`), so plan 4.2 allows BY and BY-SA images.
- **System owner (V5, unknown 2, partial):** `system@tct.local` exists as user 5, role `System`; `SharedDataEngine` and `SharedDataExporter` look the system user up by that email. The `system` alias mapping still has to be proved with a throwaway export.
- **Listing (unknown 3):** `AddSampleSetsAsync` and `AdminVocabularyService` identify system sets by owner = `system@tct.local` with `Visibility = Public`; new folders must be owned by that user and public.
- **Local DB (unknown 1, read-only counts):** system user owns 3 folders and 8 sets (the plan expects 9; verify at C5). `listeninglessons`: 90 unpublished (A1–C2, 15 per level, REACH leftovers) and 10 published (A1 4, A2 2, B1 2, B2 1, C2 1), which include the fake seed lessons and the fixture lessons 107+ added during the Listening redesign. Which published rows are fake seed versus fixture is still to be listed before D6.
- **Still open at C0:** V2/V3/V4/V6 re-checks, unknown 4 (image upload path), unknown 5 (in-process service call), unknown 7 (caption smoke test on a VOA video), `shared-data.ps1 -Action Status` and `-Action Validate`.

### 12.2 Execution record (2026-11-19, local only)

**Branch:** `feature/system-content-fill`, local commits only (no push, PR, merge or deploy). Author identity was passed per commit.

**Decisions taken and why**
- No Pixabay key and no credit display for other providers: images are CC0 / Public Domain Mark / public domain or original drawings (plan 4.2 allowed BY/BY-SA, but `CardDetail.cshtml` only credits Pixabay, so BY images were not used).
- 509 of 1,160 cards use original artwork drawn by `tools/content/vocab_generate.py` (colours, shapes, body, jobs, places, family, kitchen, emotions, health, tech, travel, shopping, education, media, sports, art, law, relationships, business, science, and all 160 phrase scenes). Photos were used where a clean CC0 photo existed.
- Photos of people, bodies and clothes were avoided (unsafe search results); A07, A08, A09, A14, A23 and phrases are drawings.
- Listening: only NASA (public-domain policy quoted per lesson). VOA videos have no retrievable English captions through the app's service, so A1 has 0 lessons and A2 has 1. Minimums in D5 were therefore **not met** (A1 8, A2 8, B1 8, B2 6, C1 4, C2 3): delivered A2 1, B1 10, B2 6, C1 3, C2 2.
- D6: the 10 published local fixture lessons (`local-fixture`, `tct-fixture-ll`) were hidden with `IsPublished=0` in the local Docker database (ids 1, 107–115). No file was edited.
- D7: all 92 legacy cards have a real image (JPEG 480×360 at the old paths; 31 `ImageUrl` values set). Names untouched.

**Gate results (local)**
- `validate_vocab.py --require-images` over all 48 manifests: 0 errors; manual IPA cards flagged `ipaVerified:false` (see the manifests, ≤ 3 %).
- `validate_listening.py`: 0 errors, 0 warnings.
- `scripts/content-import.ps1`: `ImportVocab` created 1,160 cards, a second run created 0 / unchanged 960 (and the rest after the last sets); `ImportListening` created 22 lessons, a second run reported 22 unchanged.
- `shared-data.ps1 -Action Validate`: exit 0. Packages: one per set (48) and per lesson (22).
- App check over HTTP with a local test account: `/Home/Listening` lists the new lessons and not the fake seed; `/Home/Listening/Practice/130` shows the timed lines, Vietnamese, quiz, vocabulary and the YouTube embed; `/Vocabulary/Index` lists the three new folders; `/Sets/57` renders; an exported image URL returns 200.
- Not observed: a browser session (play, highlight sync, dictation, quiz submit, subtitles overlay) and mobile layouts. Production, staging and CI were not touched.

**Replaced terms:** abstract or unpicturable candidates (e.g. deadline, payslip, name badge, soda, butter, jam, porridge, celery) were swapped for picturable terms of the same topic while writing the sets; terms repeated across sets were replaced so no word appears twice (`dupcheck`).

**Rejected candidates:** VOA Learning English series videos (no English caption track), long podcast compilations (> 20 min and not suitable), third-party news clips.

**Final gate (2026-11-19, local):**
- G1 build web + tools: ok. G2 `dotnet test` (Release, `xUnit.MaxParallelThreads=2`): **3556 passed, 0 failed, 54 skipped** (baseline 3552/0/54; +4 `ContentManifestTests`). Node: 535 passed, 0 failed (`node --test scripts/tests/test_*.cjs`; the earlier baseline quoted 536, the difference was not investigated). Python tool tests: 21 passed.
- G3 `has-pending-model-changes`: none. G4 validators: 0 errors. G7: `shared-data.ps1 -Action Validate` exit 0 (450 releases) and `-Action Sync` exit 0 with a fresh `last-sync.json`.
- G9: no forbidden file touched (`git diff --name-only master...HEAD`); `scripts/encoding_guard.py` clean on new text files. G11: no keys, e-mails or local paths found in the diff or packages (only the word "password" inside example sentences).
- G10: package assets about 30 MB under `data/shared`, `TCTEnglish/wwwroot/images` +1 MB (legacy), branch pack about 76 MB (limit 70 MB for assets only).
- Deviation: an accidental second export created identical version-2 packages for B09–B16; they were never committed and were removed together with their catalog lines before commit.
- Deviation: D6 was done with one SQL `UPDATE ... SET IsPublished=0` on the local Docker database (10 fixture lessons) instead of the admin page.
- Not done / not observed: browser playback checks, mobile layouts, the 20-card/6-lesson random re-check beyond the HTTP smoke test, a fresh-clone worktree simulation, G12 progress-row counts before/after (no learner progress rows were written or deleted by the tools). Local DB legacy cards keep their old `ImageUrl` values until reseeded; the 61 existing paths now resolve to real files.

**Browser check (2026-11-19, local, headless Chrome, Premium test account, Release app on port 5100):**
- Observed at 1440x900 and 390x844: `/Home/Listening` (22 lessons, featured card, level chips), `/Home/Listening/Practice/130` (YouTube embed, transport, speed, tabs), `/Vocabulary/Index` and `/Sets/57` (23 images). No broken image, no console error, no page-level horizontal overflow. The elements reported as wider than the viewport are the intended horizontal scrollers (`.tl-levels`, `.k-seg`, both `overflow-x: auto`).
- Still not observed: audio/video playback, highlight sync, dictation, quiz submit, subtitle overlay, tablet width (768), keyboard-only and zoom passes. These remain open for the owner or a later session.
