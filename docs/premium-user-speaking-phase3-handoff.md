# Premium User Speaking — Phase 3 Handoff

## Phase Handoff
Phase completed: Phase 3 — `Bài nói của tôi` UI And Practice Integration
Model used: Claude Sonnet 4.6 (Thinking) in Antigravity
Ready for next phase: YES

Scope completed:
- Added hero import panel (`mip-*`) to `Views/Speaking/Index.cshtml` — gradient blue, YouTube chip, large URL input, CTA button; visible to all authenticated users
- Simplified panel to YouTube-URL-only — no `Video Language`, no `Subtitle`, no multi-source flow
- Wired AJAX submit to `POST /Speaking/My/Create` with anti-forgery token
- Standard user upgrade prompt: when server returns `code: premium_required`, inline amber feedback with `/Account/Premium` link is shown — import panel is NOT hidden
- Added `Bài nói của tôi` section below the import panel and above the public catalog
- Section renders: thumbnail, title, `Riêng tư` badge, status badge (Sẵn sàng / Đang xử lý / Thất bại / Đã khóa), duration, sentence count, creation date
- "Luyện ngay" action for ready videos; "Xóa" button for ready + failed; "Nâng cấp để mở khóa" (amber CTA) for locked-shell videos (downgraded Premium → Standard)
- Empty state shown when `MyVideos` is empty; skeleton loading card shown during ongoing import
- Bootstrap 5 delete confirmation modal with animated card removal on success
- Public catalog (topic/level filters + level sections) preserved unchanged; scoped exclusively to `OwnerUserId == null` videos via the service layer
- `Bài nói của tôi` section never receives public catalog references (section data comes from `Model.MyVideos` only)
- Added `IsPrivate` to `SpeakingPracticeViewModel` + set in `SpeakingService.GetSpeakingPracticeViewModelAsync`
- Added `Riêng tư` badge in `Practice.cshtml` `spk-topnav-right` when `Model.IsPrivate == true`
- Practice page no-translation safety: `VietnameseMeaning = ""` for private imports is already safe — `transcriptVi` div has `d-none` class permanently, so empty translations cause no UI breakage
- Appended all CSS (`mip-*`, `my-*`, skeleton, responsive) to `speaking.css`
- Appended Part 3 IIFE to `speaking.js` with page guard (`#mip-submit-btn` check)

Files changed:
- `TCTEnglish/ViewModels/SpeakingPracticeViewModel.cs` — added `IsPrivate` property
- `TCTEnglish/Services/SpeakingService.cs` — set `IsPrivate = video.OwnerUserId.HasValue` in practice mapping
- `TCTEnglish/Views/Speaking/Index.cshtml` — full rewrite: import panel + Bài nói của tôi + separator + preserved public catalog + delete modal
- `TCTEnglish/Views/Speaking/Practice.cshtml` — added private badge in topnav-right
- `TCTEnglish/wwwroot/css/speaking.css` — appended Phase 3 CSS (~720 lines)
- `TCTEnglish/wwwroot/js/speaking.js` — appended Part 3 IIFE (~240 lines)

Key rules/policies implemented:
- Import panel visible to ALL authenticated users (no role check in view) — `[Authorize]` on the controller guarantees the user is logged in
- Standard users see panel, submit form, receive inline `premium_required` upgrade prompt with link — panel NOT hidden
- Locked-shell cards (`v.IsLocked == true`) show lock overlay + "Nâng cấp để mở khóa" amber CTA; practice link is NOT rendered
- `MyVideos` section only populated from `Model.MyVideos` (private owner-scoped) — public catalog queries remain scoped to `OwnerUserId == null` in service
- Delete uses `POST /Speaking/My/Delete` with `[ValidateAntiForgeryToken]` — anti-CSRF preserved
- Private badge shown in practice page topnav so users know they are practising a private video
- No translation UI rendered for private imports (`VietnameseMeaning = ""` is handled by existing `d-none` on `transcriptVi`)
- No `Video Language` or `Subtitle` field in import form
- No public catalog items appear in `Bài nói của tôi` section

Commands run:
- `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -v quiet` → **Build succeeded. 0 Warning(s). 0 Error(s).**

Verification results:
- Build passes with zero errors and zero warnings
- `SpeakingPracticeViewModel.IsPrivate` compiles correctly
- `SpeakingService.GetSpeakingPracticeViewModelAsync` sets `IsPrivate` correctly
- All Razor view syntax verified (no inline C# errors)
- JS IIFE guarded by `#mip-submit-btn` check — does not run on Practice page
- CSS class names are consistent between Index.cshtml and speaking.css

Open risks or blockers:
- `SaveSpeakingProgress` still lacks parent video ownership check (Phase 4 scope) — a user could POST a sentence ID from another owner's private video if they enumerate IDs; Phase 4 must fix this
- Account deletion does not yet clean orphaned private SpeakingVideos (Phase 4 scope)
- `POST /Speaking/My/Delete` allows deletion only when `role is Premium or Admin` — downgraded Standard owners cannot delete locked items. Feature plan says "không xóa khi đang locked" (no delete when locked). This is correctly enforced by the service-level role check.
- The `/Account/Premium` upgrade link in the feedback prompt is a placeholder — the actual premium upgrade page URL may differ; Phase 4 should verify the correct route
- No automated browser tests exist for the import panel flow; manual browser verification recommended before merge

If approval is needed before the next phase:
- No approval needed — Phase 4 can start immediately
- Phase 4 should be aware: `Program.cs` still has NOT been modified; SpeakingService is instantiated directly in the controller constructor (Phase 2 decision)

Next phase should start from:
- Read `docs/premium-user-speaking-feature-plan.md` for business requirements
- Read `docs/premium-user-speaking-multi-model-execution-plan.md` Section 10 for Phase 4 scope
- Read this handoff for the current implementation state
- Focus Phase 4 on:
  1. `SaveSpeakingProgress` — verify sentence belongs to public video OR private video owned by current user
  2. Account deletion cleanup — add `DeleteOwnedPrivateSpeakingContentAsync` in `AccountController`
  3. Anti-IDOR audit on all Speaking read/mutate paths
  4. Downgrade lock behavior verification across list/create/practice/delete
  5. Test coverage for entitlement, English-only, duplicate-per-owner, caption-first, Gemini fallback, locked-shell, progress endpoint hardening, and account deletion
