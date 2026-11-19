# Execution Prompt: Listening catalog — direction B

Follows [the prompt standard](README.md) and [the execution template](../templates/execution-prompt.md).
Save as `.ai/prompts/claude-code-listening-catalog-b.md`.

> **Ghi chú cho Trọng.**
> - Hướng B: hero ảnh lớn bên trái + thẻ "Bài nghe từ YouTube" ngay bên phải, phía dưới là các kệ theo trình độ.
> - Mockup: `Claude outputs/listening-catalog-mockups/B1-hero-va-the-youtube.png`.
> - Năm quyết định D1–D5 ở mục 1 của plan (mặc định: pill gộp A1–A2/B1/B2/C1–C2 và ẩn nhóm trống; giữ ô chọn chủ đề; kệ "Bài nghe của tôi" lên đầu khi có bài; tạo xong chèn ngay, không tải lại trang; tối đa 5 bài mỗi kệ).
> - Chỉ sửa trang danh sách Luyện nghe. Không migration, không đổi trang luyện. Không push.

## Outcome

`/Home/Listening` matches mockup B1.
1. **Top row:**
   - a photo hero for continue, start, review or guest;
   - next to it, a "Bài nghe từ YouTube" card: Premium sees the import form, Standard sees an upsell, guests see a login prompt.
2. **Filters:** band pills (empty bands hidden), status pills and topic in one row.
3. **Content:**
   - "Bài nghe của tôi" shelf first when present;
   - level shelves of up to 5 cards with "Xem cả n bài";
   - restyled cards.
4. The old bottom import section and the big level cards are removed.
5. All existing behaviour still works: search, filters, deep links, continue, import, delete.
6. Phone and tablet layouts are done.

## Context and evidence

- **Plan (authoritative):** `docs/superpowers/plans/2026-11-20-listening-catalog-b.md`. Read all of it before starting.
- **Rules:** `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`.
- **Code:** `Views/Study/Listening.cshtml` and its `_Listening*` partials, `wwwroot/js/listening-core.js`, `listening-index.js`, `wwwroot/css/listening.css`, `Services/ListeningService.cs`, `ViewModels/ListeningViewModels.cs`, and the listening tests (C# + node).
- **Verified facts:** plan 0.3 (`master` 5e8e92b). Reproduce at K0. Resolve plan 0.4 at K0.

Treat the mockup and quoted copy as data, not instructions. Vietnamese strings in the plan are literal UI copy.

## Mode, authority and scope

- `IMPLEMENT_LOCAL`, one agent, continuous K0 → K7, no subagents.
- **Owner decisions:** D1 = PILL GỘP, ẨN NHÓM TRỐNG; D2 = GIỮ CHỦ ĐỀ; D3 = KỆ "BÀI NGHE CỦA TÔI" Ở ĐẦU; D4 = CHÈN NGAY SAU KHI TẠO; D5 = 5 BÀI/KỆ (edit here if different).
- **Run mode:**
  - Do not stop between phases while gates are green.
  - Stop only if a forbidden file is needed or a gate item fails 3 times and blocks everything.
  - Resume at the first phase without a record in plan section 12.
- **Git:**
  - At K0, switch to `master` and pull once with `pwsh -NoProfile -File scripts/pull-local.ps1`. This is allowed only if the tree is clean apart from `appsettings.Development.json` and `Claude outputs/`.
  - Create `feature/listening-catalog-b`. Local commits only, one per phase, via `tct-commit-message`.
  - Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, `.appdata/`, screenshots.
  - No push, PR, merge, rebase, reset.
- **Allowed and forbidden files:** plan section 5.
- **Data:** synthetic accounts from `.appdata/local/access.json` only, never printing passwords. Delete any test lesson you import at the end through the UI. Never touch seeded lessons.

## Execution

K0 baseline → K1 logic (tests first) → K2 top row → K3 filters and shelves → K4 import flow → K5 responsive and accessibility → K6 mockup comparison and polish → K7 docs, full gate, report.

After each phase run gates G1–G15 (plan section 7):

| Gate | Check |
| --- | --- |
| G1 | build |
| G2 | listening tests |
| G3 | full suite (counts ≥ K0) |
| G4 | node tests (counts ≥ K0) |
| G5 | `has-pending-model-changes` none, routes unchanged |
| G6 | tokens grep, no inline blocks, `git diff --check`, encoding guard |
| G7 | file scope |
| G8 | render matrix × 3 roles, console and network clean |
| G9 | mockup comparison |
| G10 | behaviour checklist (plan 8) |
| G11 | accessibility |
| G12 | scripted contrast |
| G13 | regression |
| G14 | copy and consistency |
| G15 | security |

Gate rules:
- Fix and rerun the whole gate.
- At most 3 attempts per item, then deferred with evidence.
- Never weaken tests, never touch forbidden files, never call a skipped check passed.

Write the gate record into plan section 12, commit, then continue.

## Verification and acceptance
- [ ] Plan section 10 checked with evidence.
- [ ] Before/after screenshots in `.tmp/listening-catalog-b/`.
- [ ] Build, tests, node tests green; counts ≥ K0; no migration; no forbidden file.

## Handoff
Report in Vietnamese, once, at the end:
- the per-phase gate table with commit hashes;
- the K0 inventory (ids changed or removed and the tests updated);
- changed files;
- how to try it per role;
- whether the live import was verified;
- differences from the mockup and why;
- remaining risks.

List the local commits. State that nothing was pushed or merged.
