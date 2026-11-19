# Execution Prompt: Fix vocabulary course covers that do not match their topic

Follows [the prompt standard](README.md) and [the execution template](../templates/execution-prompt.md).
Save as `.ai/prompts/claude-code-vocabulary-course-covers.md`.

> **Ghi chú cho Trọng.**
> - Lỗi: bìa khóa học ở trang Từ vựng không khớp chủ đề (ví dụ "Động vật hoang dã" hiện cái cặp, "Cơ thể người" hiện bát mì).
> - Cách sửa: bìa lấy **ảnh thật đã duyệt của một từ tiêu biểu trong chính bộ đó**. Bộ không có ảnh dùng hình minh họa trung tính, không bao giờ lấy hình của chủ đề khác.
> - Không migration, không đổi dữ liệu thẻ. Chạy trên nhánh mới tách từ nhánh hiện tại hoặc `master` (xem mục Git). Không push.

## Outcome

1. **Covers match their course.** On `/Home/Vocabulary` (course cards) and on the course detail hero, every system course cover shows a photo **taken from that course's own reviewed card images**, chosen from a curated "cover word" per course:
   - Động vật hoang dã → a wild animal;
   - Đồ uống → a drink;
   - Cơ thể người → a body part;
   - …
2. **Courses without a usable image** (legacy, empty or image-less sets) fall back in this order:
   - a keyword-matched illustration when the name clearly matches one of the 7 existing SVG arts;
   - otherwise a **neutral** illustration.

   The `setId % 7` fallback that currently assigns themed art (suitcase, briefcase, bowl, palette, "Aa") to unrelated courses is removed.
3. All 48 new system sets and the 9 legacy sets are checked visually, and the result is recorded per course.

Success means no course shows a cover from another topic, with no migration, no change to card data, and no N+1 queries.

## Context and evidence (verified 2026-11-19, reproduce first)

- **Where covers come from:**
  - `Services/StudyService.Vocabulary.cs` ≈ line 830: `ArtKey = VocabularyLessonBuilder.ArtKeyFor(setName, setId)`.
  - `Services/Vocabulary/VocabularyLessonBuilder.cs`:
    - `ArtKeys = { star, talk, book, food, travel, jobs, colors }`;
    - `ArtRules` match only a few Vietnamese keywords ("giao tiếp", "oxford", "ăn", "uống", "du lịch", "nghề", "màu", …);
    - **everything else falls back to `ArtKeys[setId % 7]`**, which is the bug. That is why "Động vật hoang dã" gets `jobs`, "Cơ thể người" gets `food`, "Quần áo & phụ kiện" gets `travel`.
  - The view model is `VocabularyCourseCardViewModel.ArtKey` (`ViewModels/VocabularyPageViewModels.cs`).
  - The views are `Views/Vocabulary/_CourseCard.cshtml` and `Views/Vocabulary/Detail.cshtml` (≈ line 40); both render `<partial name="_CourseArt" model="ArtKey" />`.
  - The SVGs are in `Views/Vocabulary/_CourseArt.cshtml`; the backgrounds are `.tv-cover--<key>` in `wwwroot/css/vocabulary.css` ≈ line 565.
- **Content:** the system-content run created 48 sets (folders A/B/C) with a reviewed, licensed image on every card. The manifests are in `data/content/vocabulary/{a-co-ban,b-trung-cap,c-giao-tiep-hang-ngay}/*.json`. The legacy 9 sets had their images repaired in that run (verify).
- `Set` has no cover column (`SetId, SetName, OwnerId, FolderId, Description, Visibility, …`). **Do not add one.**
- **Rules:** `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`. Clay tokens only. No inline style blocks, scripts or handlers. The existing inline `style="--p:…"` progress pattern may stay as it is.

Unknowns to resolve in R0:
1. Grep every other place that renders `_CourseArt` or uses `ArtKey`, such as the Home dashboard or the "Bộ thẻ" pages.
2. How card `ImageUrl` values look after the shared-data import (`/uploads/shared/<hash>.webp`, `/images/...`).
3. The broken-image fallback pattern already used in the vocabulary JS (reuse it).
4. Which system sets exist in the local DB, with their ids and names (system owner `system@tct.local`).

## Design

### D1 — Cover word catalogue (code, no DB change)
Add `Services/Vocabulary/VocabularyCoverCatalog.cs`, a static map **set name → cover term**, for all 48 new sets plus the legacy sets that have a fitting image.

