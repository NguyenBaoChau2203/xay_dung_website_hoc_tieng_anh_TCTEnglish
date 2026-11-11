# Personal Sets Redesign — scoped plan (2026-11-09)

> **Tóm tắt cho Trọng (tiếng Việt).** Bản này thay thế bản 04/11. Theo `AGENTS.md`
> mới, phần chỉ dẫn cho agent viết bằng **tiếng Anh**. Ghi chú cho người trong team
> được phép viết tiếng Việt. D1–D4 ở mục 1.2 đã được điền **CÓ** cả 4 (09/11/2026).
> Prompt giao việc nằm ở file riêng
> `.ai/prompts/claude-code-personal-sets-redesign.md`.

- Owner / page owner: Huỳnh Phú Trọng (Vocabulary, Set/Card, Folder module per `README.md`).
- Save this file as `docs/superpowers/plans/2026-11-09-personal-sets-redesign.md`.
- Supersedes the 2026-11-04 draft (never committed). Do not use the old draft.
- Baseline observed while writing this plan: `master` at `eed4e2c` (PR #131, Hybrid RAG).
  Latest migration `20261107043157_AddAiMessageGroundingMetadata`. Re-verify both
  at P0; the checkout may have moved.
- Reference product: `tronghp160/English_China` (Next.js). Use it for **behavior and
  layout ideas only**. Do not copy TS/React code or add any of its packages. Everything
  needed from it is described below, so the agent does not have to open that repo.
- Visual reference: the owner's canvas "Mockup Bộ thẻ cá nhân" (6 artboards).
  It is illustrative. Where it differs from the design system (weight 800, private
  hex colors, hand-drawn icons), **the design system wins**.

---

## 0. What changed since the 2026-11-04 draft (why this rewrite exists)

| Change on master | Effect on this plan |
| --- | --- |
| Shared design system (PR #130): `tct-learning-ui.css`, `--tct-*` tokens, `.tct-theme`, `_PageHeader`, `_StateMessage`, `team-ui-workflow.md`, AGENTS.md "Shared UI foundation" | Drop the private hex palette. Page CSS uses only `var(--tct-*)`. Pages are wrapped in `tct-theme`. Use the shared partials and components. Max font weight 700. Use z-index tokens. Add a row to the adoption tracker. |
| Prompt authoring standard (PR #129): English execution prompts, `.ai/templates/execution-prompt.md` | Plan and prompt are in English. The report to the owner stays in Vietnamese. |
| Shared content packages (PR #127/#128): `Folder`/`Set`/`Card` are exportable kinds in `SharedDataRegistry.cs`; the importer creates rows with `Activator.CreateInstance` and sets only the listed fields | New `Set` columns need safe C# defaults (`Visibility` defaults to `Public`). `ShareSlug`/`PersonalNote` must never be exported. Exporting new card fields is owner decision D4. |
| Hybrid RAG chat (PR #131): `LearnerCardAccess.Authorized` (CardLookupRetriever) and citations to `/Home/Study/{SetId}` (GroundingCitationService) | Do not edit AI files. The new view rule must be a **superset** of the chat rule, so chat citations keep opening. Run the AI regression tests. |
| Local Whisper retired; `start-local.ps1` = MariaDB + web; `scripts/pull-local.ps1` after pulls | P0 and P12 use the current launcher and sync wrapper. |
| `TimeProvider.System` registered; MariaDB acceptance infra (`TCT_ACCEPTANCE_MARIADB`, `scripts/bootstrap-mariadb-acceptance.ps1`), `SharedDataMariaDbTests` in CI | Use these for verification instead of ad-hoc containers. |
| No change on master to Set/Card/Folder/Study views, `SetController`, `FolderController`, `StudyService` | The bugs B1–B7 found on 2026-11-04 are still present. Confirm each one at P0. |

---

## 1. Decisions

### 1.1 Fixed decisions (do not change)

| ID | Decision | Reason |
| --- | --- | --- |
| F1 | Keep the existing study algorithm in `StudyService.UpdateCardProgressAsync` (known → `Mastered` + 7 days; unknown → `Learning` + 1 minute). "Due" = progress exists, not suspended, `NextReviewDate <= UtcNow`. No FSRS. | FSRS is out of scope and would affect Goals and the AI retrievers (Châu's). |
| F2 | UI terms: **"Bộ thẻ"** for set, **"từ"/"thẻ"** for card. List page title: **"Bộ thẻ của tôi"**. | Matches existing product copy. |
| F3 | AI autofill, batch enrich and image search stay **Premium/Admin only**, gated exactly like `SetController.SuggestVocab`. Other users see a locked control that opens `_PremiumLockedModal`. | Keeps the existing Premium policy and quota cost. |
| F4 | Drop entirely from English_China: the Bin/dog mascot and anything mascot-related, Chinese-language features (pinyin, Hanzi, HSK, Hán Việt, tones, handwriting), study groups, the explore page, PWA/offline/push, separate XP, embedded tutor chat, photo OCR import, a page-specific dark mode. | Owner request. English-only product. Other parts belong to other modules or need new infrastructure. |
| F5 | The four study modes keep their existing pages (`Study`, `QuizMode`, `WriteMode`, `MatchingMode`). The set detail page only links to them and shows real numbers. Only the touch points in P10 change. | Limits regression risk. |
| F6 | Preserve legacy contracts: `GET/POST /Home/CreateSet`, `GET /Home/EditSet/{id}`, `POST /Home/EditSet` (legacy `Terms`/`Definitions`/`ExistingImageUrls` form), `POST /Home/DeleteSet`, `POST /Home/RemoveSetFromFolder`, `/Home/Study/{id}`, `/Home/QuizMode/{id}`, `/Home/WriteMode/{id}`, `/Home/MatchingMode/{id}`, `POST /Home/UpdateCardProgress`, `GET /Home/SuggestVocab`. `FolderSetIdorRegressionTests` and the Sprint smoke tests stay green. | AGENTS.md: preserve HTTP/JSON contracts. |
| F7 | New `Sets.Visibility` (`Private`/`Public`). The migration default and the **C# property default are both `Public`**, so seeders, Admin, `LocalDevelopmentBootstrap` and the shared-data importer keep today's behavior. Only learner-created sets (new UI **and** legacy `POST /Home/CreateSet`) and clones are assigned `Private` explicitly in service code. | Backward compatible. Imported shared sets stay studyable. New personal sets are private. |
| F8 | Do not edit Châu's AI code (`Services/AI/**`) or the shared-data engine (`Infrastructure/SharedData/**`) except as D4 allows. The new `CanView` rule must include everything `LearnerCardAccess.Authorized` allows, so chat citations to `/Home/Study/{SetId}` keep working. | Owner boundaries. Chat regression. |
| F9 | No new `:root`, hex value, font, font weight above 700, icon family, package or CDN. Page CSS lives in `wwwroot/css/personal-sets.css`, scoped under `.ts-page`, consuming `var(--tct-*)`. No edits to `tct-learning-ui.css`, `layout.css`, `site.css`, either layout or the shared partials (except D2). Any missing token becomes a written proposal (see `team-ui-workflow.md` section 5) plus a scoped local fallback built from existing tokens. | AGENTS.md "Shared UI foundation", DESIGN.md. |

### 1.2 Owner decisions — Trọng fills these before handoff

Mỗi ô ghi **CÓ** hoặc **KHÔNG**. Ô để trống được hiểu là **KHÔNG**.

| ID | Question (who approves) | If CÓ | If KHÔNG / blank |
| --- | --- | --- | --- |
| D1 | Có được thêm **1 dòng** đăng ký service vào `Program.cs` không? (Châu) | Option A: new `IPersonalSetService` (section 5.4). | Option B: methods on `IStudyService` + partial `StudyService.PersonalSets.cs`; AI methods on `IVocabSuggestService`. `Program.cs` untouched. |
| D2 | Có được thêm **1 mục** "Bộ thẻ của tôi" vào sidebar `_Layout.cshtml` không? (Tiến / Châu) | Add exactly one `nav-sub-item` in `#folderDropdownMenu` (P10.7). | Entry points only on Trọng's pages (Folder, FolderDetail, Vocabulary/Index, Study). |
| D3 | Có được thêm **1 dòng** vào `docs/design-system/team-ui-workflow.md` mục 6 (adoption tracker) và đề xuất token màu bìa (nếu cần) không? (Châu) | Add tracker row "Bộ thẻ cá nhân". Write the token proposal file if the six existing soft pairs are judged insufficient. | Record the tracker row text and any proposal in the plan's execution notes only. |
| D4 | Có được sửa `Infrastructure/SharedData/SharedDataRegistry.cs` không? (Châu) | Append portable card content fields to the `Card` spec; add an `AssertShareable` rule that rejects exporting a `Private` set; add regression tests (P1.6). | Registry untouched. Shared packages keep carrying only legacy card fields (new fields are dropped on export). Recorded as a known limitation. |

**Recorded owner decisions (2026-11-09, reported by Trọng; approvals obtained from the
responsible teammates):**

D1 = **CÓ**    D2 = **CÓ**    D3 = **CÓ**    D4 = **CÓ**

Consequences the agent must apply:

- **D1:** use Option A (`IPersonalSetService`). The grant covers exactly one
  `AddScoped` line in `Program.cs`. It does **not** cover `AddMemoryCache` or any other
  registration, so P6 skips caching unless `IMemoryCache` already resolves.
- **D2:** add exactly one sidebar item (P10.7).
- **D3:** add the tracker row (P11), and write a token proposal only if one is needed.
- **D4:** extend the `Card` spec and the private-set export guard, with tests (6.5).
  Still never export `Visibility`, `ShareSlug`, `ClonedFromSetId` or `PersonalNote`.

---

## 2. Scope

### 2.1 In scope

1. Set list `/Sets`.
2. Create set (keep URL `/Home/CreateSet`) and set settings `/Sets/{id}/Settings`. `GET /Home/EditSet/{id}` renders the same settings view. Delete set with type-the-name confirmation.
3. Set detail `/Sets/{id}`:
   - Header with cover.
   - Study / follow / add-words / "⋯" actions.
   - "Học bộ này" strip with the 4 modes.
   - Word list: search, filters, list/grid views, infinite scroll, multi-select bulk actions, share dialog.
4. Add words `/Sets/{id}/Add`:
   - Many cards at once; pasting a list splits it into cards; autosaved draft.
   - AI suggest, duplicate warnings, image, 4-tab details drawer, sticky save bar, shortcuts.
5. Card detail `/Sets/Cards/{cardId}` and edit `/Sets/Cards/{cardId}/Edit`. Includes delete and per-user suspend.
6. Bulk import `/Sets/{id}/Import`: paste text or upload CSV/TXT, preview, optional AI batch enrich, progress, retry.
7. CSV export `/Sets/{id}/Export`.
8. Share link (enable / disable / regenerate) and public page `/s/{slug}`:
   - **Follow** ("Lưu vào bộ thẻ của tôi"): add the set to the user's followed sets.
   - **Clone** ("Sao chép về bộ của tôi"): make an editable private copy.
   - **Unfollow**.
9. "Thêm bộ thẻ mẫu" in the empty state: clone up to 2 system sets (owner `system@tct.local`).
10. Connect Trọng's existing pages to the new ones:
    - Folder, FolderDetail: link to the new pages; hide private sets from others.
    - Study / QuizMode / WriteMode / MatchingMode: back links to set detail; add a "due review" mode.
11. Fix B1–B7 (section 3.3).
12. Migration, MariaDB verification and automated tests.

### 2.2 Out of scope

- Everything in F4.
- Admin `SetManagement` screens (only ensure they still build and work).
- Goals, streak, XP, AI Chat, Account (Châu); Dashboard, Layout, Class (Tiến), except D2.
- Game logic inside the four study modes beyond P10.
- Converting the legacy `vocabulary-system.css` / Folder CSS to tokens (separate adoption task).

---

## 3. Current state (verified 2026-11-09 against `eed4e2c`; re-check at P0)

### 3.1 Data

- **`Models/Set.cs`** (`TCTVocabulary.Models`)
  - Columns: `SetId`, `SetName` (255), `OwnerId`, `FolderId?`, `CreatedAt?`, `Description?`, `ViewCount`.
  - Table `Sets`, columns named `SetID`/`OwnerID`/`FolderID`.
- **`Models/Card.cs`**
  - Columns: `CardId`, `SetId`, `Term` (255), `Definition`, `ImageUrl?`, `Phonetic?`, `Example?`, `ExampleTranslation?`, `Topic?`.
  - FK `Cards → Sets` is `ClientSetNull`.
- **`Models/LearningProgress.cs`**
  - Columns: `ProgressId`, `UserId`, `CardId`, `Status?` (`Learning`/`Reviewing`/`Mastered`), `WrongCount?`, `RepetitionCount`, `LastReviewedDate?`, `NextReviewDate?`.
  - Unique `(UserId, CardId)`.
  - FK to `Cards` is `ClientSetNull`, so progress must be deleted before its card.
- **Related**
  - `Folder`, `SavedFolder`, `ClassFolder`, `ClassMember`, `Class.OwnerId`.
  - System sets belong to `system@tct.local`. The shared-data importer also maps the `system` owner alias to that user.
  - Account deletion (`AccountController` around lines 780–1025) removes the user's progress, cards, sets and saved folders explicitly with EF.
- **Shared data**
  - `SharedDataRegistry`: `Set` exports `SetName Description` (owner `OwnerId`, ref `FolderId`).
  - `Card` exports `Term Definition ImageUrl Phonetic Example ExampleTranslation Topic`.
  - The importer `SetValues` writes only those fields; `SetCreationDates` only touches non-nullable `DateTime` properties named `CreatedAt`/…

### 3.2 Code

- **`Controllers/SetController.cs`**
  - Attributes: `[Route("Home")]`, `[AutoValidateAntiforgeryToken]`, `[Authorize]`.
  - Actions: `SuggestVocab`, `CreateSet` GET/POST, `DeleteSet`, `RemoveSetFromFolder`, `EditSet` GET/POST.
- **`Controllers/FolderController.cs`**: any signed-in user can open any folder detail.
- **`Controllers/StudyController.cs`** → `IStudyService.GetStudyViewModelAsync(setId, userId)`. Has no set-level view check.
- **`Services/VocabSuggestService.cs`**: AI phonetic + ≤3 senses + 1 example; Pixabay 6 images (`Pixabay:ApiKey`).
- **`IAiRequestRateLimiter`** (singleton) and `IWritingRequestRateLimiter` exist. **No `AddMemoryCache` in `Program.cs`.**
- **`Views/Set/_SetEditorForm.cshtml`** (~879 lines): inline CSS overriding `:root`/`body`, inline JS, dead "Nhập dữ liệu" and "Thêm sơ đồ" buttons.
- **`Views/Study/Study.cshtml`**: back link goes to `FolderDetail/{FolderId}`, which is `/Home/FolderDetail/0` (a 404) for sets without a folder.
- **AI chat**
  - `LearnerCardAccess.Authorized` (in `Services/AI/Internal/Retrievers/CardLookupRetriever.cs`) allows: set owner, folder owner, saved folder, or class folder where the user owns the class or is a member.
  - `GroundingCitationService` links cards to `/Home/Study/{card.SetId}`.
- **Shell** (`_Layout.cshtml`)
  - Load order: Bootstrap 5.3.2 → `site.css` → `tct-learning-ui.css` → `layout.css` → page `Styles`.
  - Font Awesome 6.0.0 + Material Symbols. Sidebar has an `id="mainSidebar"`. `@RenderSectionAsync("Styles"/"Scripts")`.
- **Design-system pieces available**
  - Partials: `_PageHeader`, `_StateMessage` (`ViewModels/SharedUiViewModels.cs`).
  - Classes:
    - Theme and page: `.tct-theme`, `.tct-page`, `.tct-page--narrow`
    - Toolbar: `.tct-toolbar` (`__search`, `__filters`, `__actions`)
    - Cards and badges: `.tct-card`, `--interactive`, `.tct-badge--{primary,success,warning,danger,info}`
    - Tables: `.tct-table-wrap`, `.tct-table`
    - Skeletons: `.tct-skeleton`, `--line`, `--short`, `.tct-skeleton-group`
    - Text: `.tct-phonetic`, `.tct-prose`, `.tct-break`, `.tct-clamp-2`
    - Other: `.tct-btn-icon`, `.tct-on-dark`, `.tct-focusable`
  - Tokens:
    - Color: primary / success / warning / danger / info + `-soft` / `-text`, `--tct-accent` / `--tct-on-accent`
    - Space, radius, shadow: `--tct-space-*`, `--tct-radius-*`, `--tct-shadow-*`
    - Size and type: `--tct-target-min`, `--tct-control-h`, `--tct-text-*`, `--tct-weight-*`
    - Layout and motion: `--tct-sidebar-w`, `--tct-sidebar-w-md`, `--tct-z-*`, `--tct-dur*`
  - Gallery: `docs/design-system/preview.html`.
- **Tests**
  - `TCTEnglish.Tests/FolderSetIdorRegressionTests.cs`, `Sprint2SmokeTests.cs`, `Sprint3SmokeTests.cs`.
  - Infrastructure: `TestWebApplicationFactory`, `IntegrationTestClientHelper`, `TestDataIds` (`UserId=101`, `OutsiderUserId=103`, `UserFolderId=201`, `UserSetId=301`, `UserCardId=401`, `SystemSetId=302`); `TestHelpers/StubAiProviderClient.cs`.
  - MariaDB acceptance: `MariaDbAcceptanceFactory` gated by `TCT_ACCEPTANCE_MARIADB`.
  - AI chat tests: `AiRag*`, `AiPhase*`.
  - JS behavior tests: `scripts/tests/*.cjs` (`node:test`, `dom_fixture.cjs`).

### 3.3 Bugs to fix (confirm each at P0 before changing it)

| ID | Defect | Impact | Fixed in |
| --- | --- | --- | --- |
| B1 | `POST /Home/EditSet` deletes every card and its `LearningProgress`, then recreates them | Each edit wipes learning progress and `Phonetic`/`Example`/`ExampleTranslation`/`Topic` | P4.4 |
| B2 | `DeleteSet`/`RemoveSetFromFolder` delete cards without deleting `LearningProgress` | FK failure once any card has progress | P4.4 |
| B3 | `DownloadAndSaveExternalImageAsync` downloads any `http(s)` URL the client sends | SSRF risk; no type/size check | P4.5 |
| B4 | Study/Quiz/Write/Matching have no set view check | Read IDOR by id | P10 (safe because legacy sets become `Public`) |
| B5 | `_SetEditorForm.cshtml` overrides `:root`/`body` and uses inline styles | Violates DESIGN.md | Replaced (P5); partial removed (P11) |
| B6 | Study exit link → `/Home/FolderDetail/0` when the set has no folder | 404 | P10 |
| B7 | `UpdateCardProgress` does not check that the card exists and is viewable | Progress written to unviewable cards, FK errors | P10 |

---

## 4. Mapping English_China → TCT English

### 4.1 Concepts

| English_China | TCT English | Note |
| --- | --- | --- |
| deck / word | `Set` / `Card` | |
| `term` / `meaning_vi` | `Card.Term` / `Card.Definition` (required) | |
| `reading` (IPA) | `Card.Phonetic` (US) + new `Card.PhoneticUk` | |
| `meaning_en` | new `Card.MeaningEn` | |
| `parts_of_speech[]` | new `Card.PartsOfSpeech` (CSV of codes) | Filterable |
| `examples[]` | `Card.Example` + `Card.ExampleTranslation` (first) + new `DetailsJson.examples` (≤6) | Keep legacy columns: Study and the AI retrievers read them |
| `synonyms/antonyms/collocations/word_family`, `verb_forms`, `plural` | new `Card.DetailsJson` | |
| `lang_extra.cefr` | new `Card.Cefr` | Filterable |
| `topic` | `Card.Topic` | Filterable |
| `emoji` | new `Card.Emoji` | |
| `image_url` + `image_source` | `Card.ImageUrl` + new `Card.ImageSourceJson` | provider `pixabay` / `upload` |
| `personal_note` | new `Card.PersonalNote` | **Never exported** |
| `ai_generated` | new `Card.AiGenerated` | |
| `created_at/updated_at` | new `Card.CreatedAt?`, `Card.UpdatedAt?` | Legacy rows stay null; sort by `CreatedAt` then `CardId` |
| `user_cards.state/lapses/due/suspended` | `LearningProgress.Status/WrongCount/NextReviewDate` + new `IsSuspended` | F1 |
| `decks.visibility` | new `Set.Visibility` | `Private`/`Public` only |
| `decks.share_slug` | new `Set.ShareSlug` | Secret capability, never exported |
| `decks.cloned_from` | new `Set.ClonedFromSetId` | |
| `deck_subscriptions` | new table `SetSubscriptions` | |
| groups | existing `SavedFolder` + `ClassFolder` (read-only here) | |
| `enable_recall`, `allow_member_edit`, `language` | dropped | |

### 4.2 Per-viewer word status (priority: suspended > hard > learned > learning > new)

| Key | Label | Condition (progress `p` of the current user) | Badge |
| --- | --- | --- | --- |
| `suspended` | Đang tạm ẩn | `p.IsSuspended` | `.tct-badge` + scoped `.ts-badge--dashed` (dashed `--tct-border-control`) |
| `hard` | Hay quên | `!p.IsSuspended && (p.WrongCount ?? 0) >= 3` | `.tct-badge--danger` |
| `learned` | Đã thuộc | `p.Status == "Mastered"` | `.tct-badge--success` |
| `learning` | Đang học | `p.Status in ("Learning","Reviewing")` | `.tct-badge--warning` |
| `new` | Mới | `p == null \|\| p.Status == null` | `.tct-badge` (neutral) |

- **Due:** `p != null && !p.IsSuspended && p.NextReviewDate != null && p.NextReviewDate <= now`.
- **Set progress:** % learned = (total − new) / total.
- Every badge carries its text label, never color alone.

### 4.3 Study modes

| English_China | TCT English | Route |
| --- | --- | --- |
| Learn | Viết (WriteMode) | `/Home/WriteMode/{id}` |
| Test | Trắc nghiệm (QuizMode) | `/Home/QuizMode/{id}` |
| Match | Ghép thẻ (MatchingMode) | `/Home/MatchingMode/{id}` |
| Flashcard | Thẻ ghi nhớ (Study) | `/Home/Study/{id}` |
| Review this deck | Study, due mode | `/Home/Study/{id}?mode=due` (P10) |

### 4.4 Icons (Font Awesome 6.0.0 only; check each name exists in 6.0.0)

| Meaning | FA class |
| --- | --- |
| Add | `fa-solid fa-plus` |
| Review / play | `fa-solid fa-play` |
| Private | `fa-solid fa-lock` |
| Public | `fa-solid fa-globe` |
| Shared by link | `fa-solid fa-link` |
| Shared via folder / class | `fa-solid fa-users` |
| Done | `fa-solid fa-check` |
| Search | `fa-solid fa-magnifying-glass` |
| List view | `fa-solid fa-list` |
| Grid view | `fa-solid fa-table-cells-large` |
| Select mode | `fa-regular fa-square-check` |
| Close | `fa-solid fa-xmark` |
| More options | `fa-solid fa-ellipsis` |
| Import | `fa-solid fa-file-import` |
| Export | `fa-solid fa-file-export` |
| Share | `fa-solid fa-share-nodes` |
| Copy | `fa-regular fa-copy` |
| Settings | `fa-solid fa-gear` |
| Follow / unfollow | `fa-regular fa-bookmark` / `fa-solid fa-bookmark` |
| Speak | `fa-solid fa-volume-high` |
| AI suggest | `fa-solid fa-lightbulb` |
| Premium lock | `fa-solid fa-crown` |
| Image | `fa-regular fa-image` |
| Details | `fa-solid fa-sliders` |
| Delete | `fa-regular fa-trash-can` |
| Warning | `fa-solid fa-triangle-exclamation` |
| Error | `fa-solid fa-circle-exclamation` |
| Loading | Bootstrap `.spinner-border` (pending pattern in DESIGN.md) |
| Empty set | `fa-solid fa-book-open` |
| Back | `fa-solid fa-arrow-left` |
| Edit | `fa-solid fa-pen` |
| Flashcards | `fa-solid fa-clone` |
| Quiz | `fa-solid fa-list-check` |
| Match | `fa-solid fa-puzzle-piece` |
| Write | `fa-solid fa-keyboard` |
| Suggested | `fa-solid fa-wand-magic-sparkles` |

Decorative icons get `aria-hidden="true"`. Icon-only buttons use `.tct-btn-icon` with an `aria-label`.

### 4.5 Visual system (DESIGN.md + `tct-learning-ui.css`)

- **Page wrapper:** `<div class="tct-theme tct-page ts-page ts-page--{list|detail|compose|word|import|shared|form}">`. Use `.tct-page--narrow` on create/settings/edit-card forms.
- **Headings:** `_PageHeader` with `PageHeaderViewModel` for breadcrumb + the single `<h1>` + description + navigation actions. POST actions are written in page markup, never inside the partial.
- **States:** `_StateMessage` for empty/loading/error/success blocks. A POST action next to a state message (for example "Thêm bộ thẻ mẫu") is a form in page markup.
- **Components:**
  - Cards: `.tct-card` / `.tct-card--interactive`.
  - Status: `.tct-badge--*`.
  - Search/filter bar: `.tct-toolbar`.
  - Tables: `.tct-table-wrap` + `.tct-table` for the list view.
  - Loading: `.tct-skeleton*`.
  - IPA: `.tct-phonetic`.
  - Text: `.tct-break` and `.tct-clamp-2` for long text.
  - Bootstrap modal / offcanvas / toast / dropdown inside the `tct-theme` subtree.
- **Cover colors.** Use the six existing token pairs; no new hex:

| Hue key | Background | Text / glyph | Progress fill |
| --- | --- | --- | --- |
| `primary` | `--tct-primary-soft` | `--tct-primary-strong` | `--tct-primary` |
| `success` | `--tct-success-soft` | `--tct-success-text` | `--tct-success` |
| `warning` | `--tct-warning-soft` | `--tct-warning-text` | `--tct-warning` |
| `danger` | `--tct-danger-soft` | `--tct-danger-text` | `--tct-danger` |
| `info` | `--tct-info-soft` | `--tct-info-text` | `--tct-info` |
| `accent` | `--tct-accent` | `--tct-on-accent` | `--tct-on-accent` |

  Pick the hue with a stable hash in C#: `SetCoverColor.For(setId)`, using `h = (h*31 + c) mod 2^32` over `setId.ToString()`, then index `h % 6`. Render it as class `ts-hue-{key}`. If the agent judges six hues insufficient, write the token proposal (D3) and keep six.
- **Type:**
  - `<h1>` comes from the partial (`--tct-title-size`).
  - Hero term: `--tct-text-3xl`, weight `--tct-weight-bold` (700, never 800).
  - Body: `--tct-text-base`. Secondary text: `--tct-text-sm`. Captions and badges: `--tct-text-xs`.
  - Set `font-family` on the element itself if needed: `layout.css` sets it on `*`.
- **Shape and spacing:**
  - Radius: `--tct-radius-lg` for cards and panels, `--tct-radius-md` for controls, `--tct-radius-xl` for dialogs and the detail hero, `--tct-radius-pill` for chips and badges.
  - Spacing: `--tct-space-*`. Shadow: `--tct-shadow-soft`, `--tct-shadow-lift` on hover.
- **Size:** controls `--tct-control-h`; icon-only targets `--tct-target-min` (44px).
- **Layers:** fixed bulk bar and mobile FAB use `--tct-z-sidebar` (900, below the header). Keep the FAB clear of the AI chat launcher: check `ai-chat-launcher.css` for its real bottom/right offsets and place the FAB above it.
- **Desktop offset:** use `--tct-sidebar-w` / `--tct-sidebar-w-md`. Do not hardcode 280/240.
- **Motion:** `--tct-dur-fast`/`--tct-dur` + `--tct-ease` only. These already collapse under `prefers-reduced-motion`; custom keyframes need their own reduced-motion guard.
- **Focus:** keep the shared `:focus-visible` ring; inside the dark bulk bar add `.tct-on-dark`.
- **Responsive:** check at 1440 / 768 / 375–390 and 200% zoom, with no horizontal overflow. Remember the mobile root font is 14px.

---

## 5. Target architecture

### 5.1 Routes added to `SetController`

Absolute routes start with `/`, the same pattern as `BillingController` `"/Premium"`. Keep class-level `[Authorize]` + `[AutoValidateAntiforgeryToken]`. Unauthorized access returns `NotFound()`.

| Method | Route | Action | Access | Result |
| --- | --- | --- | --- | --- |
| GET | `/Sets` | `Index(string? filter)` | signed in | View `Set/Index` |
| GET | `/Home/CreateSet?folderId=` | `CreateSet` (kept) | `folderId` must be manageable (existing check) | View `Set/CreateSet` (new UI) |
| POST | `/Sets/Create` | `CreateDeck(SetInfoFormViewModel)` | folder manageable, else `NotFound()` | Redirect `/Sets/{id}/Add` |
| POST | `/Home/CreateSet` | legacy contract kept; set `Visibility = Private` explicitly | as today | as today |
| GET | `/Sets/{id:int}` | `Detail` | `CanView` | View `Set/Detail` |
| GET | `/Sets/{id:int}/Settings` | `Settings` | `CanManage` | View `Set/Settings` |
| GET | `/Home/EditSet/{id:int}` | kept; renders `Set/Settings` | `CanManage` (else 404, as tests expect) | View |
| POST | `/Sets/{id:int}/Settings` | `SaveSettings` | `CanManage` | Redirect `/Sets/{id}` + TempData "Đã lưu bộ thẻ" |
| POST | `/Home/EditSet` | legacy kept; **B1 fix** | `CanManage` | as today |
| POST | `/Sets/{id:int}/Delete` | `DeleteDeck(int id, string confirmName)` | `CanManage`; trimmed name must match | Redirect `/Sets` |
| POST | `/Home/DeleteSet`, `/Home/RemoveSetFromFolder` | kept; **B2 fix** | as today | as today |
| GET | `/Sets/{id:int}/Add?term=` | `AddCards` | `CanEdit` | View `Set/AddCards` |
| GET | `/Sets/{id:int}/Import` | `Import` | `CanEdit` | View `Set/Import` |
| GET | `/Sets/{id:int}/Export` | `Export` | `CanView` | `text/csv; charset=utf-8` with UTF-8 BOM |
| GET | `/Sets/Cards/{cardId:int}` | `CardDetail` | `CanView(card.SetId)` | View `Set/CardDetail` |
| GET/POST | `/Sets/Cards/{cardId:int}/Edit` | `EditCard` | `CanEdit` | View / redirect `/Sets/Cards/{cardId}` |
| POST | `/Sets/Cards/{cardId:int}/Delete` | `DeleteCard` | `CanEdit` | Redirect `/Sets/{setId}` |
| GET | `/s/{slug}` | `Shared(string slug)` `[AllowAnonymous]` | slug matches `^[a-z0-9]{10}$` and a set | View `Set/Shared`, else 404 |
| POST | `/Sets/Subscribe` | `Subscribe(int? setId, string? slug)` | `CanView` or valid slug; owner → no-op | Redirect `/Sets/{id}` |
| POST | `/Sets/{id:int}/Unsubscribe` | `Unsubscribe` | signed in | Redirect |
| POST | `/Sets/Clone` | `Clone(int? setId, string? slug)` | as Subscribe; ≤200 owned sets | Redirect `/Sets/{newId}` |
| POST | `/Sets/AddSamples` | `AddSamples` | signed in | Redirect `/Sets` + TempData |

### 5.2 JSON APIs

New `Controllers/SetApiController.cs`:
- Attributes: `[Authorize]`, `[AutoValidateAntiforgeryToken]`, `[Route("api/sets")]`, inherits `BaseController`.
- User id from `TryGetCurrentUserId`.
- Errors: `{ "error": "..." }` or `{ "error": "...", "fieldErrors": { ... } }`. No access → `NotFound()`.
- Follow `tct-api-endpoint`.

New `Controllers/VocabApiController.cs` (`[Route("api/vocab")]`) holds the AI endpoints.

| Method | Route | Input | Output | Access |
| --- | --- | --- | --- | --- |
| GET | `/api/sets/{id}/cards` | `q`≤80, `topic`≤60, `pos`≤20, `status` (`new`/`learning`/`learned`/`hard`/`suspended`), `level` A1..C2, `sort` (`newest` default /`az`/`hard`), `offset`≥0 | `{ rows: CardRowDto[], nextOffset: int?, total: int }`, page size 50 | `CanView` |
| POST | `/api/sets/{id}/cards/bulk` | `{ action: move/topic/suspend/delete, cardIds: int[1..500], targetSetId?, topic?(≤60, null clears), suspended? }` | `{ count, skipped? }` | see below |
| POST | `/api/sets/{id}/cards/batch` | `{ cards: CardInputDto[1..30] }` | `{ inserted:[{id,term}], skipped:[term], invalid:[term] }` | `CanEdit` |
| GET | `/api/sets/duplicates?term=&excludeCardId=` | term ≤255 | `[{ cardId, setId, setName, term }]` ≤5 | signed in; only owned or followed sets |
| POST | `/api/sets/card-image` | multipart `file` | `{ url }` | signed in; `IFileStorageService` + `ImageUploadPolicies.CardImage` |
| POST | `/api/sets/cards/{cardId}/suspend` | `{ suspended }` | `{ suspended }` | `CanView` (per-viewer) |
| POST | `/api/sets/{id}/share-link` | `{ enabled, regenerate? }` | `{ slug?, url? }` (absolute URL built server-side) | `CanManage` |
| POST | `/api/vocab/enrich` | `{ term, meaningHint?, context? }` | `VocabEnrichResultDto` | Premium/Admin, else `403` `{ error, premiumRequired: true }` |
| POST | `/api/vocab/enrich-batch` | `{ items: [{ term, meaningHint? }] }` 1..10 | `{ results: [{ term, ok, result?, error? }] }` | Premium/Admin |
| GET | `/api/vocab/images?q=` | q ≤60 | `[{ previewUrl, webformatUrl, pageUrl, author }]` ≤8 | Premium/Admin |

Bulk rules:
- `move`: `CanEdit` on both sets; `targetSetId != id`.
  - Terms that already exist in the target (trimmed, case-insensitive) are skipped and counted in `skipped`.
  - The UI message is "Bộ {tên} đã có {n} từ trong số này".
  - Progress is kept (it is keyed by `CardId`).
- `topic` / `delete`: `CanEdit`.
- `suspend`: `CanView`, applied to the caller's own progress.
- Ids that do not belong to `SetId == id` are ignored silently. `count == 0` for move/topic/delete → `NotFound` with `{ error: "Không có từ nào được cập nhật." }`.

### 5.3 Access rules (one implementation, reused everywhere)

Each rule is an `IQueryable<Set>` filter so it runs in SQL:

- `CanManage` = `OwnerId == userId || isAdmin`.
- `CanEdit` = `CanManage` (no "members may edit").
- `CanView` = `CanManage`
  - `|| Visibility == "Public"`
  - `|| exists SetSubscription(userId, setId)`
  - `|| (FolderId != null && (` folder owner `|| SavedFolder(userId) || ClassFolder` where the user owns the class or is a `ClassMember` `))`.
- **Superset check.** The folder/saved/class branch must be logically equal to `LearnerCardAccess.Authorized` in `CardLookupRetriever.cs`. Read that file; do not edit it. The test in P3 asserts the superset property.
- Slug access exists only at `/s/{slug}`, `Subscribe(slug)` and `Clone(slug)`.

### 5.4 Services

**Option A (D1 = CÓ)**
- `Services/Sets/IPersonalSetService.cs` + `PersonalSetService.cs` (namespace `TCTEnglish.Services.Sets`).
- Register with one line right below `AddScoped<IStudyService, StudyService>()`.
- May inject `TimeProvider` (already registered) for "now".

**Option B (D1 = KHÔNG)**
- Add a `// Personal sets` block to `IStudyService` and implement it in `Services/StudyService.PersonalSets.cs` (`partial class StudyService`).
- Controllers inject `IStudyService`. Keep `StudyService`'s constructor unchanged unless every construction site (DI and tests) is updated. Otherwise use `DateTime.UtcNow`, as the class does today.

**Methods** (async, `CancellationToken ct = default` last):

```text
GetSetIndexAsync(userId, isAdmin, filter)                    -> SetIndexViewModel
GetSetDetailAsync(setId, userId, isAdmin)                    -> SetDetailViewModel?
GetSetInfoFormAsync(setId, userId, isAdmin)                  -> SetInfoFormViewModel?
CreateSetAsync(input, userId, isAdmin)                       -> OperationResult<int>
UpdateSetInfoAsync(setId, input, userId, isAdmin)            -> OperationResult
DeleteSetAsync(setId, userId, isAdmin)                       -> OperationResult   // progress -> cards -> subscriptions -> set
SearchCardsAsync(setId, query, userId, isAdmin)              -> CardPageDto?
BulkCardsAsync(setId, request, userId, isAdmin)              -> OperationResult<CardBulkResultDto>
CreateCardsBatchAsync(setId, cards, userId, isAdmin)         -> OperationResult<CardBatchResultDto>
FindDuplicatesAsync(term, userId, excludeCardId)             -> IReadOnlyList<DuplicateHitDto>
GetCardDetailAsync(cardId, userId, isAdmin)                  -> CardDetailViewModel?
GetCardFormAsync(cardId, userId, isAdmin)                    -> CardFormViewModel?
UpdateCardAsync(cardId, input, userId, isAdmin)              -> OperationResult
DeleteCardAsync(cardId, userId, isAdmin)                     -> OperationResult<int>  // returns setId
SetSuspendedAsync(cardId, suspended, userId, isAdmin)        -> OperationResult<bool>
SetShareLinkAsync(setId, enabled, regenerate, userId, isAdmin) -> OperationResult<string?>
GetSharedSetAsync(slug, userId?)                             -> SharedSetViewModel?
SubscribeAsync(setId?, slug?, userId, isAdmin)               -> OperationResult<int>
UnsubscribeAsync(setId, userId)                              -> OperationResult
CloneSetAsync(setId?, slug?, userId, isAdmin)                -> OperationResult<int>
AddSampleSetsAsync(userId)                                   -> OperationResult<(int sets, int cards)>
ExportCsvAsync(setId, userId, isAdmin)                       -> SetExportFile?
CanViewSetAsync(setId, userId, isAdmin)                      -> bool
GetImportContextAsync(setId, userId, isAdmin)                -> CardImportContextViewModel?
```

If `Services/OperationResult.cs` has no generic `OperationResult<T>`, add one in the same file following the existing pattern, without changing the existing API.

**AI.** Extend `IVocabSuggestService` (already registered) and keep `SuggestAsync`:

```text
EnrichAsync(term, meaningHint?, context?, ct)                -> VocabEnrichResult
EnrichBatchAsync(items, ct)                                  -> IReadOnlyList<VocabEnrichBatchItem>
SearchImagesAsync(query, max, ct)                            -> IReadOnlyList<ImageSuggestion>
```

### 5.5 ViewModels and DTOs

- Location: `ViewModels/Sets/` (namespace `TCTEnglish.ViewModels.Sets`). Views never receive EF entities.
- Files:
  - Set list: `SetIndexViewModel`, `SetCardTileViewModel`
  - Set: `SetDetailViewModel`, `SetInfoFormViewModel`, `SharedSetViewModel`
  - Card list and search: `CardRowDto`, `CardSearchQuery`, `CardPageDto`
  - Card writes: `CardBulkRequest`, `CardBulkResultDto`, `CardInputDto`, `CardBatchResultDto`
  - Card detail and form: `CardDetailViewModel`, `CardFormViewModel`, `CardDetailsJson`
  - Import and other: `CardImportContextViewModel`, `VocabEnrichResultDto`, `DuplicateHitDto`

### 5.6 Shared constants (`Services/Sets/`)

- `SetVisibility`: `Private`, `Public`, `IsValid`.
- `CardPartsOfSpeech`: code → Vietnamese label. `Parse(csv)`, `Join(codes)` (valid codes only, deduplicated, ≤6).
  - `noun` Danh từ, `verb` Động từ, `adj` Tính từ, `adv` Trạng từ, `pron` Đại từ, `prep` Giới từ
  - `conj` Liên từ, `interj` Thán từ, `det` Hạn định từ, `phrasal_verb` Cụm động từ, `idiom` Thành ngữ, `phrase` Cụm từ, `num` Số từ
- `SetTopics` (Vietnamese UI data, keep literal):
  - Gia đình, Nhà cửa, Ăn uống, Mua sắm, Du lịch, Giao thông, Công việc, Kinh doanh
  - Học tập, Công nghệ, Sức khỏe, Thể thao, Thiên nhiên, Thời tiết, Động vật, Cảm xúc
  - Tính cách, Ngoại hình, Thời gian, Xã hội, Văn hóa – Giải trí, Giao tiếp hằng ngày, Khác
- `CefrLevels`: A1..C2.
- `SetCoverColor`: section 4.5.
- `CardStatus`: key/label per section 4.2 + LINQ filter expressions.

### 5.7 Static assets

- **CSS:** `wwwroot/css/personal-sets.css`. Every selector starts with `.ts-page`. Values come only from `var(--tct-*)`. No hex, no `:root`, no element selectors outside the scope, no `!important`.
- **JS:** vanilla, one IIFE per file, configuration read from `data-*` / `<script type="application/json">`, a single namespace `window.TctSets`, rendering with `textContent`/`createElement` (no `innerHTML` with user data).

| File | Role |
| --- | --- |
| `personal-sets-common.js` | `fetchJson` (adds header `RequestVerificationToken` from `input[name="__RequestVerificationToken"]`), `toast` (Bootstrap Toast in `#tsToastHost`, optional action button), `debounce`, `speak(text)` (`speechSynthesis`, `lang='en-US'`, cancel before speaking, hide the button when unsupported), `posLabels`, `topics`, `pending(button, on)` (`disabled` + `aria-busy` + spinner) |
| `set-index.js` | List page |
| `set-detail.js` | Word list, filters, infinite scroll, selection, bulk, menu, share |
| `set-card-form.js` | Detail fields shared by the composer drawer and the edit-card page |
| `set-composer.js` | Add words (pure logic exposed as `TctSets.composeCore` for tests) |
| `set-import-parser.js` | Pure `parseImport(text, existingTermsLower)`, UMD (`module.exports` in Node, `TctSets.parseImport` in the browser) |
| `set-import.js` | Import page |

Load with `@section Styles` / `@section Scripts` and `asp-append-version="true"`.

---

## 6. Database (P1)

### 6.1 Impact assessment (print before generating; `tct-ef-migration`)

```text
## Migration Impact: AddPersonalSetWorkspace
Type: Additive
Tables affected: Sets, Cards, LearningProgress, SetSubscriptions (new)
Backward compatible: Yes (nullable or defaulted columns, new table, new indexes)
Data loss risk: None
Shared-data impact: Set/Card are exportable kinds; new properties need safe defaults (see 6.5)
```

### 6.2 Entity changes

**`Set`**:
- `string Visibility { get; set; } = "Public";` (F7)
- `string? ShareSlug`
- `int? ClonedFromSetId` (no FK)
- `DateTime? UpdatedAt`
- `ICollection<SetSubscription> Subscriptions`

**`Card`** (all optional):
- `string? PartsOfSpeech`, `string? MeaningEn`, `string? Cefr`, `string? PhoneticUk`, `string? Emoji`
- `string? PersonalNote`, `string? DetailsJson`, `string? ImageSourceJson`
- `bool AiGenerated`
- `DateTime? CreatedAt`, `DateTime? UpdatedAt`

Keep the date columns nullable. The importer's `SetCreationDates` only touches non-nullable `DateTime`; setting `CreatedAt` for imported cards is not required.

**`LearningProgress`**: `bool IsSuspended`.

**New `Models/SetSubscription.cs`** (`TCTEnglish.Models`):
- Fields: `SetSubscriptionId`, `UserId`, `SetId`, `DateTime SubscribedAt`.
- Navigation to `User` and `Set`. Prefer `.WithMany()` on the user side over adding a `User` collection.

### 6.3 `DbflashcardContext` mapping

- `DbSet<SetSubscription> SetSubscriptions`.
- **`Set`**
  - `Visibility`: `HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue("Public")`.
  - `ShareSlug`: `HasMaxLength(32).IsUnicode(false)` + unique `IX_Sets_ShareSlug` (MariaDB allows many NULLs).
  - `UpdatedAt`: `datetime`.
- **`Card`**
  - Lengths: `PartsOfSpeech` 120 (non-Unicode), `MeaningEn` 400, `Cefr` 2 (non-Unicode), `PhoneticUk` 100, `Emoji` 16, `PersonalNote` 1000, `ImageSourceJson` 1000.
  - `DetailsJson`: longtext.
  - `AiGenerated`: `HasDefaultValue(false)`.
  - Date columns: `datetime`.
  - Non-unique `IX_Cards_SetId_Term (SetId, Term)`. Term is varchar(255) utf8mb4 = 1020 bytes, under the 3072-byte limit.
- **`LearningProgress`**: `IsSuspended` `HasDefaultValue(false)`.
- **`SetSubscription`**
  - Table `SetSubscriptions`; unique `IX_SetSubscriptions_UserId_SetId`.
  - FKs to `Users` and `Sets` both **Cascade**, so account deletion and set deletion stay unblocked without editing `AccountController`.

### 6.4 Generate and verify

```powershell
dotnet tool restore
dotnet ef migrations add AddPersonalSetWorkspace --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
```

Review `Up()`/`Down()`:
- Only `AddColumn`/`CreateTable`/`CreateIndex`.
- `Down()` mirrors `Up()`.
- Default values are present (`'Public'`, `0`).
- No edits to applied migrations.
- Commit the migration, its Designer file and the snapshot together.

**MariaDB verification.** Disposable database only; never the dev DB or hosting.
1. Read `scripts/verify-mariadb.ps1`, `scripts/bootstrap-mariadb-acceptance.ps1` and the CI job in `.github/workflows/deploy.yml` to reuse the established disposable-container procedure. The container and database names must satisfy the scripts' guards.
2. Upgrade path:
   1. Update to the previous migration.
   2. Insert minimal rows: user, set, 2 cards with `Phonetic`/`Example`/`Topic`, 1 progress.
   3. Update to the latest migration.
   4. Assert: legacy columns unchanged; `Visibility='Public'`; `AiGenerated=0`; `IsSuspended=0`; cascade FKs present.
   5. Run update again (idempotent).
   6. Downgrade to the previous migration, then upgrade again; legacy data intact.
3. Run `SharedDataMariaDbTests` against the disposable database if the acceptance environment can be prepared; it is also a CI step.
4. Record the commands and their real output in the execution notes. Without Docker, write "MariaDB not verified" and why; never claim a pass.

### 6.5 Shared-data compatibility (always required, regardless of D4)

- **C# defaults:** `Visibility = "Public"`, `AiGenerated = false`, `IsSuspended = false`. Rows created by `Activator.CreateInstance` stay valid and studyable.
- **Never export or import** `Visibility`, `ShareSlug`, `ClonedFromSetId`, `PersonalNote`, `SetSubscriptions` or progress.
- **Regression test** `SharedDataPersonalSetCompatibilityTests` (in-memory or SQLite, following the existing shared-data tests):
  - An imported `Set` lands as `Public` with `ShareSlug == null`.
  - Exporting a set whose card has `PersonalNote` does not leak the note into the package.
- **If D4 = CÓ:**
  - Append `PartsOfSpeech MeaningEn Cefr PhoneticUk Emoji DetailsJson ImageSourceJson` to the `Card` spec.
  - Add to `AssertShareable`: `Set { Visibility: "Private" }` → `SharedDataException("Private learner sets cannot be exported as shared content.")`.
  - Verify older package records without the new fields still import. Read how `SetValues` handles a missing key (`record.Values[field]`). If a missing key throws, make missing new fields optional in the narrowest possible way and test it, or stop and report.
  - Run the full shared-data test set.

### 6.6 `CardDetailsJson`

`System.Text.Json`, camelCase, nulls omitted.

| Field | Shape | Max items |
| --- | --- | --- |
| `examples` | `[{ text ≤300, vi ≤300 }]` | 6 |
| `synonyms` | `[≤80]` | 10 |
| `antonyms` | `[≤80]` | 10 |
| `collocations` | `[{ text ≤120, vi ≤200 }]` | 8 |
| `wordFamily` | `[{ text ≤80, pos, vi ≤200 }]` | 8 |
| `verbForms` | `{ past, pastParticiple, ing, thirdPerson }` | — |
| `plural` | `≤80` | — |

- **Mirroring:**
  - On save, `Card.Example`/`ExampleTranslation` = first example, or null.
  - On read, a legacy card that has only `Example` is shown as one example.
- **Bad JSON** → treated as empty, logged as a warning, never a page error.
- **Rendering:** JSON reaches the page only through Razor encoding or `JsonSerializer.Serialize` into `data-*` / `<script type="application/json">`. Never `Html.Raw` with user data.

---

## 7. Phases

Every phase follows the same loop:
1. Load only the needed skill.
2. Implement.
3. `dotnet build TCTEnglish/TCTEnglish.csproj`.
4. Run the relevant tests.
5. Self-review the diff against AGENTS.md.
6. Tick the checklist.
7. Commit locally **only if the owner granted commit** (see the prompt); otherwise leave the changes uncommitted.

Keep a task list.

### P0 — Baseline

1. Run `git status`, branch and HEAD. If the owner authorized a pull: `pwsh -NoProfile -File scripts/pull-local.ps1` (exit code 0 and fresh `.appdata/local/shared-data/last-sync.json` required). Create `feature/personal-sets-redesign` from current `master`.
2. Run `node scripts/check-local.cjs`. Start the web only when needed: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` (MariaDB + web; no local speech engine). Use a free port if one is taken. Never reset the dev DB.
3. Build. Record the baseline counts of:
   `dotnet test TCTEnglish.Tests --filter "FullyQualifiedName~FolderSetIdor|FullyQualifiedName~Sprint2|FullyQualifiedName~Sprint3|FullyQualifiedName~SharedData|FullyQualifiedName~AiRag|FullyQualifiedName~AiPhase"`.
4. Confirm B1–B7 in source. Record line references.
5. Read (do not edit) every Set/Card reader:
   - `Services/AI/Internal/Retrievers/*`, `GroundingCitationService.cs`
   - `HomeController`, `StudyService*.cs`, `ClassService`
   - `AccountController` deletion block
   - `Areas/Admin/Controllers/SetManagementController.cs`, `UserManagementController`, `DashboardController`
   - `LearningApiController`, `Infrastructure/SharedData/*`, `LocalDevelopmentBootstrap`, both seeders

   Record what each reads.
6. Save this plan to `docs/superpowers/plans/2026-11-09-personal-sets-redesign.md` and create the execution-notes section at its end.

**Done when:** baseline numbers recorded, defects confirmed, plan saved.

### P1 — Schema and migration

Section 6 in full.

**Tests:**
- `PersonalSetsSchemaTests`: model contains `SetSubscription`; unique `(UserId, SetId)`; unique `ShareSlug`; `Visibility` default `Public`; C# default `Public`.
- `SharedDataPersonalSetCompatibilityTests` (6.5).

**Done when:** no pending model changes; build and baseline tests green; MariaDB evidence recorded (or the reason it could not be run).

### P2 — Constants, DTOs, ViewModels, normalizer

1. Create the files listed in 5.5 and 5.6.
2. `SetInfoFormViewModel` fields:
   - `SetId?`
   - `SetName` `[Required(ErrorMessage="Nhập tên bộ thẻ")] [StringLength(255)]`
   - `Description` `[StringLength(500)]`
   - `Visibility`
   - `FolderId?`
   - `AvailableFolders` (owned folders)
   - `WordCount`
3. `CardInputDto` / `CardFormViewModel` constraints:

| Field | Rule |
| --- | --- |
| `Term` | required, trimmed, ≤255 |
| `Definition` | required, trimmed, ≤1000 |
| `Phonetic`, `PhoneticUk` | ≤100 |
| `MeaningEn` | ≤400 |
| `PartsOfSpeech` | valid codes, ≤6 |
| `Topic` | ≤60 |
| `Emoji` | exactly one grapheme (`StringInfo.LengthInTextElements == 1`), otherwise dropped silently |
| `Cefr` | empty or A1..C2 |
| `PersonalNote` | ≤1000 |
| `ImageUrl` | null, internal `/images/cards/…` or `/uploads/shared/…`, or `https://` on a Pixabay host (P4.5) |
| `ImageSource` | `{ provider: pixabay/upload, pageUrl?, author? }` |
| `Examples`, `Synonyms`, `Antonyms`, `Collocations`, `WordFamily`, `VerbForms`, `Plural` | limits in 6.6 |
| `AiGenerated` | bool |

4. Write `CardInputNormalizer.Normalize(input) → (CardData, Dictionary<string,string> fieldErrors)`:
   - Trim every string; empty strings become null.
   - Drop empty list items; enforce the limits.
   - Vietnamese messages (literal UI copy): "Nhập từ", "Nhập nghĩa tiếng Việt", "Tối đa {n} ký tự", "Ảnh không hợp lệ".

**Tests** (`CardInputNormalizerTests`): missing term or definition; list clamping; multi-grapheme emoji dropped; invalid CEFR; unknown POS filtered; disallowed image URL rejected.

### P3 — Read side and access

Apply D1 (option A or B).
- All async with `CancellationToken`. No `.Result`/`.Wait()`.
- Reads use `AsNoTracking` + `Select` projections.
- Access helpers: `ViewableSets(userId, isAdmin)` and `ManageableSets(userId, isAdmin)` return `IQueryable<Set>`.

**3.1 `GetSetIndexAsync`** — three groups:
- **own**: `OwnerId == userId`.
- **subscribed**: has a `SetSubscription`, not owned.
- **shared**: sets in the user's saved folders or in class folders of classes the user owns or belongs to. Exclude owned and subscribed sets. Cap 60, newest `SetId` first.

Each tile (`SetCardTileViewModel`):
- `SetId`, `SetName`, `OwnerName` (when not own), `Visibility`, `HasShareLink`, `CoverHue`
- `SampleTerm`: term of the lowest `CardId`, ≤24 characters
- `WordCount`, `DueCount`, `NewCount`, `ProgressPercent`
- `Relation`: `own` / `subscribed` / `shared`

Compute `DueCount` and `NewCount` with **one** grouped query over the user's progress joined to cards of the listed set ids. No per-set queries. `filter` is `null` / `own` / `subscribed` / `shared`.

**3.2 `GetSetDetailAsync`** returns:
- Flags: `IsOwner`, `CanEdit`, `CanManage`, `IsSubscribed`, `IsStudying` (own, subscribed or shared via folder/class).
- `FolderId`/`FolderName`, `DueCount`, `WordCount`, `MasteredPercent`.
- `Topics`: distinct non-null, A–Z.
- `MoveTargets`: own sets other than this one, ≤200, sorted by name.
- `ShareUrl`: built in the controller with `Url.Action(..., protocol: Request.Scheme)`.
- `StudyStrip`: see P5.3.
- Do **not** touch `ViewCount` (it belongs to system vocabulary).

**3.3 `SearchCardsAsync`**:

```text
q:      Term/Definition/Phonetic LIKE %q% (EF.Functions.Like, escape % _ \)
topic:  Card.Topic == topic
pos:    ("," + PartsOfSpeech + ",") LIKE "%,pos,%"   (valid code only)
level:  Cefr == level
status: section 4.2 via left join on the caller's progress
sort:   newest -> CreatedAt desc (nulls last), CardId desc
        az     -> Term asc, CardId asc
        hard   -> WrongCount desc (null = 0), LastReviewedDate desc, CardId desc
paging: Skip(offset).Take(51) -> 50 rows + has-more flag
```

- `total` is counted only when filters are active; otherwise it equals `WordCount`.
- `CardRowDto`: `cardId, term, phonetic, definition, cefr, topic, imageUrl, emoji, status{key,label}, isSuspended`.

**3.4 `GetCardDetailAsync`**:
- Card + set (name, id, `CanEdit`) + parsed details.
- Caller's progress: status, `LastReviewedDate`, `NextReviewDate`, `WrongCount`, `RepetitionCount`, `IsSuspended`.
- `RelatedExisting`: synonyms/antonyms already present in the same set → card ids (trimmed, case-insensitive match).

**3.5 `FindDuplicatesAsync`**: trimmed, case-insensitive term match within owned and followed sets; exclude `excludeCardId`; ≤5 results.

**3.6 `GetSharedSetAsync`**:
- Set + `OwnerName` + `WordCount` + `LearnerCount` (subscriptions).
- First 50 cards (term, phonetic, definition) ordered by `CardId`.
- With a signed-in user: `IsOwner`, `IsSubscribed`.

**3.7 `ExportCsvAsync`**:
- Header: `term,phonetic,meaning_vi,meaning_en,parts_of_speech,topic,example,example_vi,synonyms,antonyms,cefr,note`.
- RFC 4180 escaping. Cells starting with `=`, `+`, `-` or `@` get a leading `'`.
- UTF-8 BOM.
- File name: set name with `[^\p{L}\p{N} _-]` stripped, fallback `bo-the.csv`. Send both `filename` (ASCII) and `filename*` (UTF-8).
- `note` (`PersonalNote`) is exported **only when the caller owns the set**; otherwise the column is empty.

**Tests** (`PersonalSetsReadIntegrationTests`, using the existing factory/helpers/`TestDataIds`; seed extra rows inside the test):
- Owner gets 200 on `/Sets` and `/Sets/{UserSetId}`.
- Outsider gets 404 on a Private set at `/Sets/{id}`, `/api/sets/{id}/cards`, `/Sets/{id}/Export`, `/Sets/Cards/{cardId}`.
- Outsider sees a Public set but no edit or settings controls.
- A subscriber can view a Private set.
- Filters `status=learned`, `q`, `pos` and `sort=az` return the right rows in the right order.
- Export: correct header, BOM present, `=1+1` escaped, `note` empty for non-owners.
- **Chat superset:** for each branch of `LearnerCardAccess.Authorized` (owner, folder owner, saved folder, class owner, class member), seed a Private set and assert `CanViewSetAsync == true` and `GET /Home/Study/{id}` == 200.

### P4 — Write side and legacy fixes

**4.1 `CreateSetAsync`**:
- Validate input. The folder must be manageable, else `NotFound`.
- `Visibility` defaults to `Private`. Set `CreatedAt = UpdatedAt = now`.
- Cap 200 owned sets: "Bạn đã có quá nhiều bộ thẻ (tối đa 200)."

**4.2 `UpdateSetInfoAsync`**: name, description, visibility, folder (manageable or null); `UpdatedAt`.

**4.3 `CreateCardsBatchAsync` (≤30)**:
- Normalize each card. Invalid cards go to `invalid`.
- In-batch duplicates (trim + lower): keep the first, the rest go to `skipped`. Cards already in the set go to `skipped`.
- Insert the rest with `CreatedAt`. Images follow 4.5.
- One `SaveChangesAsync`. Update `set.UpdatedAt`.

**4.4 Legacy fixes**

**B1** — rewrite the card update inside `POST /Home/EditSet` as a non-destructive upsert:
1. Load existing cards ordered by `CardId`.
2. Pair them by position with the valid submitted rows.
3. Paired cards: update `Term`, `Definition` and `ImageUrl` only when a new file or a different `ExistingImageUrl` arrives. Keep `Phonetic`, `Example`, `Topic`, the new columns and progress.
4. Surplus old cards: delete their progress, then the cards.
5. New rows: insert with `CreatedAt`.

Keep the legacy redirects and messages (e.g. "Vui lòng nhập tiêu đề học phần."). Also set `Visibility = Private` explicitly in legacy `POST /Home/CreateSet`.

**B2** — `DeleteSet`, `RemoveSetFromFolder`, `DeleteSetAsync`, `DeleteCardAsync` and bulk delete always delete in this order, inside one transaction (`BeginTransactionAsync`): progress (all users) → cards → subscriptions (when deleting a set) → set.

**Tests** (`PersonalSetsWriteIntegrationTests`; do not edit existing tests unless they contradict a contract):
- `EditSet_Legacy_KeepsLearningProgressAndPhonetic`
- `DeleteSet_WithProgress_DoesNotThrow_AndRemovesProgress`
- `CreateCardsBatch_SkipsDuplicates_AndRejectsOver30`
- `CreateDeck_IntoOthersFolder_ReturnsNotFound`
- `DeleteDeck_RequiresExactName`
- `LegacyCreateSet_IsPrivate`
- `AccountDeletion_WithSubscriptionsOnBothSides_Succeeds` — drive the existing account deletion endpoint as the existing account tests do. Do not edit `AccountController`.
- Every new POST without an antiforgery token → 400 with data unchanged (pattern: `SetMutations_RejectMissingAntiforgeryToken_AndLeaveDataUnchanged`).

**4.5 Images (B3)** — move the download into the service:
- Accept only `https://` where `Uri.Host` equals `pixabay.com` or ends with `.pixabay.com` (no `Contains`).
- 10 s timeout. Bounded stream read of 5 MB.
- `Content-Type` must be image/jpeg, png, gif or webp. The file extension is taken from the content type.
- Save to `wwwroot/images/cards/{guid}{ext}` via `IWebHostEnvironment.WebRootPath`.
- On failure: store no external URL, return null, log a warning.
- User uploads go to `POST /api/sets/card-image` → `IFileStorageService.SaveImageAsync(file, ImageUploadPolicies.CardImage)`.
- Orphaned uploads are accepted for now (risk register).

**4.6 Bulk, suspend, share, follow, clone, samples**
- **Bulk:** per 5.2.
  - Suspend creates progress when it is missing: `{ Status=null, WrongCount=0, RepetitionCount=0, IsSuspended, LastReviewedDate=null }`.
  - Check whether the `LastReviewedDate` column has a database default. If the DB fills it anyway, the card page shows "chưa ôn" when `RepetitionCount == 0 && Status == null`.
- **Share link:**
  - Disable → slug null.
  - Enable (or regenerate) → 10 characters from `abcdefghijkmnpqrstuvwxyz23456789` using `RandomNumberGenerator`; retry ≤5 times on a unique collision.
- **Subscribe:** resolve the set by id (`CanView`) or slug. Owner → no-op. Idempotent; a unique-index race counts as success.
- **Clone:**
  - Resolve as Subscribe. Cap 200 owned sets.
  - New set: name (append " (bản sao)" if the user already owns that name), description, `Private`, no folder, `ClonedFromSetId`.
  - Copy every card content column except `PersonalNote` when the cloner is not the owner. No progress. One transaction.
- **Samples:**
  - Source sets: those owned by `system@tct.local`. If that user is missing → "Chưa có bộ thẻ mẫu."
  - Pick the 2 system sets with the most cards (≥5).
  - Skip any sample the user already cloned (`ClonedFromSetId`). Clone the rest. Return `(sets, cards)`.

**Tests:**
- `Subscribe_ByDisabledSlug_ReturnsNotFound`
- `Subscribe_Twice_IsIdempotent`
- `Clone_CopiesContentFields_NotProgress_NotForeignNotes`
- `ShareLink_Regenerate_OldSlug404`
- `BulkMove_ToOthersSet_ReturnsNotFound`
- `BulkSuspend_IsPerUser`
- `AddSamples_IsIdempotent`
- `BulkDelete_CardCitedByChat_StillPassesAiCitationTests` — run the `AiRag*` suite after the write changes; no new AI code.

### P5 — List, create, settings, detail (UI)

Load `tct-ui-component`. Open `docs/design-system/preview.html` and pick components before writing markup.

**5.1 `Views/Set/Index.cshtml` (`/Sets`)**

```text
div.tct-theme.tct-page.ts-page.ts-page--list
  _PageHeader: Title "Bộ thẻ của tôi"; Actions: [PageHeaderAction("Tạo bộ thẻ", /Home/CreateSet, Primary, "fa-solid fa-plus")]
  nav.ts-chips aria-label="Lọc bộ thẻ": links Tất cả | Của tôi | Đang theo học | Được chia sẻ (/Sets?filter=…, aria-current="page" on active)
  TempData -> Bootstrap .alert-success, dismissible
  empty -> _StateMessage(Empty, "Chưa có bộ thẻ nào.", "Thêm vài bộ thẻ mẫu để thử ngay, hoặc tự tạo bộ thẻ đầu tiên.")
           + page-markup form POST /Sets/AddSamples (btn-primary "Thêm bộ thẻ mẫu") + link btn-outline-primary "Tạo bộ thẻ"
  per non-empty group: section aria-labelledby; h2 + count (.tct-text-muted); ul.ts-grid (1 col <576, 2 cols >=576, 3 cols >=1200, gap var(--tct-space-md))
  a.ts-fab (<992px only): .tct-btn-icon round 56px, aria-label "Tạo bộ thẻ"
```

`_SetTile.cshtml`:

```text
article.tct-card.tct-card--interactive.ts-tile (position:relative)
  a.ts-tile-link (stretched; covers the tile)
    div.ts-tile-art.ts-hue-{CoverHue} (84px; soft bg, strong text): SampleTerm or first letter, var(--tct-text-3xl), weight 700, single line ellipsis
       span.ts-tile-vis (corner chip): fa-lock "Riêng tư" | fa-globe "Công khai" | fa-link "Chia sẻ bằng link" | fa-users "Được chia sẻ" (visually-hidden text)
    div.ts-tile-body: name (.tct-clamp-2, weight 600); meta row: "{n} từ" (+ owner/class name) and badge:
       DueCount>0 -> .tct-badge--danger "{n} cần ôn"; WordCount==0 -> .tct-badge "Chưa có từ"; else .tct-badge--success "Đã ôn xong"
       progress bar (8px, var(--tct-surface-muted) track, hue fill) width via style="--p:NN%" (the only allowed inline style)
       span.visually-hidden "{p}% đã học"
  a.ts-tile-quick (sibling of the link, absolute, z above link; only @media (hover:hover) and DueCount>0): btn-primary btn-sm "Ôn" -> /Home/Study/{id}?mode=due, aria-label "Ôn bộ {tên}"
```

**5.2 Create (`Set/CreateSet.cshtml`) and settings (`Set/Settings.cshtml`)** share `_SetInfoForm.cshtml`.

```text
div.tct-theme.tct-page.tct-page--narrow.ts-page.ts-page--form
  _PageHeader: Title "Tạo bộ thẻ" | "Cài đặt bộ thẻ"; Breadcrumbs: Bộ thẻ (/Sets) [› Thư mục] [› Tên bộ] › current
  div.ts-form-layout (1 col; >=768: form + 112px cover preview, sticky top calc(var(--tct-header-h) + var(--tct-space-md)))
  form (Tag Helpers, antiforgery):
    asp-validation-summary ModelOnly
    .form-label "Tên bộ thẻ" + .form-control (autofocus on create; placeholder "Ví dụ: Tiếng Anh giao tiếp văn phòng") + .invalid-feedback
    .form-label "Mô tả (không bắt buộc)" + textarea rows=2
    fieldset legend "Ai được xem": two .btn-check radios + label.ts-choice
        Riêng tư — "Chỉ mình bạn. Bạn vẫn có thể bật link chia sẻ sau."
        Công khai — "Ai có đường dẫn trong hệ thống đều xem và học được."
    .form-select "Thư mục (không bắt buộc)": "Không xếp vào thư mục" + owned folders
    btn-primary "Tạo bộ thẻ" | "Lưu bộ thẻ" with the pending pattern (disabled + aria-busy + spinner, text "Đang lưu…")
  create page only: .form-text "Muốn nhập nhiều từ từ file? Tạo bộ thẻ trước, sau đó chọn Nhập hàng loạt."
```

- The create page must still render an antiforgery token (legacy tests read it from this page).
- The settings page:
  - Must render the set name (asserted by `OwnerCanStillRenameFolder_AndReadWriteEditSet_OnLegacyHomeRoutes`).
  - Ends with a "Xóa bộ thẻ" section (`.tct-card` with a `--tct-danger` border) containing "Xóa {WordCount} từ và tiến độ ôn của mọi người đang học bộ này. Không hoàn tác được." and a `btn-outline-danger` that opens a Bootstrap modal:
    - Title: "Xóa bộ “{tên}”?"
    - Input `confirmName` with a visually-hidden label.
    - Buttons: "Giữ lại" and danger "Xóa vĩnh viễn". The danger button is enabled only on a trimmed match (JS); the server re-checks.

**5.3 Detail (`Set/Detail.cshtml`)**

```text
div.tct-theme.tct-page.ts-page.ts-page--detail
  section.ts-hero.ts-hue-{hue} (radius xl, padding lg; flex wrap)
     div.ts-hero-art aria-hidden (112x96 mobile / 176x128 desktop; surface 70% via color-mix with var tokens or a surface overlay class; SampleTerm, 700)
     div.ts-hero-main
        _PageHeader: Title = set name; Breadcrumbs Bộ thẻ › [Thư mục ›] name; Description = description
        p.ts-hero-meta: "{n} từ" · "của {Owner}" · "Thư mục {Folder}" · DueCount>0 -> span.ts-due "{n} cần ôn" (danger-text)
        div.ts-hero-actions (wrap, gap sm):
           IsStudying ? a.btn.btn-primary "Ôn bộ này ({Due})" -> /Home/Study/{id}?mode=due
                      : form POST /Sets/Subscribe -> btn-primary "Theo học"
           CanEdit -> a.btn.btn-outline-primary "Thêm từ" -> /Sets/{id}/Add
           partial _SetActionsMenu
  WordCount>0 -> section h2 "Học bộ này" + nav.ts-study-strip (2 cols mobile, 4 cols >=576)
  section.ts-words aria-label="Danh sách từ"
  CanEdit -> a.ts-fab aria-label "Thêm từ" (<992px)
  CanManage -> partial _ShareSetModal
  div#tsToastHost.toast-container (aria-live polite)
  script#tsDetailConfig type=application/json
```

**Color-mix fallback.** If `color-mix()` is judged a risk for the supported browsers, use a plain `var(--tct-surface)` panel with `opacity`-free borders instead. Do not introduce rgba hex.

**`_SetActionsMenu.cshtml`** (Bootstrap dropdown; trigger `.tct-btn-icon` `fa-ellipsis`, `aria-label` "Thêm tùy chọn cho bộ thẻ", `dropdown-menu-end`):
1. [CanEdit] "Nhập hàng loạt" → `/Sets/{id}/Import`
2. "Luyện trắc nghiệm" → `/Home/QuizMode/{id}`
3. [CanManage] "Chia sẻ" → opens `#tsShareModal`
4. "Xuất CSV" → `/Sets/{id}/Export`
5. [!IsOwner] "Sao chép về bộ của tôi" (form POST `/Sets/Clone`)
6. divider
7. [CanManage] "Cài đặt bộ thẻ" → `/Sets/{id}/Settings`
8. [IsSubscribed] "Bỏ theo học" (form POST `/Sets/{id}/Unsubscribe`)

Form items are `<button type="submit" class="dropdown-item">` inside a form with an antiforgery token.

**Study strip** — each tile is an `<a class="tct-card tct-card--interactive">` with an icon block on its hue's soft background, the mode name (600) and one line of **real** data:

| Mode | Hue | Data line |
| --- | --- | --- |
| Thẻ ghi nhớ | info | "Đã thuộc {MasteredPercent}%" (server) |
| Trắc nghiệm | primary | localStorage `tct:set-stats:{setId}.quizPercent` → "Lần trước {x}%", else "{min(WordCount,20)} câu" |
| Ghép thẻ | warning | `matchMs` → "Kỷ lục {m:ss}", else "Chưa có kỷ lục" |
| Viết | success | "{WordCount} từ" |

- Read localStorage inside try/catch. Until the read finishes, show no number; keep a `min-height`.
- One tile carries `.tct-badge--primary` "Gợi ý cho bạn". Rule, first match wins:
  1. Due > 0 → Thẻ ghi nhớ
  2. Mastered < 30% → Thẻ ghi nhớ
  3. No `quizPercent` → Trắc nghiệm
  4. `quizPercent` < 80 → Viết
  5. Otherwise → Ghép thẻ

**Word list** — the server renders the shell + skeleton; JS renders rows from the API.

```text
section.tct-toolbar
  __search: input type=search aria-label "Tìm trong bộ thẻ", placeholder "Tìm từ, nghĩa hoặc phiên âm…"
  __filters: .form-select (each with visually-hidden label):
     Sắp xếp: Mới thêm | A–Z | Hay quên nhất
     Trạng thái: Mọi trạng thái | Mới | Đang học | Đã thuộc | Hay quên | Đang tạm ẩn
     Chủ đề: Mọi chủ đề | topics
     Loại từ: Mọi loại từ | POS
     CEFR: Mọi CEFR | A1..C2
     active filter -> .ts-filter--active (primary-soft bg, border-control -> primary border)
  __actions: [CanEdit && rows] button "Chọn"/"Xong" (aria-pressed); view toggle group aria-label "Kiểu hiển thị": .tct-btn-icon fa-list "Dạng danh sách" / fa-table-cells-large "Dạng lưới có ảnh" (aria-pressed; localStorage "tct:set-view" in try/catch)
p aria-live=polite: "Đang tải…" | "{n} kết quả" (+ "+" when more) | "{WordCount} từ"
list view: div.tct-table-wrap > table.tct-table (>=768) — columns [select] Từ | Phiên âm (.tct-phonetic) | Nghĩa | CEFR | Trạng thái | speak button
           <768: stacked rows (term + IPA on one line, meaning below with .tct-clamp-2, badge right)
           row link -> /Sets/Cards/{id}; speak button outside the link, .tct-btn-icon aria-label "Phát âm {term}"
           suspended -> term struck through + .tct-text-muted
           select mode -> leading checkbox (real <input type=checkbox> with label "Chọn {term}")
grid view: ul.ts-word-grid (2 cols; 3 cols >=576): square image (alt="") or emoji / initial on var(--tct-surface-muted); badge top-right; checkbox top-left in select mode; term + meaning below
first load: .tct-skeleton-group with 6 .tct-skeleton--line
empty + filters -> _StateMessage-like block (JS-rendered, same classes) "Không có từ nào khớp." / "Thử bỏ bớt bộ lọc."
empty + none -> "Bộ thẻ này chưa có từ nào." + [CanEdit] link "Thêm từ đầu tiên"
sentinel (IntersectionObserver rootMargin 400px) + "Đang tải thêm…"
load error -> .alert-danger role=alert "Không tải được danh sách từ." + button "Thử lại"
```

**Bulk bar** (`div role="toolbar" aria-label="Thao tác với từ đã chọn"`):
- Fixed at the bottom, centered in the main column, max-width 640px.
- Background `var(--tct-text)`, white text, `.tct-on-dark`, `var(--tct-radius-lg)`, `var(--tct-shadow-overlay)`.
- Left offset uses `--tct-sidebar-w` / `-md`; `left: 0` under the 768px drawer breakpoint.
- Content: "Đã chọn {n}" · [move targets] select "Chuyển bộ…" · select "Chủ đề…" ("Bỏ chủ đề" + topics) · "Tạm ẩn" / "Bỏ ẩn" (Bỏ ẩn when filtering `suspended`) · danger "Xóa" · `.tct-btn-icon` `fa-xmark` "Bỏ chọn".
- While pending, all buttons are disabled.
- Delete opens a Bootstrap modal: "Xóa {n} từ?" / "Tiến độ ôn của mọi người với các từ này cũng bị xóa. Không hoàn tác được." / "Giữ lại" | "Xóa từ".
- Success:
  - Toast "{Đã chuyển|Đã đổi chủ đề|Đã tạm ẩn|Đã bỏ ẩn|Đã xóa} {count} từ".
  - Leave select mode and reload from offset 0, keeping the filters.
- Error: danger toast with the server's `error`.

**`set-detail.js`:**
- Search debounce 300 ms.
- Any filter change resets the offset and aborts the in-flight request (`AbortController` + request token, so a stale response never overwrites).
- `Escape` leaves select mode.

**`_ShareSetModal.cshtml`:**
- Title: "Chia sẻ bộ thẻ".
- Text: "Người có link xem được danh sách từ, rồi có thể theo học trực tiếp hoặc sao chép về bộ của họ."
- `.form-switch` "Chia sẻ bằng link" → `share-link`. Disabled while pending; reverts on failure and shows a toast.
- With a link:
  - Readonly input (`aria-label` "Link chia sẻ") + button "Sao chép".
  - Use `navigator.share` on small touch screens, otherwise `navigator.clipboard.writeText`. Show `fa-check` for 2 s and toast "Đã sao chép link".
  - Link button "Tạo link mới (link cũ sẽ không dùng được)".

### P6 — AI enrich (`VocabSuggestService`)

**`EnrichAsync`**:
1. Trim and cap at 100 characters. Empty → "Nhập từ trước khi gợi ý."
2. Prompt the model (application prompt; keep it in English as the existing service does) with these rules:
   - Role: English–Vietnamese dictionary for Vietnamese office workers.
   - Vietnamese meaning: natural, common, 1–6 words, comma-separated senses.
   - Examples: ≤15 words, everyday/workplace register, a natural Vietnamese translation, no sensitive content.
   - `meaningHint` given → the primary sense must match it and the user's meaning is never replaced.
   - `context` given → pick the sense used in that sentence.
   - Not a real word → `is_valid_word=false` + `did_you_mean`.
   - `topic_vi` must be one of `SetTopics` (list them all), else "Khác".
   - `image_query`: 1–3 concrete English words.
   - JSON only:

```json
{ "is_valid_word": true, "did_you_mean": null, "lemma": "run", "ipa_us": "/rʌn/", "ipa_uk": null, "cefr": "A1",
  "senses": [ { "pos": ["verb"], "meaning_vi": "chạy", "meaning_en": "move fast on foot",
                "examples": [ { "text": "I run every morning.", "vi": "Tôi chạy mỗi sáng." } ] } ],
  "synonyms": [], "antonyms": [], "collocations": [ { "text": "run a meeting", "vi": "điều hành cuộc họp" } ],
  "word_family": [ { "text": "runner", "pos": "noun", "vi": "người chạy" } ],
  "verb_forms": { "past": "ran", "past_participle": "run", "ing": "running", "third_person": "runs" },
  "plural": null, "topic_vi": "Thể thao", "emoji": "🏃", "image_query": "person running", "memory_tip_vi": null }
```

   Limits: senses 1–3, each with 2–3 examples; synonyms ≤6; antonyms ≤4; collocations ≤5; word family ≤6.
3. Call through the existing `IAiProviderClient` path, matching `SuggestAsync`'s options (temperature 0.2, max output 1536, 15 s timeout). Retry once on parse or schema failure.
4. Post-validate:
   - Strip code fences; clamp counts.
   - Invalid CEFR → null. Unknown POS → dropped. Non-single-grapheme emoji → null. Unknown topic → "Khác".
   - Empty strings → null. Wrap IPA in `/…/` if the slashes are missing.
5. **Cache:** only if `IMemoryCache` already resolves from DI (write a DI test). Key `vocab-enrich:v1:{term}:{hint}` (lowercase), 24 h. `AddMemoryCache` is **not** in `Program.cs` today, and the D1 grant does not cover adding it. If it does not resolve, skip caching and note it.
6. Provider errors → `AiProviderErrorMessages.For(code)`, as the existing code does.

**`EnrichBatchAsync`:** 1–10 items; `SemaphoreSlim(3)`; order preserved; one failure does not fail the batch.

**`SearchImagesAsync`:** reuse the Pixabay code (`per_page = max ≤ 8`, `safesearch=true`); return `pageUrl` and `user` as author.

**`VocabApiController`:**
- Same Premium/Admin check as `SuggestVocab`. Pass `HttpContext.RequestAborted`.
- **Rate limiting:** read `IAiRequestRateLimiter` and `IWritingRequestRateLimiter`. Reuse one only if its contract fits a per-user window of about 30 requests per 10 minutes without a new DI registration. Otherwise skip and record the risk.

**Tests** (with `StubAiProviderClient`):
- `Enrich_ClampsAndValidates`
- `Enrich_RetriesOnceOnBadJson`
- `EnrichEndpoint_NonPremium_Returns403`
- `EnrichBatch_RejectsOver10`
- `EnrichBatch_PartialFailure_ReturnsPerItemErrors`

### P7 — Add words (`Set/AddCards.cshtml`, `set-composer.js`, `set-card-form.js`)

**Layout** (`.tct-page--narrow`):

```text
_PageHeader: Title "Thêm từ"; Breadcrumbs Bộ thẻ › {set} › Thêm từ
row: label "Vào bộ:" + .form-select-sm (owned sets; default current) when the user owns >1 set
segmented group aria-label "Cách nhập": "Từng thẻ" | "Dán danh sách" (aria-pressed buttons)
!Premium -> .alert.alert-light-like block using primary-soft/primary-text: fa-crown "Nâng cấp Premium để tự điền nghĩa, ví dụ và ảnh bằng AI." + link "Xem gói Premium" (/Premium)
Từng thẻ:
  restored draft -> small bar "Đã khôi phục {n} thẻ chưa lưu." + link button "Xóa nháp"
  ol.ts-compose-list > li (from <template id="tsComposeCardTpl">)
  button.ts-add-card (dashed var(--tct-border-control), full width, 48px): "Thêm thẻ" | at 100: "Tối đa 100 thẻ mỗi lần" (disabled)
Dán danh sách:
  .tct-card: label "Danh sách từ" + .form-text "Mỗi dòng một từ, có thể kèm dấu gạch ngang và nghĩa. AI sẽ điền phần còn lại (Premium)."
  textarea rows=8 (var(--tct-font-mono)), placeholder "apple - quả táo\nresilient\nborrow: mượn"
  line errors (.text-danger + fa-circle-exclamation): "Dòng {n}: {lý do} ({nội dung})"
  footer: link "Nhập từ file CSV" -> /Sets/{id}/Import | btn-primary "Tách thành thẻ" (disabled when empty)
div.ts-save-bar (mobile fixed bottom with safe-area padding; >=992 sticky bottom var(--tct-space-md); .tct-card look)
  left (aria-live polite): "Đang điền {i}/{n}…" | "{ok} thẻ sẵn sàng" (+ " · {k} thẻ thiếu nghĩa")
  right: btn-primary btn-lg min-width 8rem: "Lưu từ" | "Lưu {n} từ" (pending pattern)
.offcanvas.offcanvas-end#tsCardDetails (560px desktop, full width mobile): header term or "Thẻ {i}" + close; body _CardDetailFields; footer btn-primary w-100 "Xong"
modal#tsDiscardDetails: "Bỏ thay đổi chi tiết?" / "Các thay đổi bạn vừa nhập trong bảng chi tiết sẽ không được lưu vào thẻ." / "Tiếp tục sửa" | "Bỏ thay đổi"
#tsToastHost
script#tsComposeConfig: { setId, userId, isPremium, targets, initialTerm, urls{…}, maxCards: 100, chunk: 30 }
```

**One card:**

```text
article.tct-card.ts-compose-card (valid: success left border 4px; error: danger border)
  header: number (600, muted) + fa-check (success, with visually-hidden "Hợp lệ") | actions:
     .tct-btn-icon image (32px thumb or fa-regular fa-image) aria-label "Ảnh cho thẻ {i}" -> drawer tab Ảnh
     .btn.btn-sm "Chi tiết" (fa-sliders; text hidden <576) aria-label "Chi tiết thẻ {i}" -> drawer tab Ví dụ
     [>1 card] .tct-btn-icon fa-regular fa-trash-can aria-label "Xóa thẻ {i}"
  grid (>=576: [term | meaning] + 96px image column; mobile one column):
     term: input (underline var(--tct-border-control), var(--tct-text-2xl), lang=en, autocomplete=off, spellcheck=false, autocapitalize=none, aria-label "Từ {i}", placeholder "resilient") + inline spinner while AI runs
        sub-row (xs): label "Từ" · small IPA input (.tct-phonetic, aria-label "Phiên âm {i}", placeholder "/rɪˈzɪliənt/") | "Gợi ý" (fa-lightbulb; Premium) or "Gợi ý" with fa-crown (opens #premiumLockedModal)
        senses (>1): checkbox chips "{POS} · {nghĩa}" + "Chọn tất cả"
        did_you_mean: "Có phải bạn muốn gõ <strong>{x}</strong>?" + link button "Áp dụng"
        inner duplicate: fa-triangle-exclamation "Trùng với thẻ số {k}" (warning-text)
        other-set duplicate: "Đã có trong bộ " + links to /Sets/Cards/{id}
        save error: "Đã có trong bộ này" | "Dữ liệu chưa hợp lệ" (danger)
     meaning: textarea 2 rows (aria-label "Nghĩa tiếng Việt {i}", placeholder "Ví dụ: kiên cường, mau phục hồi") + label "Nghĩa"; touched + empty + has term -> "Nhập nghĩa tiếng Việt"
     image column (>=576): 96px tile, dashed border: chosen image or button "Thêm ảnh"
  summary (xs muted): e.g. "Đã điền bằng AI · 3 ví dụ · Tính từ · Cảm xúc · có ảnh"
```

**Behavior** (`set-composer.js`):
1. **Keys:**
   - Enter in Từ → focus Nghĩa.
   - Enter/Tab (no Shift) in the last card's Nghĩa, when that card has a term → add a card and focus its Từ.
   - Ctrl/Cmd+Enter anywhere → save.
2. **Pasting multi-line text into Từ:**
   - Prevent the default paste and run `parseImport`.
   - An empty current card is replaced; otherwise the new cards are inserted after it.
   - Cap 100, with the danger toast "Mỗi lần tối đa 100 từ."
   - Toast "Đã tách thành {n} thẻ".
   - Premium → batch enrich.
3. **Duplicates:**
   - On blur, when the term changed → call `duplicates`.
   - Inner duplicates are computed client-side (trim + lower).
4. **Single enrich** (Premium):
   - Triggered by the button or by auto after 900 ms idle (term ≥2 characters, changed). Auto never runs for batch-created cards.
   - Never overwrite a meaning the user typed (track `aiMeaning`), nor a typed IPA (`readingTyped`).
   - Fill: Definition (chosen sense), Phonetic, PhoneticUk, MeaningEn, POS, examples, synonyms, antonyms, collocations, word family, verb forms, plural, CEFR, topic, emoji, `AiGenerated`.
   - Fetch images lazily (when the Ảnh tab or the image column is visible).
   - Multiple chosen senses: Definition = joined with "; ", POS = union, examples merged ≤6.
   - Error → danger line under the card. Manual entry stays possible.
5. **Batch enrich:** chunks of 10, at most 2 concurrent, progress "Đang điền {i}/{n}…". Keep the meanings typed on pasted lines. One toast per failure: "Lỗi khi điền gợi ý."
6. **Save:**
   - Ignore blank cards. Validate client-side: mark invalid cards, scroll to the first one, toast "Kiểm tra lại các thẻ được đánh dấu."
   - Send chunks of 30 to the target set's `batch` endpoint.
   - Merge results:
     - Inserted cards are removed from the list.
     - `skipped` → "Đã có trong bộ này". `invalid` → "Dữ liệu chưa hợp lệ".
   - Toast:
     - one saved: "Đã lưu {term}" + action "Xem" (`/Sets/Cards/{id}`)
     - several saved: "Đã lưu {n} từ" + action "Xem bộ" (`/Sets/{targetId}`)
   - Nothing left → reset to one empty card, clear the draft, focus Từ.
   - A network failure mid-way stops the save, shows a toast and keeps the unsaved cards.
7. **Remove card:** toast "Đã xóa thẻ" with action "Hoàn tác" (6 s, restores the card at its position).
8. **Draft:**
   - localStorage `tct:compose:{userId}:{setId}` (800 ms debounce, try/catch, ≤100 cards).
   - On load: restore it and show the banner.
   - `?term=` fills the last empty card (or appends one) and focuses Nghĩa.
   - The draft is cleared when everything has been saved. No `beforeunload` prompt.
9. **Drawer:**
   - Open: copy the card's data into the form and mark it clean.
   - "Xong": write the data back and refresh the summary.
   - Closing a dirty form asks `#tsDiscardDetails` first.

**`_CardDetailFields.cshtml` + `set-card-form.js`** (Bootstrap nav-tabs; labels show state: "Ví dụ {n}", "Ảnh ✓", "Thông tin ✓", "Ghi chú ✓", with ✓ `aria-hidden` + visually-hidden "đã có"):

**Tab Ví dụ:**
- Up to 6 rows. Each row: English sentence textarea (required when the row exists) + Vietnamese input.
- Move up/down (`aria-label` "Đưa lên"/"Đưa xuống") and delete.
- Button "Thêm ví dụ".

**Tab Ảnh:**
- Current image (120px) + credit "Ảnh: {author} / Pixabay" (link with `rel="noopener"`, `target="_blank"`).
- "Tải ảnh của tôi lên" (`accept="image/*" capture="environment"`; ≤5 MB check client-side; `card-image`; spinner; toast on failure).
- "Không dùng ảnh".
- Premium: query input (default `image_query` or the term) + "Tìm ảnh" → 2×4 grid of `button[aria-pressed]` thumbnails; picking one sets the URL and source.
- Non-Premium: "Tìm ảnh gợi ý là tính năng Premium."

**Tab Thông tin:**
- POS checkbox chips (≤6).
- Topic input with `<datalist>` (`SetTopics` ∪ the set's topics).
- Emoji input.
- CEFR select ("Không rõ" + A1..C2).
- IPA US / UK.
- English definition.
- Collocations ≤8 `{text, vi}`.
- Synonyms / antonyms as tag inputs (Enter or comma adds a tag, × removes, ≤10).
- Word family ≤8 `{text, POS select, vi}`.
- Verb forms (4 inputs: Quá khứ, Quá khứ phân từ, V-ing, Ngôi 3 số ít) shown when POS has `verb` or data exists.
- Plural shown when POS has `noun` or data exists.

**Tab Ghi chú:** textarea "Ghi chú riêng (mẹo nhớ)", ≤1000, character counter.

**Node test** `scripts/tests/test_set_composer_behavior.cjs` (pattern: `test_notification_behavior.cjs` + `dom_fixture.cjs`) against `TctSets.composeCore`:
- `saveLabel(0|1|5)`
- `findInnerDuplicates`
- `applyEnrich` keeps the user's meaning
- `mergeBatchResults` marks skipped/invalid on the right cards
- `detailSummary`

### P8 — Bulk import

**Parser** (English only; no Chinese branch):
- Strip list markers `^\s*(\d{1,3}[.)]\s+|[-*•·]\s+)`. Skip blank lines and `#` lines.
- Split term and meaning at the first separator found, in this order:
  1. tab
  2. `" - "` / `" – "` / `" — "` (spaces on both sides, so "well-known" survives)
  3. `–` / `—` without spaces
  4. `:` / `：`
  5. `=` / `＝`

  A separator at index 0 → missing term.
- **CSV:** when the first line has a `term` / `từ` / `word` column (`,` `;` or tab), parse RFC 4180 (quotes, `""`, newlines inside cells).
  - Column aliases: term ← `term|từ|word`; meaning ← `meaning_vi|meaning|nghĩa|nghia|vi`; IPA ← `phonetic|reading|ipa|phiên âm`; `example`; `example_vi`; `topic`.
  - The P3.7 export must round-trip.
- **Per-line validation:**
  - "Thiếu từ"
  - "Từ dài quá 255 ký tự"
  - "Nghĩa dài quá 1000 ký tự"
  - duplicate with the set (the server supplies the existing lowercase terms): "Đã có trong bộ"
  - "Trùng dòng phía trên"
  - more than 100 valid lines → "Quá 100 từ mỗi lần" + `truncated=true`
- **Returns:** `{ lines:[{lineNo, raw, term, meaning, phonetic, example, exampleVi, topic, status: ok|duplicate|error, message}], ok, duplicates, errors, truncated }`.

**Node test** `scripts/tests/test_set_import_parser.cjs`:
- The four separators, and `well-known - nổi tiếng`.
- `1. apple`, `- apple`, `# ghi chú`.
- Quoted CSV with commas.
- Duplicate with the set and with an earlier line.
- 101 lines → truncated.
- `- quả táo` → missing term.

**Page** (`.tct-page--narrow`):

```text
_PageHeader "Nhập hàng loạt" (Breadcrumbs Bộ thẻ › {set} › Nhập hàng loạt), Description "Dán danh sách từ, mỗi dòng một từ. Tối đa 100 từ mỗi lần."
input step: label "Danh sách từ" + textarea rows=10 (mono); file button "Tải file CSV" (accept .csv,.txt,.tsv,text/csv,text/plain; ≤500 KB else toast "File quá lớn (tối đa 500 KB).") ; btn-primary "Xem trước"
preview step: .tct-table-wrap table: # | Từ | Nghĩa | Trạng thái (.tct-badge--success "Sẵn sàng" / --warning "Trùng: …" / --danger "Lỗi: …"); summary "{ok} từ sẵn sàng · {dup} trùng · {err} lỗi"
   buttons: "Sửa danh sách" | [Premium] btn-primary "Điền tự động & thêm" | btn-outline-primary "Thêm không cần AI" (only rows with a meaning; others -> "Thiếu nghĩa — cần AI hoặc tự nhập")
   !Premium: only "Thêm {k} từ có nghĩa" + Premium hint
run step: title "Đang điền và thêm từ…" / "Đã thêm {saved}/{total} từ"; Bootstrap .progress (aria-label "Tiến độ nhập", aria-valuenow)
   per-line state: spinner "Đang xử lý" / fa-circle-check success "Đã thêm" / muted "Bỏ qua: Đã có trong bộ" / danger reason
   done: "Thử lại {n} từ lỗi" (if any) | btn-primary "Xem bộ thẻ" | "Nhập tiếp"
```

**Run loop:**
- Chunks of 10, at most 2 concurrent.
- Optional `enrich-batch` → build `CardInputDto` (keep user meanings) → `batch` (≤30).
- Added lines join the "existing" set so a retry cannot duplicate them.
- No OCR / photo import (F4).

### P9 — Card detail and edit

**`Set/CardDetail.cshtml`** (`.tct-page--narrow`):

```text
_PageHeader: Title = term (lang="en" on a wrapping span if the partial allows; otherwise render the term again in the hero with lang and keep h1 = term); Breadcrumbs Bộ thẻ › {set} › {term}
section.ts-word-hero: IPA line (.tct-phonetic): US/UK when they differ (<abbr title="Anh-Mỹ">US</abbr>), else one value; .tct-btn-icon 48px fa-volume-high "Phát âm {term}"; image 96/128px (alt="") or emoji on var(--tct-surface-muted); Pixabay credit
badges: POS (.tct-badge), CEFR (.tct-badge), "Chủ đề: {Topic}" (.tct-badge--info)
p.ts-word-meaning (var(--tct-text-2xl), 700) + MeaningEn (.tct-text-muted)
actions: [CanEdit] "Sửa" -> /Sets/Cards/{id}/Edit · suspend toggle (aria-pressed; "Tạm ẩn khỏi ôn tập" / "Bỏ tạm ẩn") · [CanEdit] btn-outline-danger "Xóa từ" (modal "Xóa “{term}”?" / "Tiến độ ôn của mọi người với từ này cũng bị xóa." -> POST /Sets/Cards/{id}/Delete)
sections (.tct-card each, h2 var(--tct-text-xl)):
  Câu ví dụ: list; each with left border var(--tct-primary); English sentence with the term highlighted via Razor-split segments wrapped in <mark class="ts-hl"> (case-insensitive; no Html.Raw); translation muted; speak button "Đọc câu ví dụ {i}"
  Cụm từ thường gặp
  Đồng nghĩa và trái nghĩa: in-set -> chip link (primary-soft) to /Sets/Cards/{id}; missing & CanEdit -> dashed chip "+ {từ}" -> /Sets/{setId}/Add?term={từ} (title "Thêm từ này"); missing & !CanEdit -> plain chip
  Họ từ và dạng từ (word family, verb forms, plural)
  Ghi chú riêng (owner only; warning-soft background with warning-text; white-space: pre-line)
  Tiến độ của bạn: status badge; "Lần ôn gần nhất: {dd/MM/yyyy | 'chưa ôn'}"; "Lần ôn tới: hôm nay | ngày mai | sau {n} ngày | quá hạn {n} ngày"; "Số lần quên"; "Số lần nhớ liên tiếp" — Vietnam time via the existing BusinessDateHelper if it fits (verify), never a manual +7
```

**`Set/EditCard.cshtml`:**
- Full POST form with `CardFormViewModel` + `_CardDetailFields` (all tabs).
- Fields: term + Premium "Gợi ý" (same no-overwrite rules), IPA, meaning, duplicate warning (excluding this card).
- "Lưu thay đổi" (pending pattern) and "Hủy".
- Field errors render next to each field. An error inside a hidden tab switches to that tab and focuses the first invalid field.
- List binding uses indexed names (`Examples[0].Text`). JS re-indexes rows after add/remove.

### P10 — Connect existing pages (Trọng's files only)

1. **`StudyService.GetStudyViewModelAsync`** gains an optional `string? mode = null`. Keep existing call sites compiling.
   - `mode == "due"` with a user: due progress (not suspended), ordered by `NextReviewDate`, then up to 20 new cards ordered by `CardId`.
   - No cards → the controller redirects to `/Sets/{id}` with TempData "Bạn đã ôn xong bộ này. Quay lại sau nhé."
   - `StudyViewModel.IsDueReview` shows a label "Ôn đến hạn". Grading logic is unchanged.
2. **`StudyController`:**
   - Study/QuizMode/WriteMode/MatchingMode require `CanViewSetAsync` (B4), else `NotFound()`. Legacy sets are Public (F7); chat-authorized sets pass (P3 superset test).
   - Seeders: make sure system sets end up `Public`. The C# default already does this; assert it in a test.
   - An empty study set redirects to `/Sets/{id}` (B6).
   - `UpdateCardProgress`: the card must exist and be viewable, else `NotFound` (B7). Keep `{ success = true }`.
   - Test: outsider + Private set → 404.
3. **`Study.cshtml`, `QuizMode.cshtml`, `WriteMode.cshtml`, `MatchingMode.cshtml`:**
   - Exit/back → `Url.Action("Detail","Set", new { id })`. Keep the existing return path for system vocabulary if those views serve it (verify first).
   - "Chỉnh sửa" → `/Sets/{id}` for editors.
   - Quiz/Matching: only if the end-of-session point is unambiguous, store `localStorage['tct:set-stats:{setId}'] = { quizPercent, quizAt, matchMs, matchAt }` (best `matchMs` kept, try/catch). Otherwise skip and record it.
   - Do not change ids, `data-*` attributes or script order that the JS relies on.
4. **`FolderController` + `Folder/FolderDetail.cshtml`:**
   - Non-managers see only `CanView` sets. Saved/class folders still show all their sets (rule 5.3).
   - Links go to `/Sets/{id}`, plus a small "Học" link to `/Home/Study/{id}`.
   - The create button keeps `/Home/CreateSet?folderId={id}`.
5. **`Folder/Folder.cshtml`:** add a header link "Bộ thẻ của tôi" (`fa-layer-group`) → `/Sets`.
6. **`Vocabulary/Index.cshtml`:** add one link/tile "Bộ thẻ của tôi" (`/Sets`) without changing the rest of the layout. A "copy system set" button belongs to a later task.
7. **If D2 = CÓ:** in `_Layout.cshtml`, add one `nav-sub-item` in `#folderDropdownMenu` that follows the neighbouring items exactly, including `aria-current="@AriaCurrent("Set")"` and the existing icon-color pattern. Nothing else in the layout changes.

### P11 — Public share page and cleanup

**`Set/Shared.cshtml`** (`/s/{slug}`, anonymous allowed):
- Add `noindex` only if the layout exposes a supported hook; otherwise record that it was skipped.
- Content:

```text
section.ts-hero (same as detail; meta "{n} từ" · "của {Owner}" · LearnerCount>0 "{n} người đang học")
anonymous: btn-primary btn-lg "Đăng nhập để học bộ này" -> existing login with returnUrl=/s/{slug} (verify the parameter name) + muted "Miễn phí. Chưa có tài khoản thì đăng ký chỉ mất một phút."
owner: redirect to /Sets/{id}
subscribed: btn-primary "Bạn đang học bộ này, mở bộ thẻ" + btn-outline-primary "Sao chép về bộ của tôi"
else: form POST Subscribe(slug) btn-primary "Lưu vào bộ thẻ của tôi" + form POST Clone(slug) btn-outline-primary "Sao chép về bộ của tôi"
      muted: "“Lưu vào bộ thẻ của tôi” tự cập nhật khi {Owner} thêm từ. “Sao chép” tạo bản riêng, bạn sửa thoải mái."
"Danh sách từ": first 5 as .tct-card tiles (2 cols >=576), the rest as a compact list; WordCount>50 -> "Đang hiện 50 từ đầu tiên. Lưu hoặc sao chép để xem đủ {WordCount} từ."
```

- Buttons use the pending pattern. Forms carry antiforgery. No POST forms render for anonymous users.

**Cleanup:**
- Delete `Views/Set/_SetEditorForm.cshtml` and `Views/Set/EditSet.cshtml` once nothing references them (grep). Keep `SetEditorViewModel` because the legacy POST binds it.
- If D3 = CÓ, add the tracker row in `team-ui-workflow.md` section 6:

```text
| Bộ thẻ cá nhân | `/Sets`, `/Sets/{id}`, `/Sets/{id}/Add`, `/Sets/{id}/Import`, `/Sets/Cards/{id}`, `/s/{slug}` | `personal-sets.css` | `--tct-*` | Đã dùng token | Huỳnh Phú Trọng |
```

  Otherwise put the same row in the execution notes.

### P12 — Verification, review and docs

1. **Commands:**

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/test_set_import_parser.cjs scripts/tests/test_set_composer_behavior.cjs
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
git diff --check
```

   - Compare against the P0 baseline. No test count may drop.
   - Skipped tests are reported as skipped, never as passed.
   - New `.cjs` files may trigger tooling checks. Run the checks AGENTS.md lists for `scripts/**` changes (e.g. `uv run --no-project python -m unittest discover -s scripts/tests -v` when `uv` exists).
2. **Browser review** (`tct-ui-review`, `team-ui-workflow.md` section 7):
   - Start: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
   - Sign in with the synthetic accounts in `.appdata/local/access.json`. Never print passwords.
   - Check at 1440 / 768 / 375–390 + 200% zoom:
     - `/Sets` (with data, empty, add samples)
     - create set
     - detail: filters, search, >50 words with infinite scroll, both views, all four bulk actions, share dialog
     - add words: multi-card, paste split, draft restore, drawer, save, undo
     - import: text, and a CSV produced by Export
     - card detail and edit, suspend
     - `/s/{slug}` anonymous / other user / owner
     - Study `?mode=due`, FolderDetail
     - an AI chat citation link to a set still opens
   - Also check: keyboard (Tab, Shift+Tab, Enter, Escape), visible focus, contrast, long Vietnamese and English text, no horizontal overflow.
   - Confirm Vocabulary, Dashboard and Admin screens look unchanged (scope check: no selector outside `.ts-page`).
   - Screenshots use synthetic data only, saved in `.tmp/personal-sets/` (ignored, never committed).
   - Do not call paid AI unnecessarily. If real AI is exercised, report the number of calls.
   - Anything not observed is reported as not observed.
3. **Docs:**
   - `docs/project-structure.md`: new controllers and the `/Sets` routes.
   - `.ai/context/bug-fix-log.md`: B1–B7 in the file's existing format.
   - `.ai/context/known-issues.md`: close or adjust the related entries.
   - Fill in the execution notes.
4. **Commits** — only if granted. Suggested Conventional Commits:
   - `feat(sets): add personal set workspace schema`
   - `feat(sets): add personal set service and api`
   - `fix(sets): preserve progress when editing legacy sets`
   - `feat(sets): redesign personal set pages with the team design system`
   - `feat(study): link study modes to personal sets and due review`
   - `docs(sets): record personal set redesign plan and evidence`
5. **Stop and report.** Push, PR, CI, merge and staging only with separate owner authority. When a PR is authorized, its description includes:
   - summary
   - schema table
   - MariaDB evidence or the reason it is missing
   - shared-data impact (D4)
   - tests and their counts
   - screenshots (synthetic data)
   - risks
   - out-of-scope items
   - shared files touched (D1–D4)

---

## 8. Definition of done

- [ ] All routes in 5.1 and 5.2 work with the correct access rules. Unauthorized access → 404 (the only 403 is the AI Premium precedent).
- [ ] Every form and AJAX mutation is antiforgery-protected. A missing token → 400 with data unchanged.
- [ ] Views receive ViewModels only. Display reads use `AsNoTracking` + projection. No queries in loops.
- [ ] `Program.cs`, appsettings, `.csproj`, shared CSS, layouts, shared partials, AI code and the shared-data engine are untouched, except where D1–D4 allow.
- [ ] Migration is additive, with Designer + snapshot. No pending model changes. MariaDB evidence recorded, or its absence stated.
- [ ] Shared-data compatibility tests pass: imported sets are Public, and no slug or personal note leaks.
- [ ] `CanView` ⊇ `LearnerCardAccess.Authorized` (tested). The AI chat test suites pass.
- [ ] B1–B7 are fixed and tested. The F6 legacy contracts still work. `FolderSetIdorRegressionTests` and the Sprint tests are green.
- [ ] UI:
  - Wrapped in `tct-theme`; uses `_PageHeader` / `_StateMessage` and the shared components.
  - `personal-sets.css` uses only `var(--tct-*)`, scoped to `.ts-page`.
  - No hex, no `:root`, no weight above 700, no `!important`, no new icon family, package or CDN.
  - The only inline style is `--p`.
- [ ] No mascot, Chinese, group or OCR remnants.
- [ ] Loading, empty, error, success, disabled and pending states everywhere. No raw exceptions.
- [ ] Keyboard-complete; visible focus; Bootstrap modal/offcanvas behavior; decorative images `alt=""`; icon buttons labelled; reduced motion respected.
- [ ] The Vietnamese report states local / CI / staging / production clearly.

---

## 9. Risks

| Risk | Mitigation |
| --- | --- |
| View checks on Study break class / saved-folder / chat flows | Legacy sets are Public; `CanView` covers the folder branches; superset test + AI tests |
| Shared-data import creates sets via reflection | C# default `Public`; compatibility test; no slug or note export |
| New card fields missing from shared packages (D4 = KHÔNG) | Documented limitation; follow-up proposal to Châu |
| Legacy EditSet pairs cards by position | The new UI no longer posts it; regression test; noted in the bug-fix log |
| Orphaned uploaded images | Accepted; cleanup job later |
| AI cost (900 ms autofill, batches) | Premium only; batch ≤10; cache/limiter only where DI already allows |
| Six cover hues feel limited | Token proposal (D3); keep the six meanwhile |
| `color-mix()` support | Fallback to a plain surface panel |
| MariaDB unavailable locally | State "not verified"; CI runs the migration and `SharedDataMariaDbTests` once a PR is authorized |
| `LastReviewedDate` DB default on suspend-only progress | Explicit null; "chưa ôn" display rule |
| Index `(SetId, Term)` key length | 1020 bytes < 3072; if the environment rejects it, drop the index and record it |

## 10. Later (not now)

- "Sao chép về bộ của tôi" on system vocabulary pages.
- Admin SetManagement using the new fields.
- Better scheduling (FSRS) and daily stats.
- Photo OCR import when the provider supports images (Châu).
- Orphaned image cleanup.
- Public set discovery.
- Converting `vocabulary-system.css` and the Folder CSS to tokens.

---

## 11. Execution notes (agent filled in)

- Branch / HEAD at P0: `feature/personal-sets-redesign` from origin/master `eed4e2c`. The local tree had one unrelated whitespace change in `appsettings.Development.json` (left uncommitted), so no `pull-local.ps1` was run.
- Owner decisions D1–D4 as received: all CÓ. D1: one `AddScoped` line (Option A). D2: one sidebar item. D3: tracker row added, no token proposal needed. D4: Card optional fields + private-set guard.
- Baseline test counts: 271 passed, 1 skipped (filtered subset). Final full Release run: 1272 passed, 22 skipped, 0 failed. Node tests: 25/25 (parser 12, composer 13).
- B1–B7: confirmed in source and fixed (see `.ai/context/bug-fix-log.md`).
- Service option used: A (`IPersonalSetService`, namespace `TCTEnglish.Services.Sets`).
- Migration + MariaDB evidence: `20261109154943_AddPersonalSetWorkspace`. `IX_Cards_SetId_Term` is created before `IX_Cards_SetID` is dropped (EF's generated order fails on MariaDB with error 1553). On disposable `mariadb:10.5.23` (`tct-ci-mariadb`, port 33307): `scripts/verify-mariadb.ps1` passed (16 migrations); manual downgrade/upgrade/repeat with legacy rows kept Phonetic/Example/Topic/progress, `Visibility='Public'`, `AiGenerated=0`, `IsSuspended=0`, cascade FKs present. `SharedDataMariaDbTests` were not run locally (skipped).
- Shared-data compatibility: Card spec gained `OptionalFields` (omitted while default) so old packages and digests stay valid; validator, engine and exporter changed minimally; tests in `SharedDataPersonalSetCompatibilityTests`.
- Chat superset test result: passes for owner, folder owner, saved folder, class owner, class member (reflection on `LearnerCardAccess`).
- Quiz/Matching end-of-session hook found?: yes (`QuizMode` finishStudy, `MatchingMode` finishGame); localStorage `tct:set-stats:{setId}` is written.
- IMemoryCache available? / limiter used: `IMemoryCache` is an optional constructor dependency (used only if registered). Chat `IAiRequestRateLimiter` not reused (1-minute window shared with chat).
- Token proposal: none; the six existing hues were kept.
- Deviations: new code uses `TCTEnglish.*` namespaces while legacy controllers stay `TCTVocabulary.*`; login has no `returnUrl`; legacy EditSet pairs by term then position; Pixabay redirects are followed by the default HttpClient (final host re-checked); no `noindex` hook on the shared page.
- Browser (headless Chrome via CDP, Premium synthetic account, port 5100, screenshots in ignored .tmp/personal-sets/): /Sets (1440, 390), create, add words (1440, 390), detail (1440, 768, 390) with 60 words (infinite scroll 50 -> 60), settings, import, card detail/edit, study ?mode=due: pages render, no console/JS errors, no horizontal overflow outside the off-canvas drawer. Fixed one layout bug found (meaning cell clamp broke table layout).
- Second browser pass (headless Chrome CDP, local web on a free port, synthetic Premium/Standard/Admin accounts, no paid AI call: the local key is absent so enrich failed fast): search filter, select mode + bulk suspend/unsuspend with the status filter, share dialog (toggle, link, Escape closes, focus now returns to the menu button), paste composer (3 valid cards, in-batch duplicate reported, one summary toast plus an inline message per card when AI enrich is unavailable), anonymous /s/{slug} at 1440 and 375 (CTA only, no private note), bad slug and a non-owner opening a private set or its study page both return 404, Standard user sees save/clone actions, Admin dashboard and Home still render without console errors. Tab order of the detail page is logical with a visible focus ring everywhere except the shared layout search box (not part of this feature). Detail page at 720px/2x (200% zoom equivalent), 390, 375 and 320px has no horizontal scroll; list/add/settings/import/create have none at 375px. A scripted contrast check of text on the detail and shared pages found no failures against 4.5:1 (3:1 for large text).
- Still not verified: screen-reader behaviour, real touch interaction, AI enrich/images with a configured key (premium flow with a key), image upload, class/folder-member viewing in a browser, drag-reorder of the compose list, Admin/Dashboard visual diff (only render/console checked). CI, staging and production were not touched.