- **Choosing the term** (the agent picks it by looking at the images, R2). Pick the most iconic, simplest picture of the set: one clear subject, readable at about 280×150 px, no busy background. Examples:
  - Động vật hoang dã → `tiger` or `elephant`;
  - Trái cây → `mango` or `watermelon`;
  - Đồ uống → `orange juice`;
  - Cơ thể người → `hand`;
  - Quần áo & phụ kiện → `jacket`;
  - Màu sắc & hình dạng → a colourful one.

  For phrase sets (folder C), pick the clearest situation photo.
- The map key is the exact `SetName` (sets are matched by name, the same convention as the seeder and the packages). Comparison is ordinal after trim.
- Also expose a mirror of the map as data in `data/content/vocabulary/covers.json` (`{ "setName": "…", "coverTerm": "…", "reason": "…" }`) so the content tools and the next content run keep it in sync. Add a test that the two agree.

### D2 — Resolve the cover in the service (one query)
In the vocabulary home and detail building code (`StudyService.Vocabulary.cs`), for all listed courses at once:
1. Collect `(setId, coverTerm)` pairs from the catalogue.
2. In **one** query, load the `ImageUrl` of cards whose `SetId` is in the list and whose `Term` (case-insensitive, trimmed) equals that set's cover term.
3. For sets with no curated term, or no match, load in the **same or one more grouped query** the image of the **first card by `CardId` that has a safe image URL**. This guarantees an on-topic picture for any set that has images, including personal or admin-made ones.
4. Accept only safe URLs: local paths starting with `/` but not `//`, or `https://`. Anything else is ignored.

Add to `VocabularyCourseCardViewModel`:
- `CoverImageUrl` (string?);
- `CoverAlt` (string; the cover term; the image stays decorative).

Keep `ArtKey` for the fallback.

### D3 — Fix the illustration fallback
In `VocabularyLessonBuilder.ArtKeyFor`:
- Keep the keyword rules and extend them only where the 7 arts truly fit:
  - `food` also matches "trái cây", "rau", "món", "nấu", "bếp";
  - `travel` also matches "giao thông", "sân bay", "khách sạn", "phương tiện";
  - `jobs` also matches "văn phòng", "kinh doanh";
  - `colors` also matches "hình dạng", "nghệ thuật", "nhạc cụ";
  - `talk` also matches "chào hỏi", "lịch sự", "điện thoại", "xã giao", "hỏi đường";
  - `book` also matches "trường học", "giáo dục", "học tập".
- **Remove the `setId % 7` fallback.** The fallback is the neutral `star` art. If `star` looks themed, simplify it into an abstract shape that suggests "vocabulary" in general, such as letter tiles.
- Update the XML comment and the tests that relied on the modulo.

### D4 — Render
In `_CourseCard.cshtml` and the `Detail.cshtml` hero:
- **When `CoverImageUrl` is set:**
  - render `<img class="tv-cover__photo" src="…" alt="" width="480" height="360" loading="lazy" decoding="async">` inside the existing cover box (`object-fit: cover`, same radius, the level and status badges stay on top with their current contrast — add a subtle top gradient scrim **only if** the scripted contrast check of the badges fails on photos);
  - add a modifier `tv-cover--photo` (neutral background token while loading).
- **Otherwise:** the existing `_CourseArt` partial with the fixed key.
- **Broken image:** reuse the existing JS fallback pattern (no inline `onerror`). Hide the `img` and show the art partial, which is already rendered hidden next to it (`hidden` toggled by JS). Without JS the image simply shows, or the browser shows nothing; acceptable.
- Detail hero: same rule, larger size, `loading="eager"` because it is above the fold.
- Reduced motion and forced colors: no new animation. In forced colors the photo keeps a 1 px `CanvasText` border.

## Mode, authority and scope

- Mode: `IMPLEMENT_LOCAL`, one agent, continuous run R0 → R4, no subagents.
- **Git:**
  - If `feature/system-content-fill` is not merged yet, branch `fix/vocabulary-course-covers` **from it**, because the covers depend on its sets. Otherwise branch from an up-to-date `master` (pull with `scripts/pull-local.ps1`; only with a clean tree apart from `appsettings.Development.json` and `Claude outputs/`).
  - Local commits only, one per phase, via `tct-commit-message`.
  - Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, `.appdata/`, screenshots.
  - No push, PR, merge, rebase, reset.
- **Allowed files:**
  - `Services/Vocabulary/VocabularyLessonBuilder.cs`, new `Services/Vocabulary/VocabularyCoverCatalog.cs`;
  - `Services/StudyService.Vocabulary.cs` (cover resolution only);
  - `ViewModels/VocabularyPageViewModels.cs` (additive);
  - `Views/Vocabulary/_CourseCard.cshtml`, `Views/Vocabulary/Detail.cshtml` (cover area only), `Views/Vocabulary/_CourseArt.cshtml` (`star` simplification only);
  - `wwwroot/css/vocabulary.css`, `wwwroot/js/vocabulary-home.js` / `vocabulary-course.js` (image fallback only);
  - `data/content/vocabulary/covers.json`;
  - tests (new file `TCTEnglish.Tests/VocabularyCoverTests.cs`, plus additions to existing vocabulary tests without weakening any);
  - docs (`bug-fix-log.md`, `known-issues.md` if needed).
- **Forbidden:**
  - migrations, `Models/**`, `Program.cs`, appsettings, `.csproj`;
  - shared layouts, `site.css`, `study-kit.css`;
  - card or set data in the DB, the shared-data packages, the content manifests other than `covers.json`;
  - other owners' files;
  - new packages or CDNs.
- **Data:** read-only on the DB; never reset or reseed; synthetic accounts from `.appdata/local/access.json` only, never printing passwords.

## Execution

1. **R0 — Reproduce.**
   - Screenshot `/Home/Vocabulary` (1440×900, full page) and three course detail pages into `.tmp/vocab-covers/before/`.
   - List every system course with its current `ArtKey` and whether it fits (table in the report).
   - Resolve the unknowns.
2. **R1 — Logic, tests first.**
   - `ArtKeyFor`:
     - new keywords map correctly;
     - unknown names → `star`;
     - **no themed key is ever returned for a name without a keyword** (property test over all 57 names plus random strings).
   - Catalogue:
     - every one of the 48 new set names has an entry;
     - every cover term exists in that set's manifest **and** that card has an image;
     - `covers.json` equals the C# map.
   - Service:
     - curated image wins;
     - first-image fallback;
     - unsafe URLs ignored;
     - no image → null;
     - **query count stays constant** for 1 vs 50 courses (use the existing query-counting helper or an EF interceptor in tests).
   - Implement D1–D3.
3. **R2 — Choose the cover words visually.**
   - For each of the 48 sets, build a contact sheet of its 25 card images (reuse the content tools' sheet script) and pick the cover term by the rule in D1.
   - Record the choice and a one-line reason in `covers.json`.
   - Do the same for legacy sets that have good images.
4. **R3 — Render.**
   - Implement D4 and the fallback JS.
   - Check:
     - the Vocabulary home at 1440×900, 1366×768, 1024, 768, 390 and 200% zoom;
     - three detail pages;
     - a personal set with images (first-image fallback);
     - a set without images (neutral art);
     - a broken image (rename the URL in a test view model, not in the DB).
   - Scripted contrast of the level and status badges on the 10 brightest photos.
   - Console and network clean.
5. **R4 — Visual acceptance and docs.**
   - Full-page screenshots after the change into `.tmp/vocab-covers/after/`.
   - Go through **every** course card and record "matches / does not match" with the cover term. Any "does not match" → change the cover term (R2) and re-check. Done only at 0 mismatches.
   - Docs: `bug-fix-log.md` (root cause: the modulo fallback), and how to set a cover for a new system set (add to `VocabularyCoverCatalog` and `covers.json`).

**Phase gate after each phase** (all must pass; fix and rerun on failure; at most 3 attempts per item, then deferred with evidence):

| Gate | Check |
| --- | --- |
| G1 | build |
| G2 | vocabulary tests + new cover tests |
| G3 | full suite (counts ≥ R0) |
| G4 | node tests (if JS changed) |
| G5 | `has-pending-model-changes` → none |
| G6 | token grep, no new inline blocks/handlers, `git diff --check` |
| G7 | file scope as above |
| G8 | render matrix of R3, console and network clean |
| G9 | contrast and accessibility (decorative `alt=""`, the card link name unchanged, focus ring intact) |
| G10 | regression: the vocabulary lesson/study pages, the Bộ thẻ pages, the Home dashboard |

Never weaken or delete a test, never touch a forbidden file, never call a skipped check passed.

## Verification and acceptance

- [ ] Root cause reproduced and fixed; `setId % 7` themed fallback removed.
- [ ] All 48 new system courses show a photo from their own set; R4 table has 0 mismatches.
- [ ] Legacy and image-less sets show a fitting or neutral illustration, never another topic's.
- [ ] One grouped query (constant count) for covers; tests prove it.
- [ ] No migration, no DB/content data change apart from `covers.json`; gates green.

## Handoff

Report in Vietnamese, once, after R4:
- the root cause in one paragraph;
- the before/after table per course (name, old art, new cover term, matches);
- changed files;
- test and command results with counts;
- screenshot paths;
- what was not verified;
- how to set the cover of a future system set.

List the local commits and state that nothing was pushed or merged and no data was changed.
