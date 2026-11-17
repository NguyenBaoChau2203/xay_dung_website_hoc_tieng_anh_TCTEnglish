# Plan: Landing page "Đêm học" (dark, product-led) and a bug sweep of Trọng's modules

Save as `docs/superpowers/plans/2026-11-16-landing-night-and-bug-sweep.md`.
Execution prompt: `.ai/prompts/claude-code-landing-night-and-bug-sweep.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Việc 1 – Landing page mới theo hướng D "Đêm học":** nền xanh đêm có ánh sáng, tiêu đề lớn, ảnh trang Từ vựng thật đặt nghiêng 3D, các thẻ nhỏ bay quanh; thêm phần **học thử 3 câu ngay trên trang**, bảng kỹ năng, lịch ôn 1 → 60 ngày, trợ lý AI, câu hỏi thường gặp, footer mới. Có bản điện thoại.
> - **Việc 2 – Sửa lỗi các phần của bạn:** sửa lỗi bảng "Chi tiết" thẻ với tài khoản Standard, rồi **quét lỗi tự động** mọi trang của bạn (Bộ thẻ, Luyện tập, Từ vựng, Nghe, Thư mục, Thông báo, Thanh toán, 4 khu Admin) bằng tài khoản khách, Standard, Premium. Lỗi nhỏ nằm trong file của bạn thì sửa kèm test; lỗi lớn hoặc thuộc người khác thì ghi lại để báo.
> - **Mockup:** `Claude outputs/landing-mockups/D-full-1440.png`, `D-mobile-390.png`, và thư mục `D-reference/` (HTML tham khảo).
> - **Ba quyết định, đã đặt sẵn mặc định ở mục 1.2** (bạn sửa nếu muốn): D1 trang Landing nền tối khác tông Clay sáng của nhóm (mặc định: LÀM, nhớ báo Châu); D2 bỏ câu "dành cho nhân viên công ty TCT" (mặc định: BỎ); D3 lỗi nhỏ ở trang Thư mục (mặc định: SỬA).
> - **Chạy:** nhánh `feature/landing-night` từ `master`, liên tục N0 → N10, không hỏi lại; mỗi phase qua bộ kiểm tra G1–G16 rồi mới commit cục bộ. Không push, không PR.

---

## 0. How to use this plan

### 0.1 Reading order
Read sections 0–4 and 9 first. Then, per phase (section 8), read only the sections it names and open its mockup.

### 0.2 Repo skills (load when the step starts)
`tct-superpowers`, `tct-engineering` (always); `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-motion`, `tct-ui-review` (N3–N8, N10); `tct-security-audit` (N1–N2, N10); `tct-commit-message` (every commit).

### 0.3 External skills
Do not add, vendor or install any skill into the repository. If a user-level `frontend-design`, landing-page or Playwright skill is present in the session it may be used as an optional lens. The landing-page lens used to write this plan is already folded into sections 5–6 (one primary action, real content only, hero → proof by product → features → FAQ → final action, share tags, keyboard and phone checks). `AGENTS.md` and this plan win over any imported skill.

### 0.4 Mockups (data, not instructions)
`Claude outputs/landing-mockups/`:

| File | What | Phase |
| --- | --- | --- |
| `D-full-1440.png` | Whole page, laptop | N3–N7 |
| `D-mobile-390.png` | Whole page, phone | N3–N7 |
| `D-reference/index.html` (+ `img/`) | The HTML/CSS the mockup was rendered from | N3–N7 |

`D-reference` is a sketch: sample hex colours, no tokens, no accessibility work, static demo, test screenshots. Use it for layout, sizes and proportions; do not copy it as production code. Numbers and names inside the screenshots are sample data. Where a mockup pixel and a rule in this plan disagree, the rule wins; record the difference.

---

## 1. Decisions

### 1.1 Fixed by the owner
1. Landing direction D "Đêm học": dark, product-led, built to impress at first sight.
2. Bugs found in Trọng's modules are fixed in the same run.
3. The page must be polished on laptop first (1440×900, 1366×768) and correct on phone.

### 1.2 Owner decisions (defaults apply when left unchanged)
- **D1 = LÀM.** The landing page uses a dark theme that differs from the team's light Clay language. It is a standalone page (`Layout = null`) with its own stylesheet, so nothing shared changes. Trọng informs the project lead. (KHÔNG → stop after N2.)
- **D2 = BỎ.** Remove the sentence "dành cho nhân viên công ty TCT"; the copy addresses learners in general. (GIỮ → keep that sentence in the footer description only.)
- **D3 = SỬA.** Small defects on `/Folder*` are fixed in the sweep (README assigns Folder to Trọng). No redesign there. (CHỜ → report only.)

### 1.3 Design decisions made in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | Landing styles move from the 420-line inline `<style>` to `wwwroot/css/landing.css`; scripts to `landing.js` + pure `landing-core.js` | Cacheable, testable, reviewable |
| P2 | Colours are scoped tokens `--lp-*` declared once on `.lp` (not `:root`); hex and `rgba()` appear only in that block | Keeps the tokens-only discipline on a page that cannot use the light tokens |
| P3 | Fonts: Be Vietnam Pro only (already loaded by this page); Playfair Display removed; weights 400–800 | No new font; one request fewer |
| P4 | Hero visual is a real screenshot of the Vocabulary home, captured from the running app with synthetic data | Honest product proof; no invented UI |
| P5 | The "học thử" demo is client-only (3 fixed questions, no network, no storage) | Zero backend risk, works for anonymous visitors |
| P6 | No fake numbers, logos, reviews or testimonials anywhere | Nothing on the page may be untrue |
| P7 | Content is visible without JavaScript; one load sequence in the hero is the only non-triggered motion | The old page hid every section until a script ran |
| P8 | FAQ uses native `<details>` | Accessible without script |
| P9 | Primary action everywhere: "Bắt đầu miễn phí" → `/Account/Register` | The old buttons opened the login tab |
| P10 | Bug sweep fixes defects only; redesigns of Billing, Notification, Folder and Admin are separate work | Keeps this run bounded |

---

## 2. Scope

### 2.1 Files that may be added
- `wwwroot/css/landing.css`, `wwwroot/js/landing.js`, `wwwroot/js/landing-core.js`
- `wwwroot/images/landing/` — `hero-home-2240.webp`, `hero-home-1120.webp`, `lesson-card-1560.webp`, `lesson-card-780.webp`, `og-landing.png` (1200×630) (names may differ; record them)
- `TCTEnglish.Tests/LandingPageTests.cs`
- `scripts/tests/test_landing_core.cjs`
- Regression tests for sweep fixes (new files, named after the feature)

### 2.2 Files that may be edited
- `Views/Home/landing.cshtml`
- `wwwroot/js/set-card-form.js` (N1)
- **Sweep fixes (N2), defects only**, in Trọng's files: views, page CSS and page JS of `/Sets*`, `/Practice*`, `/Vocabulary*`, `/Home/Listening*`, `/Folder*` (if D3 = SỬA), `/Notification`, `/Billing*`, and `Areas/Admin/Views/{SetManagement,ListeningManagement,BillingManagement,NotificationManagement}`; their controllers and services only for a clear defect with a test, never a contract change
- Trọng's own tests when behaviour changes on purpose (record why)
- Docs: tracker (`docs/design-system/team-ui-workflow.md`: add a "Landing" row, correct the "Từ vựng" status to merged), `.ai/context/known-issues.md`, `bug-fix-log.md`, `project-structure.md` when applicable; this plan's section 12

### 2.3 Forbidden
`Program.cs`, `appsettings*.json`, `.csproj`, migrations, `Models/**`; `HomeController.cs` (the `Landing` action stays as is); `LearningApiController.cs`, `GoalsService*`, `Services/AI/**`, account/auth code; `_Layout.cshtml`, `layout.css`, `site.css`, `tct-learning-ui.css`, `study-kit.css` (except a defect fix that only narrows a selector, recorded), shared partials including `_AiChatLauncher.cshtml` and `ai-chat-launcher.*`; `Class-Detail.css` and `/Class*`; Speaking, Writing, Reading, Goals, Dashboard, AI chat; payment provider logic (signatures, IPN, amounts, status transitions); other owners' tests; new packages, fonts or CDNs.

### 2.4 Must stay unchanged
- `/` for an anonymous visitor still redirects to the landing page; `/Home/Landing` returns 200 anonymously.
- Any string that an existing test of another owner asserts on the landing page (find at N0; keep it on the page, for example in the `<title>`, and record it).
- The AI launcher keeps working on the landing page.
- All routes, antiforgery, 404 rules, payment flows and data of the swept modules.

---

## 3. Findings (static review 2026-11-16 on `master` at `22c53ab`; re-verify at N0)

| # | Where | Finding | Phase |
| --- | --- | --- | --- |
| F1 | `landing.cshtml` | 794 lines, 420 of them an inline `<style>` with its own `:root` palette; no tokens | N3 |
| F2 | `landing.cshtml` | "Bắt đầu miễn phí", "Học ngay miễn phí", "Tạo tài khoản miễn phí" all link to `/Account/Login` (login tab) | N3–N7 |
| F3 | `landing.cshtml` | Three footer social icons link to `#` | N7 |
| F4 | `landing.cshtml` | Copy is stale: "4 chế độ" with "kéo thả ghép thẻ", "Trợ lý AI OpenAI", "dành cho nhân viên công ty TCT"; spaced repetition is one clause | N4–N7 |
| F5 | `landing.cshtml` | Every section has `.reveal` (hidden until an `IntersectionObserver` adds `.visible`); a script error or no JS leaves the page blank; no reduced-motion handling; anchor smooth-scroll hijacks focus | N3, N8 |
| F6 | `landing.cshtml` | Loads Bootstrap CSS+JS and Font Awesome 6.5.1 from CDNs (the app uses 6.0.0) and two Google font families with nine weights | N3 |
| F7 | `landing.cshtml` | No meta description, no Open Graph tags, no theme colour | N3 |
| F8 | `set-card-form.js` ~465 | `refs.imageResults.textContent = ''` runs for Standard users, for whom the image search block is not rendered → `TypeError`, the "Chi tiết" panel on `/Sets/{id}/Add` does not open and `/Sets/Cards/{id}/Edit` logs an error (known issue SETS-CARD-DETAILS-NONPREMIUM-NULL) | N1 |
| F9 | tracker | The "Từ vựng" row still says "chưa duyệt trên nhánh chính" although it is merged; there is no "Landing" row | N9 |
| F10 | owner controllers | Every POST in Folder, Notification, Billing, Set, Practice, Vocabulary and the four admin controllers is covered by antiforgery (the MoMo IPN webhook is exempt by design). No finding. | — |
| F11 | `Views/Footer/*` | `/Home/Introduction`, `/Home/Termsofuse`, `/Home/Privacypolicy` exist. Confirm they open for an anonymous visitor; if they redirect to login, report (controller is forbidden) and link only the ones that open. | N0, N7 |

Static reading cannot find runtime defects; N2 exists for that.

---

## 4. Bug work

### 4.1 N1 — Sets card details for Standard accounts (F8)
1. Test first: a DOM test in the style of `scripts/tests/test_set_composer_behavior.cjs` (uses `dom_fixture.cjs`) that builds the card form **without** the image-search block (`isPremium: false`), calls the path that fails (`setData` / opening details) and asserts no throw and that the details open. It must fail before the fix.
2. Fix: guard every unguarded use of `refs.imageResults`, `refs.imageSearch`, `refs.imageQuery` (check lines ~377–407 and ~465; the handlers at ~405 are already inside `if (refs.imageSearch)`).
3. Verify in the browser with the synthetic Standard account: `/Sets/{id}/Add` → "Chi tiết" opens; `/Sets/Cards/{id}/Edit` has a clean console; Premium still searches images.
4. Move the known-issue entry to resolved.

### 4.2 N2 — Bug sweep
**Tooling.** A Playwright script kept in `.tmp/bug-sweep/` (never committed), using the pre-installed browser. Accounts from `.appdata/local/access.json` (never print passwords): guest, Standard, Premium, and Admin if a synthetic admin exists (otherwise record admin pages as not swept).

**Routes** (discover real ids from the local database; at least one populated and one empty state each):
`/Sets`, `/Sets/{id}`, `/Sets/{id}/Add`, `/Sets/{id}/Import`, `/Sets/Cards/{id}/Edit`, `/s/{slug}`; `/Practice`, each `/Practice/{mode}`, `/Practice/HardWords`; `/Vocabulary`, `/Vocabulary/Detail`, `/Vocabulary/Study`, `/Vocabulary/Review`, legacy `/Vocabulary/*` URLs; `/Home/Listening`, `/Home/Listening/Practice/{id}`; `/Folder`, `/Folder/Detail/{id}` (real route names by discovery); `/Notification`; `/Billing/Pricing`, `/Billing/History`, payment result pages reachable without paying; admin `SetManagement`, `ListeningManagement`, `BillingManagement`, `NotificationManagement` index and form pages (open only; submit nothing that sends notifications or changes billing).

**Per page, at 1440×900 and 390×844, record:**
- console errors and unhandled rejections (keep an explicit allow-list of known noise, for example the two `jQuery is not defined` errors on `/Goals`, which is not swept);
- failed requests (4xx/5xx) other than expected ones;
- broken images (`naturalWidth === 0`), links with `href` empty, `#` or `javascript:`;
- controls without an accessible name; inputs without a label;
- horizontal overflow; text clipped or overlapped by the AI launcher on a primary button;
- a smoke run of the primary actions: create/edit/delete a set and a card in a throwaway set; one round of each practice mode; one vocabulary lesson step; play/pause and one dictation check; create/rename/delete a throwaway folder; open and mark a notification read; open pricing as Standard and as Premium. **Never** start a real payment, send a real notification or call a paid AI endpoint.

**Triage.** Write a table into section 12: id, page, role, symptom, cause, owner, action.
- **Fix now** when all hold: the defect is in a file of 2.2; the fix is small (about 40 lines or fewer), changes no schema, route, JSON contract or payment logic; a regression test (xUnit or node) can pin it. Write the test first.
- **Report** (known-issues entry + handoff) when the fix is larger, needs a forbidden file, belongs to another owner, or is a design/redesign matter.
- Remove throwaway data created by the sweep; never reset the database.
- Budget: at most 15 fixes in this phase; list the rest by severity.

---

## 5. Landing page specification

Common rules: one root `<body class="lp">`; classes `lp-*`; all colours through `--lp-*`; no inline `style` attributes except custom properties for sizes (`--lp-h`); no `!important`; font weight up to 800 on this page only (headings); focus ring visible on the dark background (2 px accent outline, 2 px offset); touch targets ≥ 44 px; one `forced-colors` block and one `prefers-reduced-motion` block; Vietnamese strings below are literal.

### 5.1 Head and assets
- `<title>`: "TCT English — Học tiếng Anh mỗi ngày 15 phút" (append any string an existing test requires).
- `<meta name="description">`: "Học từ vựng, nghe, nói, đọc và viết tiếng Anh theo bài ngắn. TCT English tự xếp lịch ôn để bạn nhớ lâu."
- Open Graph: `og:type=website`, `og:locale=vi_VN`, `og:title`, `og:description`, `og:image` (absolute URL built from the request scheme and host, pointing to `og-landing.png`), `twitter:card=summary_large_image`. `<meta name="theme-color">` = the background token value.
- Fonts: the existing Google Fonts link reduced to `Be Vietnam Pro:wght@400;500;600;700;800&display=swap`, with the two `preconnect` hints kept. System fallback stack so text paints before the font arrives.
- Icons: Font Awesome from the exact URL `_Layout.cshtml` uses (6.0.0), not 6.5.1. Use only icons that exist in 6.0.0.
- Bootstrap: remove the CDN CSS and JS from this page **if** the AI launcher works without them (verify at N3: open, send nothing, close, keyboard). If it needs Bootstrap, load the local copy from `wwwroot/lib/bootstrap` instead of the CDN and scope nothing new on it.
- `landing.css` and `landing.js` with `asp-append-version`; scripts `defer`.

### 5.2 Tokens (`.lp { … }` in `landing.css`, the only place with raw colours)
| Token | Value | Use |
| --- | --- | --- |
| `--lp-bg` | `#0a0f2e` | page background |
| `--lp-surface` | `rgba(255,255,255,.07)` | glass cards, nav |
| `--lp-surface-strong` | `rgba(255,255,255,.12)` | hover, chat bubble |
| `--lp-line` | `rgba(255,255,255,.14)` | borders |
| `--lp-text` | `#ffffff` | headings, body on dark |
| `--lp-muted` | `#b9c2f0` | secondary text |
| `--lp-dim` | `#8f9ad0` | footer text |
| `--lp-primary` | `#4255ff` | brand, buttons |
| `--lp-primary-strong` | `#3447ee` | pressed, ladder base |
| `--lp-violet` | `#7c3aed` | gradient end, glow |
| `--lp-cyan` | `#06b6d4` | glow, listening accent |
| `--lp-accent` | `#ffcd1f` | final step, yellow button |
| `--lp-on-accent` | `#111c2d` | text on yellow and on white cards |
| `--lp-card` | `#ffffff` | demo card, floating chips |
| `--lp-card-muted` | `#55607a` | muted text on white |
| `--lp-success`, `--lp-success-strong`, `--lp-success-soft` | `#10b981`, `#047857`, `#ecfdf5` | correct answer |
| `--lp-danger`, `--lp-danger-soft` | `#ba1a1a`, `#fef2f2` | wrong answer |
| `--lp-radius-sm/md/lg/xl` | 14 / 22 / 30 / 40 px | radii |
| `--lp-shadow-glow`, `--lp-shadow-card`, `--lp-shadow-button` | composed from the tokens above | depth |

Check every text/background pair with a script (G12); if a pair fails, change the token, not the rule.

### 5.3 Page structure (top to bottom)
`<a class="lp-skip" href="#noi-dung">Bỏ qua điều hướng</a>` → `<header>` nav → `<main id="noi-dung">` with sections 5.5–5.10 → `<footer>`. One `h1`; each section has an `h2`; cards use `h3`.

Background: `--lp-bg` plus three soft glows (primary centre behind the screenshot, violet top-left, cyan right) drawn with `radial-gradient` layers on a pseudo-element. Do not use `filter: blur()` on large elements.

### 5.4 Navigation
- Glass bar (surface, 1 px line, radius md, backdrop blur with a solid fallback), sticky at the top with 20 px margin.
- Logo text "TCT English" → `/`.
- Links: "Kỹ năng" → `#ky-nang`; "Cách ôn tập" → `#on-tap`; "Trợ lý AI" → `#tro-ly-ai`; "Liên hệ" → `/Home/Contact`.
- Right side, anonymous: "Đăng nhập" → `/Account/Login`; button "Bắt đầu miễn phí" → `/Account/Register`. Signed in: one button "Vào trang học" → `/Home/Index`.
- Below 900 px: links collapse behind a button (`aria-expanded`, `aria-controls`, label "Mở menu" / "Đóng menu"); the panel is a plain disclosure; `Esc` closes it and returns focus. Without JS the links render as a wrapped row.
- In-page links use native anchor navigation with `scroll-margin-top` on targets and `scroll-behavior: smooth` only when motion is allowed. No scripted scroll.

### 5.5 Hero
- Pill: mascot image (24–30 px, decorative) + "Có trợ lý AI trả lời mọi câu hỏi tiếng Anh". Use the image file the launcher already loads on this page so it comes from cache; never the 498 KB file twice in different sizes.
- `h1`: "Tiếng Anh ngấm dần," line break "mỗi ngày 15 phút." Size `clamp(40px, 6vw, 86px)`, weight 800, tight tracking, a top-to-bottom white→pale-blue gradient clipped to the text with solid `--lp-text` as fallback and under forced colours.
- Sub: "Học từ vựng, nghe, nói, đọc và viết theo bài ngắn. TCT English tự xếp lịch để bạn ôn lại đúng lúc sắp quên."
- Buttons: primary "Bắt đầu miễn phí" → `/Account/Register`; glass "Xem cách học" (play icon) → `#hoc-thu`.
- **Product shot:** browser-style frame (three dots) holding `hero-home-*.webp` (`srcset` 1120w/2240w, explicit `width`/`height`, `fetchpriority="high"`, `alt="Trang Từ vựng của TCT English: mục tiêu hôm nay, từ cần ôn và lịch ôn 7 ngày"`). The frame is tilted with `perspective` + `rotateX(14deg)` (8deg on phones), has the glow shadow, and fades into the background at the bottom with a gradient overlay.
- **Floating chips** (white cards, decorative, `aria-hidden`): "Chuỗi 5 ngày / Đừng để đứt hôm nay" with `streak-flame-clay.svg`; "negotiate / Ôn lại sau 7 ngày"; "+25 XP / Xong bài 3" with `badge-trophy-clay.svg`; mascot + "“Deadline” khác “due date”… / Trợ lý AI đang trả lời". On phones show the first two only. Chips must never cover the buttons or leave the viewport (no horizontal scroll at any width).

### 5.6 "Học thử" demo — `#hoc-thu`
Left column: `h2` "Thử một câu trước khi đăng ký."; lead "Ba câu hỏi từ vựng, trả lời ngay trên trang này. Không cần tài khoản."; three ticks: "Hệ thống tự chấm, bạn chỉ cần chọn đáp án", "Mỗi từ đúng được xếp ngày ôn lại", "Dùng phím 1 đến 4 để trả lời nhanh".

Right column: white card.
- Header "Học thử · câu n/3" and chip "Từ mới".
- Prompt line and big word; four option buttons with number hints.
- Questions (fixed order; options shuffled once per load with a seed so tests are deterministic):
  1. "Từ nào có nghĩa này?" — **đàm phán** → `negotiate`; others `accomplish`, `schedule`, `confirm`.
  2. "Từ này nghĩa là gì?" — **deadline** → `hạn chót`; others `cuộc họp`, `hợp đồng`, `báo cáo`.
  3. "Từ nào có nghĩa này?" — **cộng tác** → `collaborate`; others `compete`, `complain`, `calculate`.
- After an answer: options lock; the chosen and the correct option are marked with icon + text, not colour alone.
  - correct: "Đúng rồi!" and "Ôn lại sau 1 ngày";
  - wrong: "Chưa đúng" + "Đáp án: {đáp án}" and "Ôn lại sau 10 phút".
  - Button "Câu tiếp theo" (last: "Xem kết quả"), focused after answering; `Enter` activates it.
- Result: "Bạn đúng x/3 câu." + "Tạo tài khoản để lưu tiến độ và học tiếp." + primary "Tạo tài khoản miễn phí" → `/Account/Register` + text button "Làm lại".
- Keys `1–4` answer only while focus is inside the card. Feedback goes to an `aria-live="polite"` region. The options are a group with a label; each option is a real `<button>`.
- Without JS the card shows question 1 with its options as plain text and a link "Bắt đầu miễn phí"; no dead buttons.
- No network, cookies or storage.

`landing-core.js` (pure, node-tested): `createDemo(questions, seed)`, `current(state)`, `answer(state, optionId)` → `{ correct, correctId, label }`, `next(state)`, `summary(state)`, `reset(state)`. `landing.js` renders with `textContent` / DOM nodes only (no `innerHTML` with data).

### 5.7 Features — `#ky-nang`
`h2` "Mọi thứ bạn cần để học đều nằm ở đây."; lead "Không phải lắp ghép nhiều ứng dụng. Một tài khoản, sáu kỹ năng, một mục tiêu mỗi ngày."

Glass cards (surface, line, radius lg):
1. Wide: `h3` "Bài học từ vựng ba bước" — "Làm quen, luyện tập, xem kết quả. Hệ thống tự chấm, bạn không phải tự đánh giá mình." + `lesson-card-*.webp` bleeding off the bottom-right (`loading="lazy"`, `alt="Thẻ từ Please trong bài học từ vựng"`).
2. `id="on-tap"`: `h3` "Lịch ôn riêng cho từng từ" — "Trả lời đúng, khoảng cách ôn dài dần cho đến khi thuộc hẳn. Quên thì quay về bậc đầu." + the ladder: six rising bars "1, 3, 7, 14, 30, 60" each with "ngày", the last in the accent colour. Mark up as an ordered list with a visually hidden sentence "Khoảng cách ôn: 1, 3, 7, 14, 30 rồi 60 ngày."; bar heights from `--lp-h`.
3. Four small cards: "Nghe" — "Video A1 đến C2, phụ đề song ngữ, chép chính tả."; "Nói" — "Nhại theo từng câu, xem điểm phát âm ngay."; "Viết" — "AI chấm bài và chỉ ra chỗ cần sửa."; "Đọc" — "Bài đọc ngắn kèm câu hỏi kiểm tra."
4. "Mục tiêu, chuỗi ngày học, huy hiệu" — "Đặt số từ mỗi ngày và giữ chuỗi. Học cùng bạn bè trong lớp học nhóm." + three small tiles using the existing clay SVGs: "Chuỗi ngày học", "Mục tiêu mỗi ngày", "Huy hiệu" (labels only, no invented counts).
5. `id="tro-ly-ai"`: "Trợ lý AI trả lời bất cứ lúc nào" — "Giải thích từ, sửa ngữ pháp, đặt ví dụ theo đúng tình huống của bạn." + a static two-bubble exchange: user "“Deadline” và “due date” khác nhau thế nào?"; assistant "**Deadline** là hạn chót, trễ sẽ có hậu quả. **Due date** chỉ là ngày đến hạn." Marked as an example (`aria-label="Ví dụ hội thoại với trợ lý AI"`).

Verify each claim against the product at N0 (CEFR range of listening lessons, pronunciation scoring, AI grading, reading questions, class groups). Reword or drop any claim that is not true today; record it.

### 5.8 FAQ
`h2` "Câu hỏi thường gặp"; five `<details>` (first open):
1. "TCT English có miễn phí không?" — "Có. Tài khoản miễn phí học được từ vựng, nghe, nói, đọc, viết và dùng trợ lý AI. Gói Premium mở thêm luyện viết với AI không giới hạn, dịch lời thoại và tạo quiz bằng AI cho bài nghe."
2. "Mỗi ngày cần học bao lâu?" — "Khoảng 15 phút: ôn các từ đến hạn, học vài từ mới hoặc làm một bài nghe. Mục tiêu mặc định là 10 từ mỗi ngày."
3. "Lịch ôn tập hoạt động thế nào?" — "Mỗi từ có một bậc nhớ. Trả lời đúng, từ lên một bậc và khoảng cách ôn dài dần: 1, 3, 7, 14, 30 rồi 60 ngày. Quên thì từ quay về bậc đầu."
4. "Tôi mới bắt đầu thì học được không?" — "Được. Khóa từ vựng và bài nghe có từ trình độ A1; bạn chọn bài theo trình độ của mình."
5. "Có cần cài ứng dụng không?" — "Không. TCT English chạy trên trình duyệt của máy tính và điện thoại."

Check answers 1, 2 and 4 against `Views/Billing/Pricing.cshtml`, the access rules and the content before writing them; adjust wording to what is true.

### 5.9 Final call to action
Rounded panel with the primary→violet gradient: `h2` "Học 5 từ đầu tiên ngay tối nay."; "Tài khoản miễn phí. Bài đầu tiên mất khoảng 3 phút."; yellow button "Bắt đầu miễn phí" → `/Account/Register`. Signed in: button "Vào trang học".

### 5.10 Footer
Four columns (two on phones): brand "TCT English" + "Học từ vựng, nghe, nói, đọc và viết tiếng Anh theo bài ngắn, có lịch ôn tập tự động." (+ the D2 sentence only if D2 = GIỮ); **Kỹ năng**: "Từ vựng" `/Vocabulary`, "Nghe" `/Home/Listening`, "Nói" `/Speaking`, "Viết" `/Home/Writing`, "Đọc" `/Home/Reading` (confirm each route and that an anonymous click lands on login and returns to the page afterwards; otherwise link to `/Account/Login`); **Tài khoản**: "Đăng nhập", "Tạo tài khoản", "Gói Premium" `/Billing/Pricing`; **Thông tin**: "Giới thiệu", "Liên hệ", "Quyền riêng tư", "Điều khoản" (F11). Bottom line "© 2026 TCT English". No social icons (F3) unless real URLs exist in the repo.

### 5.11 Screenshots (N4, N6)
- Capture from the running local app at 1440×900, device scale factor 2, signed in as a synthetic learner whose Vocabulary home has some progress (create it by doing a lesson through the UI; no SQL beyond what plan `2026-11-14` allowed; no reset).
- Hero crop: the Vocabulary home content area (goal banner, memory panel, 7-day forecast), without the sidebar, top bar, user name, avatar or the AI launcher. Lesson crop: the word card of step 1.
- No email, real name or personal data in any image.
- Export WebP through a canvas in the browser (`toBlob('image/webp', 0.82)`), no new tooling: hero 2240 w ≤ 220 KB and 1120 w ≤ 90 KB; lesson 1560 w ≤ 120 KB and 780 w ≤ 50 KB. `og-landing.png`: a 1200×630 screenshot of the finished hero ≤ 300 KB.
- If the screens change later the images go stale; add that to known-issues as a maintenance note.

### 5.12 Motion
- One load sequence in the hero, total ≤ 900 ms: pill, heading, sub and buttons fade up 12 px in order; the frame eases from `rotateX(22deg)` and 0.6 opacity to its resting tilt; chips pop in last. CSS only, started by a class the script adds after first paint; without JS everything is already in its final state.
- Chips drift ±6 px on a 6–8 s loop. Buttons and cards respond to hover/press.
- No scroll-triggered reveals. Under `prefers-reduced-motion: reduce`: no sequence, no drift, no smooth scroll.

### 5.13 Responsive
- ≥ 1200: as mockup. 900–1199: demo and feature grids keep two columns, small cards two per row. < 900: single column, nav collapses, hero buttons stack full width, frame tilt 8deg and image height capped, chips reduced to two, ladder bars narrower, footer two columns.
- No horizontal scroll from 320 px to 1920 px and at 200% zoom. The fixed AI launcher must not cover a primary button; give the final CTA and the demo card bottom clearance on phones.

### 5.14 Performance budgets (G16)
`landing.css` ≤ 30 KB, `landing.js` + `landing-core.js` ≤ 14 KB unminified; landing-owned images transferred on first view ≤ 350 KB at 1440 and ≤ 150 KB at 390; no request to a host the old page did not already use; no layout shift from images (dimensions set); the `h1` paints without waiting for the web font.

---

## 6. Accessibility
- Landmarks, one `h1`, ordered headings, skip link, visible focus on dark, logical tab order (nav → hero buttons → demo → cards → FAQ → CTA → footer).
- Contrast: text ≥ 4.5:1, large text and UI parts ≥ 3:1, on every surface (dark, glass, white card, yellow button, gradient panel) — scripted.
- Decorative images `alt=""`/`aria-hidden`; product images with the alt texts above.
- Demo: full keyboard path, live region, no colour-only states.
- Forced colours: borders on cards and buttons, solid text, images visible, ladder readable.
- 200% zoom and 320 px: nothing clipped, no overlap.

---

## 7. Tests

### 7.1 `LandingPageTests.cs`
Anonymous `GET /Home/Landing` → 200; contains the `h1` text, the meta description and `og:title`; links `landing.css`; has no `href="#"` and no `/Account/Login` on any "Bắt đầu miễn phí" / "Tạo tài khoản" link (they point to `/Account/Register`); contains the three demo questions' words for no-JS; contains no `cdn.jsdelivr.net` reference (when Bootstrap was removed); anonymous `/` still redirects to the landing page; a signed-in request shows "Vào trang học" and no "Đăng nhập".

### 7.2 `test_landing_core.cjs`
Deterministic shuffle by seed; correct and wrong answers; second answer to the same question ignored; `next` past the end; summary counts; reset; every question has exactly one correct option and four distinct options.

### 7.3 Sweep fixes
One regression test per fix, written first.

---

## 8. Phases

Run mode: continuous, autonomous, one agent. After each green gate: write the gate record (8.3) into section 12, commit locally, start the next phase.

| Phase | Work | Read | Mockup |
| --- | --- | --- | --- |
| **N0 Baseline** | Clean-tree check (leave the unrelated blank-line change in `appsettings.Development.json` and `Claude outputs/` unstaged); `git switch master`; verify `22c53ab` or later; create `feature/landing-night`; build; full and node tests (record counts, warnings, known failures); save this plan and the prompt; read D1–D3; confirm F1–F11; find tests that assert landing strings; launcher dependencies; routes for footer links; product facts for 5.7 and 5.8; start the app (`scripts/start-local.ps1`); before-screenshots of the landing page at 1440, 1366, 390 into `.tmp/landing/before/` | 0–4 | all |
| **N1 Sets fix** | 4.1 | 4.1 | — |
| **N2 Bug sweep** | 4.2 | 4.2 | — |
| **N3 Landing foundation** | New `landing.css`/`landing.js`/`landing-core.js` skeleton; head (5.1); tokens (5.2); page structure, background, skip link, nav (5.3–5.4); remove the inline style block and old script; `LandingPageTests` first | 5.1–5.4, 7.1 | D-full, D-mobile |
| **N4 Hero** | Copy, buttons, screenshot capture, frame, chips (5.5, 5.11) | 5.5, 5.11 | D-full top |
| **N5 Demo** | Core + node tests first, then UI (5.6) | 5.6, 7.2 | D-full "Thử một câu" |
| **N6 Features** | Cards, ladder, AI example (5.7) | 5.7 | D-full middle |
| **N7 FAQ, CTA, footer** | 5.8–5.10 | 5.8–5.10 | D-full bottom |
| **N8 Motion, responsive, accessibility, performance** | 5.12–5.14, 6; `og-landing.png` | 5.12–5.14, 6 | D-mobile |
| **N9 Docs** | Tracker rows (F9), known-issues, bug-fix-log, project-structure | 2.2 | — |
| **N10 Full gate** | Whole gate on the landing page and on every page touched by N1–N2; self-review against `AGENTS.md`, this plan's section 10; after-screenshots into `.tmp/landing/after/` | all | all |

If D1 = KHÔNG, run N0–N2, N9 (bug parts), N10 and stop.

### 8.1 Phase gate (every applicable item, in order)
| Gate | Check |
| --- | --- |
| G1 | `dotnet build`; warnings ≤ N0 |
| G2 | Area tests: `Landing*`, and the areas touched by fixes (`Set*`, `Practice*`, `Vocabulary*`, `Listening*`, `Folder*`, `Notification*`, `Billing*`) |
| G3 | Full test suite; counts not below N0 plus new tests |
| G4 | Node tests; only failures already known at N0 may remain |
| G5 | Contracts: `/` redirect, landing 200, asserted strings kept, swept routes and JSON shapes unchanged |
| G6 | Colour discipline: in `landing.css` raw colours only inside the `.lp` token block; no `:root`, no `!important`; no inline colour in the view; in other touched CSS the team tokens-only rules; `git diff --check` |
| G7 | File scope: every changed path is in 2.1 or 2.2 |
| G8 | Render at 1920×1080, 1440×900, 1366×768, 1024, 768, 390, 320 and 200% zoom; no horizontal scroll; console and network clean |
| G9 | Side-by-side with the mockup; differences recorded |
| G10 | The phase behaviour checklist (8.4) by mouse and by keyboard only |
| G11 | Accessibility (section 6) incl. forced colours, reduced motion, no-JS render |
| G12 | Scripted contrast on every new colour pair |
| G13 | Regression: AI launcher on the landing page; login and register pages reached from the buttons; `/Sets`, `/Practice`, `/Vocabulary`, `/Home/Listening` smoke |
| G14 | For files outside the landing page: Clay conformance grep (depth via `--tct-clay-*`, radius tokens) |
| G15 | Security: no `innerHTML` with data in new JS; external links have `rel="noopener"`; no credentials, mockups, screenshots or `.tmp/` in the diff; sweep created no lasting data |
| G16 | Performance budgets (5.14) measured and recorded |

### 8.2 Failure and stop rules
- A failed item: fix and rerun the **whole** gate; at most 3 attempts per item.
- Still failing and caused by this phase → revert that element, mark "deferred" with evidence, continue. Outside scope → record evidence, mark "blocked", continue with what is independent.
- **Stop the run** only if green requires a forbidden file, the base state is unsafe, data would have to be reset, or a payment/notification would really be sent.
- Never skip, weaken or delete a test; never call a skipped check passed.

### 8.3 Gate record (append to section 12 before each commit)
Phase, date, commit subject; G1–G16 each with pass / n-a / deferred / blocked and one line of evidence; decisions; mockup differences; tests changed and why; for N2 the triage table.

### 8.4 Behaviour checklists
**N1:** Standard: details open on Add, clean console on Edit; Premium: image search still works; the new test fails before and passes after.
**N2:** every route visited in each role and size; triage table complete; each fix has a failing-then-passing test; throwaway data removed; nothing paid or sent.
**N3:** page renders fully with JavaScript disabled; nav links jump to their sections with the heading not hidden under the bar; mobile menu opens, closes with `Esc`, returns focus; launcher opens and closes; signed-in state shows "Vào trang học".
**N4:** both hero buttons go to the right place; image sharp at 1440 and 390, no layout shift; chips never overlap buttons or cause scroll at any size.
**N5:** all three questions by mouse and by keys 1–4; correct and wrong feedback texts; focus lands on the next button; result counts; "Làm lại" restarts; keys do nothing when focus is outside the card; screen-reader announcements present in the live region.
**N6:** every claim verified; ladder readable as text; AI example marked as an example.
**N7:** each `<details>` toggles by mouse, `Enter` and `Space`; every footer link resolves (no 404, no `#`); CTA correct in both auth states.
**N8:** load sequence plays once and never with reduced motion; no content hidden without JS; budgets met; forced colours readable.

---

## 9. Constraints
- One agent, local only. Granted: local commits on `feature/landing-night`, one per phase, via `tct-commit-message`. Not granted: push, PR, merge, rebase, reset, deploy, remote CI.
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, working screenshots (the files in `wwwroot/images/landing/` are deliverables and are committed).
- Never reset or reseed the database; no paid AI calls; no real payment; never print or store credentials.
- Plan and code comments in English; UI copy in Vietnamese exactly as written here.

## 10. Definition of Done
- [x] F8 fixed with a regression test; known issue closed. (N1)
- [ ] Bug sweep done for every listed route and role; triage table in section 12; each fix tested; the rest reported. PARTLY: 204 page views swept and the triage table is complete, but several interactions were not driven (see "Not driven" in N2).
- [x] Landing page matches direction D on laptop and phone, with every section of 5.4–5.10. (N3–N7; differences recorded)
- [x] No inline style block; colours only through `--lp-*`; fonts and CDNs reduced as in 5.1.
- [x] Every primary action leads to registration (or "Vào trang học" when signed in); no dead links.
- [x] Demo works by mouse and keyboard, with no network use; node tests green. (369 of 370; the one failure is the known Speaking package hash)
- [x] Page is complete and readable without JavaScript, with reduced motion and in forced colours. (screenshots reviewed in N10)
- [x] Contrast script, performance budgets and all sizes recorded.
- [x] Every claim on the page is true today; no invented numbers or reviews. (checked in N6/N7; two marketing phrases are not measurable, see N10)
- [x] `/` behaviour, AI launcher and other owners' tests unaffected.
- [x] Tracker, known-issues and section 12 updated.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| Dark page feels disconnected from the light app after sign-up | Same brand blue and yellow, same product screenshots; D1 recorded for the lead |
| Launcher depends on Bootstrap | Verified at N3; local copy instead of CDN if needed |
| Screenshots go stale when the UI changes | Maintenance note in known-issues; capture steps in section 12 |
| Glows and tilt cost performance on weak phones | Gradients instead of blur filters; one transform; budgets in G16 |
| Sweep grows without end | 15-fix budget, strict "fix now" rule, everything else reported |
| A swept fix alters payment or notification behaviour | Those logics are forbidden; views and clear defects only |
| Another owner's test asserts old landing copy | Keep the asserted string on the page; record it |
| Claims drift from the product | Verification step at N0 and in N6–N7 |

## 12. Execution notes
(The agent appends discovery notes from N0, the N2 triage table and one gate record per phase here.)

### N0 — Baseline and discovery (2026-11-16) — `docs(landing): save the landing-night plan and execution prompt`
- Base: `master` at `22c53ab`; branch `feature/landing-night` created. Unrelated `appsettings.Development.json` change and `Claude outputs/` left unstaged.
- G1 pass: `dotnet build` 0 warnings, 0 errors (baseline for later gates).
- G3 pass: 1745 passed, 35 skipped (MariaDB-only), 0 failed. Baseline to keep.
- G4 baseline: node 344 / 345 pass. Known failure: `test_speaking_catalog_packages.cjs` (package hash mismatch, Speaking owner). Not touched here.
- F1–F7 confirmed in `landing.cshtml` (794 lines, inline `<style>`, three login CTAs, `#` social icons, CDN Bootstrap + FA 6.5.1, two font families, no meta tags).
- Tests asserting landing content: only status 200 (`Sprint1SmokeTests`, `CriticalFlowSqliteIntegrationTests`) and the anonymous `/Home/Index` redirect. No string needs preserving.
- Launcher dependency: `_AiChatLauncher.cshtml` uses Bootstrap utilities (`d-flex align-items-center gap-2 mb-1`), no Bootstrap JS data attributes. Decision: load the local `wwwroot/lib/bootstrap` CSS, no CDN, no Bootstrap JS unless N3 shows it is needed.
- Font Awesome in `_Layout.cshtml`: 6.0.0 (cdnjs).
- Footer routes, anonymous (live app on :5100): `/Home/Introduction`, `/Home/Termsofuse`, `/Home/Privacypolicy`, `/Home/Contact`, `/Home/Listening`, `/Home/Writing` = 200. `/Vocabulary/Index`, `/Speaking` = 302 to login with ReturnUrl. `/Home/Reading` = 302 to `/Account/Login` (no ReturnUrl). Pricing is `/Premium` (`[Authorize]`; `/Billing/Pricing` is 404). `/Account/Register` = 200.
- `/` returned 200 for an anonymous request in this probe (plan 2.4 expects landing; re-check at G5, the controller is not touched).
- Before-screenshots: `.tmp/landing/before/landing-{1440,1366,390}.png`.
- Product claims for 5.7/5.8: to be verified in N6/N7 (not yet checked).


### N1 — Sets card details for Standard accounts (2026-11-16) — `fix(sets): open the card details panel for Standard accounts`
- Root cause (F8 confirmed): `setData` in `set-card-form.js` cleared `refs.imageResults`, which exists only in the Premium markup. Fix: `if (refs.imageResults)` guard. The other image-search refs are only reached from handlers inside `if (refs.imageSearch)`.
- Test first: `scripts/tests/test_set_card_form_nonpremium.cjs` (3 tests). Before the fix the two Standard tests failed with `TypeError: Cannot set properties of null (setting 'textContent')`; the Premium control passed. After: 3/3 pass.
- G1 pass: 0 errors. Warning baseline correction: the N0 figure of 0 came from an incremental no-op build; a clean rebuild reports 58 pre-existing nullable/analyzer warnings (CS8618 ×48 etc.). N1 changed no C#.
- G2/G3 pass: 1745 passed, 35 skipped, 0 failed (equals N0).
- G4 pass: 347 / 348; the one failure is the known `test_speaking_catalog_packages.cjs` (N0 baseline 344 / 345, +3 new tests).
- G5 pass: no route, JSON or markup contract touched. G6 pass: no CSS touched; `git diff --check` flags only the unrelated blank line in `appsettings.Development.json` (not staged).
- G7 pass: files are `set-card-form.js`, the new node test, `known-issues.md`, `bug-fix-log.md` and this plan.
- G8/G10 browser (headless Chrome, synthetic accounts, app on :5100): Standard `/Sets/1/Add`: image-search block absent, "Chi tiết" offcanvas opens, console clean; `/Sets/Cards/5/Edit` console clean. Premium `/Sets/3/Add`: image-search block present, details open, console clean; `/Sets/Cards/8/Edit` clean. Image search itself was not triggered (it calls an external provider).
- G9/G11/G12/G14/G16 n-a (no visual change). G13 smoke covered by the browser run above. G15 pass: no new `innerHTML`, no credentials printed.
- Docs: known issue SETS-CARD-DETAILS-NONPREMIUM-NULL moved to resolved; bug-fix-log entry appended.


### N2 — Bug sweep (2026-11-16) — `fix(sweep): name unlabeled controls and drop the dead jQuery validation includes`

**Method.** Headless Chrome driven over DevTools (scripts in the ignored `.tmp/bug-sweep/`, outside Git). Four roles (guest, Standard, Premium, Admin) log in with the synthetic accounts, crawl the owner's modules from seed pages by following same-origin links (query strings kept; logout/delete/pay/send/export/api links excluded; at most two pages per URL pattern), and audit every page at 1440×900 and 390×844: console errors, failed requests, broken images, empty links, unnamed controls, unlabeled inputs, horizontal overflow, AI-launcher overlap. Run 1: 204 page views (guest 9, Standard 70, Premium 70, Admin 70 URLs; both sizes). The sweep submits nothing. A separate smoke then drove the primary actions on throwaway data.

**Route corrections.** `/Notification` is 404 (the page is `/Notification/Index`); pricing is `/Premium`, not `/Billing/Pricing`; Folder is `/Home/Folder` and `/Home/FolderDetail/{id}`; Practice modes are radio cards of one form (`learn, test, match, flashcard, quiz, typing, listening, cloze`), not links. The legacy `/Vocabulary/Study|Detail|FolderDetail` URLs answer an empty 404 when the required query value is missing (by design); with the query they render.

**Smoke of primary actions (Standard, throwaway data, all removed; DB check: 0 leftover rows).** Create set through the form, add three cards (batch API, the route the composer uses), set detail shows cards, edit set name through the form, create/rename/delete folder (POST with antiforgery), folder detail loads, notifications page and list API, pricing page as Standard: all pass, console clean. All eight practice modes plus HardWords load and start at both sizes with a clean console (the `test` mode start click pressed "Nộp bài"; it was run once on the synthetic account, no answers). Not driven: the card composer UI (cards added by API), a full practice round with answers, a vocabulary lesson step, listening play/pause and dictation check, marking a notification read, Premium pricing view, admin form submissions, shared-set page `/s/{slug}` (no slug known), payment result pages. These are recorded as not swept.

**Triage.**

| Id | Page / role | Symptom | Cause | Owner | Action |
| --- | --- | --- | --- | --- | --- |
| S1 | Admin Set create/edit/add-card/edit-card, Listening create/edit, Notification create | `ReferenceError: jQuery is not defined` ×2 per page | `_AdminLayout` has no jQuery; six views include `_ValidationScriptsPartial` | Trọng (views) | **Fixed**: include removed (it never ran). Test `test_owner_admin_views_sweep.cjs` |
| S2 | Folder detail (Standard, Premium) | Two three-dot buttons without a name | missing `aria-label` | Trọng | **Fixed** + test |
| S3 | Admin Set and Listening lists | Search box, two filters and the row menu button unnamed | missing `aria-label` | Trọng | **Fixed** + test |
| S4 | Admin Listening create/edit | Four speaker inputs unnamed | label not tied to the input | Trọng | **Fixed** + test |
| S5 | Admin Billing orders and premium users | Search and filters unnamed | missing `aria-label` | Trọng | **Fixed** + test |
| S6 | Admin Notification create | Title and message labels point at ids that do not exist | `asp-for` label vs overridden input id | Trọng | **Fixed** + test |
| S7 | Admin Set edit | Read-only owner field unnamed | missing `aria-label` | Trọng | **Fixed** + test |
| R1 | every page with the main layout footer | Three `href="#"` icons without a name | shared footer | layout owner (Châu) | Reported: SWEEP-FOOTER-SOCIAL-HASH-LINKS |
| R2 | Admin Speaking, Writing create/edit (inferred from source, not opened) | same jQuery error as S1 | same partial, other owners | Speaking, Writing owners | Reported: SWEEP-ADMIN-LAYOUT-NO-JQUERY |
| R3 | `/Home/Listening` | Thumbnail `9F8o8eH_Hms` returns 404 | data | content | Reported |
| R4 | Admin Set cards (set 11) | 10 sample images 404 | sample data | data | Reported |
| R5 | `/Home/Folder` | no `h1`, English tab title | page design | Trọng, redesign | Reported (D3: no redesign) |
| R6 | `/Practice`, `/Practice/learn` (390), `/Vocabulary` (390, Premium) | AI launcher overlaps a primary button | known TD-012 | layout / owners | Reported: TD-012 extended |

Fix budget: 7 of 15 used.

**Gates.**
- G1 pass: 0 errors, 58 warnings (the clean-rebuild baseline).
- G2 pass: area tests (Admin, Set, Listening, Billing, Notification, Folder) 323 passed, 10 skipped, 0 failed after the final whitespace cleanup.
- G3 pass: full suite 1745 passed, 35 skipped, 0 failed (equals N0), run before the whitespace-only cleanup.
- G4 pass: node 354 / 355; the one failure is the known 	est_speaking_catalog_packages.cjs (N1: 347 / 348, +7 new tests).
- G5 pass: no route, JSON or antiforgery change; only attributes and one script include per view.
- G6 pass: no CSS touched; no raw colour added.
- G7 pass: changed files are `Areas/Admin/Views/{SetManagement,ListeningManagement,BillingManagement,NotificationManagement}`, `Views/Folder/FolderDetail.cshtml`, the new node test and docs, all in 2.2.
- G8/G10 pass for the swept routes (see Method); the sweep re-run after S1–S6 shows the jQuery errors and those unnamed controls gone (156 of 204 page views still carry findings: R1 on every layout page, R3, R4, R5, R6). S7 was fixed after that re-run and is covered by its test only, not by a second browser pass.
- G9/G11/G12/G14/G16 n-a (no visual change). G13 pass: launcher and `/Sets`, `/Practice`, `/Vocabulary`, `/Home/Listening` load in the sweep. G15 pass: no credentials printed, no data left behind.



### N3 — Landing foundation (2026-11-16) — `feat(landing): rebuild the landing foundation in the dark "Dem hoc" direction`
- Work: `landing.cshtml` rewritten (head with title, description, Open Graph, twitter card, theme colour; skip link; sticky glass nav with phone menu; hero text and buttons; section shells for the demo and the skills cards; basic footer); new `landing.css` (scoped `--lp-*` tokens, base, buttons, nav, hero, forced-colour and reduced-motion blocks) and `landing.js` (phone menu only). Old 420-line inline `<style>`, CDN Bootstrap + FA 6.5.1, Playfair Display and the scroll-reveal script are gone.
- Tests first: `LandingPageTests` (6 tests) were red on the old page (6 of 6 failed), green after.
- G1 pass: 0 errors, 58 warnings (clean-rebuild baseline). G2 pass: landing + older landing smoke tests 53 / 53. G3 pass: 1751 passed (1745 + 6 new), 35 skipped, 0 failed. G4 pass: node 354 / 355, only the known Speaking package failure.
- G5 pass: `/` is the default route to the Landing action and returns 200; anonymous `/Home/Index` still redirects to `/` (plan 2.4 said "`/` redirects"; the real behaviour is recorded here and asserted in a test). Landing returns 200 anonymously and signed in.
- G6 pass (script): raw colours only inside the `.lp` block, no `:root`, no `!important`, no inline colour, no `<style>`. G7 pass: only `landing.cshtml`, `landing.css`, `landing.js`, `LandingPageTests.cs`, plan.
- G8 pass (headless Chrome, 31 checks): 1920×1080, 1440×900, 1366×768, 1024, 768, 390, 320 and 200% zoom (720 css px): no horizontal scroll, console and network clean. Anchors `#hoc-thu`, `#ky-nang`, `#on-tap`, `#tro-ly-ai` land below the sticky nav (target top 104 px, nav bottom 88 px). Found and fixed during the run: `.lp-top { overflow: hidden }` stopped the nav being sticky (now `overflow: clip`), and the reset `.lp p { margin: 0 }` outranked `.lp-sub`/`.lp-lead` margins (now `:where()`).
- G9 mockup comparison: nav, hero text, pill, buttons, section headings and cards match the layout of `D-full-1440.png`. Differences recorded: phone h1 is `clamp(32px, 8.6vw, 44px)` with `text-wrap: balance` (the mockup wraps "dần," alone on its own line at 44px); below 480px the nav shows only the logo and the menu button because the logo plus a CTA plus the button do not fit at 360px (the hero CTA stays); the product frame, chips and the demo are later phases.
- G10 pass: phone menu opens (`aria-expanded`, "Đóng menu"), `Esc` closes and returns focus; links close the panel; skip link present; launcher opens and closes with `Esc` for a signed-in learner and the trigger is present for a guest.
- G11: no-JS render checked from the server HTML (hero, nav links, buttons are markup; the phone menu falls back to the wrapped row because `lp-js` is only added by an inline one-line script); forced-colour and reduced-motion screenshots taken (`.tmp/landing/n3/`), not inspected pixel by pixel. G12 pass: all 23 pairs ≥ 4.5 (text) or ≥ 3 (large text and UI parts); lowest is white on primary at 5.28. G13 pass: launcher works; the login and register pages are the nav and CTA targets. G14/G16 n-a (no non-landing CSS; page weight measured in N8). G15 pass: no `innerHTML`, `eval`, new external host, no secrets.
- Decisions: Bootstrap CSS comes from the local `lib/bootstrap` (the launcher panel uses `d-flex align-items-center gap-2 mb-1`); no Bootstrap JS. The pill mascot is `tct-ai-mark.svg` (4.5 KB, already used by the launcher), not the 498 KB `tct-ai-launcher.png`. `og:image` and `landing-core.js` arrive in N8 and N5.


### N4 — Hero (2026-11-16) — `feat(landing): add the real product shot and floating chips to the hero`
- Work: tilted browser frame holding a real screenshot of the Vocabulary home, four decorative chips, bottom fade into the page. Images captured from the running app (headless Chrome, DPR 2, synthetic Premium learner with progress) and exported to WebP through a canvas: `hero-home-1120.webp` (1110 × 655, 42.6 KB) and `hero-home-2240.webp` (2220 × 1310, 94.7 KB); `lesson-card-780.webp` and `lesson-card-1560.webp` (816 and 1632 px wide, 12 KB and 29 KB) are captured now for N6. File names keep the plan's labels; the real widths are 1110 / 2220 and 816 / 1632.
- Capture crop: the content area only (goal banner, memory panel, 7-day forecast): no sidebar, top bar, user name, avatar, e-mail or AI launcher (the launcher was hidden in the page before the shot). The lesson crop is the step-1 question (progress bar, step pills, word, four options, key hints). No personal data in any image.
- Test: `Landing_Hero_ShowsTheRealProductShotWithSizesAltAndDecorativeChips` added with the markup in the same step (it was not run red on its own; the page had no hero image before this phase).
- G1 pass: 0 errors, 58 warnings. G2 pass: 7 landing tests. G3 pass: 1752 passed (1745 + 7), 35 skipped, 0 failed. G4 pass: node 354 / 355 (known Speaking failure only).
- G5 pass. G6 pass (script): colours only in the `.lp` block (new tokens: frame, chip shadow). G7 pass: `landing.cshtml`, `landing.css`, `LandingPageTests.cs`, `wwwroot/images/landing/*`, plan.
- G8 pass (32 checks, headless Chrome, DPR 1 to 3): at 1920×1080, 1440×900, 1366×768, 1024, 768, 390 and 320 the image is sharp (file pixels ≥ layout pixels × min(DPR, 2)), no horizontal scroll, layout shift 0.000, console and network clean. Both hero buttons go where specified (primary opens `/Account/Register`, glass goes to `#hoc-thu`).
- G9 mockup: frame, tilt, glow and chip placement follow `D-full-1440.png` and `D-mobile-390.png` (phone: two chips, image cropped to 300 px, tilt 8°). Differences recorded: (a) chip texts changed from the plan: "Chuỗi ngày học / Đừng để đứt hôm nay" instead of "Chuỗi 5 ngày" (the screenshot right beside it shows 2 days, and no figure may be invented); "Huy hiệu mới / Nhịp từ vựng" instead of "+25 XP / Xong bài 3" (the XP award for a lesson was not verified; "Nhịp từ vựng" is a real badge name from the app); (b) the AI chip uses `tct-ai-mark.svg`, the same mascot the launcher loads, not the 498 KB PNG; (c) below 1280 px the chips sit inside the frame edges so none leaves the viewport.
- G10 pass: chips do not overlap the nav, pill, heading, subtitle or buttons at any size tested (rectangle intersection test) and stay inside the viewport. G11: image `alt` as specified, chips `aria-hidden`; forced-colour and reduced-motion screenshots taken. G12 pass (all pairs, chip text on white 17.1 and 6.29). G13 pass: launcher still opens and closes. G14 n-a. G15 pass.
- G16: `landing.css` 15.1 KB (budget 30), `landing.js` 1.7 KB; first-view landing-owned images: 1x screens load `hero-home-1120.webp` (43.6 KB), 2x and phones load `hero-home-2240.webp` (96.9 KB), both under the 350 KB / 150 KB budgets; dimensions are set, `fetchpriority="high"`, `h1` paints without waiting for the web font (system fallback stack).


### N5 — "Học thử" demo (2026-11-16) — `feat(landing): add the client-only "hoc thu" demo of three questions`
- Work: pure core `landing-core.js` (seeded option shuffle, `createDemo`, `current`, `answer`, `next`, `summary`, `reset`, `keyOptionIndex`), the UI in `landing.js` (text and element nodes only), the card and copy in `landing.cshtml`, styles in `landing.css`. Question 1 is also server-rendered as plain text with a real "Bắt đầu miễn phí" link, so the card works without JavaScript and has no dead buttons.
- Tests first: `scripts/tests/test_landing_core.cjs` (15 tests) was written before the core existed (it could not load until the core was written; it was not run red on its own). It also guards the no-JS markup against drifting from question 1. xUnit: two more landing tests for the no-JS card (words, option group label, live region, no `<button>` or `<form>`, scripts deferred in order) and for the signed-in demo link. Plan 7.1 asked for "the three questions' words" in the no-JS markup; plan 5.6 says the no-JS card shows question 1 only, so 5.6 won and only question 1 is asserted.
- G1 pass: 0 errors (incremental build; clean-rebuild baseline stays 58). G2 pass: landing tests 9 / 9. G3 pass: 1754 passed (1745 + 9), 35 skipped, 0 failed. G4 pass: node 369 / 370 (known Speaking failure only; 355 + 15 new).
- G5 pass. G6 pass (script). G7 pass: `landing.cshtml`, `landing.css`, `landing.js`, `landing-core.js`, the two test files, plan.
- G8/G10 (headless Chrome, 20 checks): mouse and keyboard-only runs; "Đúng rồi!" with "Ôn lại sau 1 ngày"; "Chưa đúng" with "Đáp án: …" and "Ôn lại sau 10 phút"; chosen and correct options both marked by icon, hidden text and colour; a second click is ignored; focus lands on "Câu tiếp theo", then on the first option, then on the score; "Xem kết quả" on the last question; result "Bạn đúng 2/3 câu." and "3/3 câu." with the register link; "Làm lại" restarts; keys 1 to 4 do nothing while focus is outside the card; options reshuffle between loads (4 orders in 6 loads); no new network request and no storage or cookie written; phone: single column, options 56 px high, no horizontal scroll; console clean.
- G9 mockup: layout, white card, 32 px radius, glow and tick list match `D-full-1440.png`. Differences: the correct-option outline is `--lp-success-strong` (the mockup's lighter green measured 2.54:1 against the white card, below 3:1 for a UI part); a "Câu tiếp theo" button sits under the feedback (needed for keyboard and screen-reader flow).
- G11: focus ring on the white card is the primary blue (3 px) because the page-wide yellow ring is 1.5:1 on white; live region `aria-live="polite"`; option group labelled; forced-colour block gives the card, options and feedback a system-colour border. G12 pass (all pairs, including primary-ink on primary-soft 6.84, success-strong on success-soft 5.21, danger on danger-soft 5.91). G13: demo CTA goes to `/Account/Register`, signed-in to `/Home/Index`. G14 n-a. G15 pass: no HTML strings in JS, no storage, no network.
- G16: `landing.css` 19.7 KB (budget 30), `landing.js` + `landing-core.js` 13.1 KB (budget 14); the first version was 14.3 KB, so the DOM code was rewritten around one small builder helper.
- Note: after the final `landing.js` rewrite only the demo checks (20 / 20) were re-run in the browser; the phone menu and the common checks are re-run at the N6 gate. A first N5 test run was stopped by the host for low memory and was repeated; the figures above come from the repeat.


### N6 — Skills section (2026-11-16) — `feat(landing): add the skills cards, the review ladder and the AI example`
- Work: lesson card with the real step-1 screenshot (lazy, `srcset`), the 1 → 60 day ladder (ordered list with a spoken summary, bar heights through `--lp-h`), four skill cards, goal tiles with labels only, and a static two-bubble AI example marked as an example. The nav anchors `#on-tap` and `#tro-ly-ai` now land on these cards.
- Test first: `Landing_Features_…` was run red (1 failed) before the markup, then green.
- **Claim check (plan 5.7 / 5.8 against the product, 2026-11-16).** Verified true and kept: Vocabulary lessons with new words have the steps Làm quen, Luyện tập, Kết quả (`Views/Vocabulary/Study.cshtml`; review lessons have two); grading is automatic; Listening has levels A1 to C2, dictation ("Chính tả") and quizzes; Speaking has per-sentence shadowing and a pronunciation score; Reading pages have question lists; class groups, daily goals, streaks and badges exist; the 1, 3, 7, 14, 30, 60 day intervals are the scheduler's. Reworded or dropped because not true today: (1) "phụ đề song ngữ" → "xem lời thoại" (the Listening page offers the transcript; translation is a Premium feature); (2) "AI chấm bài và chỉ ra chỗ cần sửa" → "Viết câu tiếng Anh, nhận góp ý về nghĩa, ngữ pháp và cách dùng từ" (`WritingAssessmentPolicy` is a local assessment, not AI); (3) "sáu kỹ năng" → "năm kỹ năng" (the sidebar has Từ vựng, Nghe, Đọc, Viết, Nói); (4) "Bài đọc ngắn" → "Bài đọc" (length not verified); (5) "sửa ngữ pháp" in the AI card → "ngữ pháp" and "đúng tình huống" → "tình huống" (capability not verified beyond Q&A). A guard test fails if the dropped phrases return. Still to verify for the FAQ in N7: free/Premium split, default goal, A1 content.
- G1 pass: 0 errors. G2 pass: 10 landing tests. G3 pass: 1755 passed (1745 + 10), 35 skipped, 0 failed. G4 pass: node 369 / 370 (known Speaking failure only).
- G5 pass. G6 pass (script; the only inline style is `--lp-h` on six ladder items, asserted by a test). G7 pass.
- G8/G10 (headless Chrome, 31 checks on the section plus the 31 common, 32 hero and 20 demo checks re-run after the JS rewrite, all pass): 1920×1080, 1440×900, 1366×768, 1024, 768, 390 and 320: the lazy lesson image loads when scrolled to, ladder bars rise from 1 to 60 (30 to 210 px), eight cards stay inside the viewport, no horizontal scroll, console and network clean; skill cards 4 per row at 1280+, 2 per row below; nav anchors land under the sticky nav.
- G9 mockup: matches the middle of `D-full-1440.png` (lesson card with the bleeding shot, ladder with the yellow top bar, four icon cards, goal tiles, AI bubbles). Differences: goal tiles carry labels only (the mockup showed "5 ngày", "6/10 từ", "3 huy hiệu"); the lesson shot is cropped to 240 px so both top cards have the same height; copy changes as listed above.
- G11: ladder is readable as text (list plus a visually hidden sentence); the AI example is a labelled group; decorative icons `aria-hidden`; the skill-icon tiles are decoration. G12 pass: tile icons ≥ 5.8, "ngày" on the blue bars 6.40, on the yellow bar 6.26, white 24 px numbers on the bar top 3.47 (large text, minimum 3). G13 pass (launcher, login and register pages). G14 n-a. G15 pass.
- G16: `landing.css` 23.5 KB (budget 30); `landing.js` + `landing-core.js` 13.1 KB (budget 14); the lesson image is lazy: 12 KB at 1x and 29 KB at 2x.


### N7 — FAQ, final call to action, footer (2026-11-16) — `feat(landing): add the FAQ, the final call to action and the new footer`
- Work: five native `<details>` (first open; a CSS plus/minus icon, no font dependency), the gradient final call to action, and a four-column footer (brand, Kỹ năng, Tài khoản, Thông tin) with real links and no social icons (F3 closed). D2 = BỎ: the footer text no longer mentions company staff.
- Tests first: three new landing tests were run red (3 of 3 failed) before the markup, then green.
- **Claim check, continued.** Verified and kept: A1 content exists (the Vocabulary home filter and the Listening levels start at A1); the review intervals; the 10-word daily Vocabulary target is a constant (`DailyTargetWords = 10`); a Standard account can use the AI assistant with a daily allowance (15 messages); private Speaking videos, AI-generated writing exercises and Listening transcript translation and AI quiz are Premium (`Pricing.cshtml`, `WritingService.Generation.cs`, `SpeakingController`); the app is a responsive website. Reworded: (1) "Mục tiêu mặc định là 10 từ mỗi ngày" → "Mục tiêu từ vựng mỗi ngày hiện là 10 từ" (it is a fixed target, not a default); (2) FAQ 1 no longer says Premium gives "luyện viết với AI không giới hạn" (generation is capped at 5 a day) and says what Premium actually adds; (3) "Khóa từ vựng và bài nghe" → "Từ vựng và bài nghe"; (4) final call to action "Học 5 từ đầu tiên ngay tối nay. / Bài đầu tiên mất khoảng 3 phút." → "Học những từ đầu tiên ngay tối nay. / Tài khoản miễn phí, học ngay trên trình duyệt." (neither "5 words" nor "3 minutes" for a first lesson could be verified).
- Footer links (F11, checked on the running app): Giới thiệu, Liên hệ, Quyền riêng tư, Điều khoản, Nghe and Viết return 200 for a guest; Từ vựng, Nói and Gói Premium redirect to login with a `ReturnUrl`; `/Home/Reading` redirects to login without one, so "Đọc" links to `/Account/Login?returnUrl=%2FHome%2FReading` for a guest (a signed-in learner gets `/Home/Reading`). Signed in, the account column shows "Vào trang học" instead of login and register.
- G1 pass: 0 errors. G2 pass: 13 landing tests. G3 pass: 1758 passed (1745 + 13), 35 skipped, 0 failed. G4 pass: node 369 / 370 (known Speaking failure only).
- G5 pass. G6 pass (script). G7 pass.
- G8/G10 (headless Chrome, 16 checks plus the 31 common, 32 hero, 20 demo and 31 skills checks, all pass): FAQ opens and closes by mouse, `Enter` and `Space`, the first stays open when another opens, the icon switches between plus and minus, every answer is real DOM text while closed; 14 footer and final-CTA links all resolve (no 404, none `#`); guest clicks on gated skills redirect to login keeping the way back; the final CTA is "Bắt đầu miễn phí" → `/Account/Register` for a guest and "Vào trang học" → `/Home/Index` signed in; phone: footer in two columns, links 44 px high, FAQ rows 55 px, no horizontal scroll, final CTA has 56 px bottom clearance for the AI launcher.
- G9 mockup: matches the bottom of `D-full-1440.png` and `D-mobile-390.png`. Differences: copy changes above; the FAQ plus/minus is drawn with CSS instead of Font Awesome; the footer links are 44 px high (the mockup's are shorter).
- G11: native keyboard behaviour for the FAQ, footer navs are labelled, one `h1` and ordered `h2`s; forced-colour borders on the new blocks. G12 pass: one pair failed first (end-of-page text on the blue half of the gradient measured 4.35:1); the token `--lp-end-text` moved from `#e4e8ff` to `#f5f6ff` (4.91 and 5.29). G13 pass. G14 n-a. G15 pass.
- G16: `landing.css` 25.8 KB (budget 30), JS 13.1 KB (budget 14).


### N8 — Motion, accessibility, performance, share image (2026-11-16) — `feat(landing): add the load motion, the share image and the accessibility pass`
- Work: the hero load sequence and the chip drift (pure CSS, only inside `@media (prefers-reduced-motion: no-preference)`), hover on the skill cards, `og:image` and `twitter:image` tags with a 1200 × 630 `og-landing.png` captured from the finished hero (no personal data), a forced-colour tweak for the nav.
- Design choice: the plan said "started by a class the script adds after first paint". The sequence is driven by the `lp-js` class that the one-line script in the page head adds, so a script that fails to load can never leave the hero hidden (the old page went blank in that case, F5). Without JavaScript no class is added and every element is already in its final state. Chip tilt uses the individual `rotate` property, so the pop-in (`scale`) and the drift (`translate`) never overwrite it.
- Tests: `Landing_ShareImage_…` was run red before the image existed (1 failed), then green; `Landing_Stylesheet_KeepsTheMotionOptIn…` is a guard that was already true when written (every `@keyframes` is inside the no-preference block; no `!important`; forced-colour and reduced-motion blocks present; no reveal classes, no `IntersectionObserver`).
- G1 pass: 0 errors. G2 pass: 15 landing tests. G3 pass: 1760 passed (1745 + 15), 35 skipped, 0 failed. G4 pass: node 369 / 370 (known Speaking failure only).
- G5 pass. G6 pass (script). G7 pass: only landing files and the new `og-landing.png`.
- G8/G10/G11 (headless Chrome, 17 N8 checks, all pass after fixes; the 31 common, 32 hero, 20 demo, 31 skills and 16 FAQ/footer checks re-run on the final CSS, all pass): the sequence is 9 one-shot animations ending at 900 ms; four chips drift on 6, 6.5, 7 and 8 s loops and stay within ±6 px; after the sequence everything is fully visible; scrolling away and back replays nothing; with reduced motion there are no animations and scrolling is not smooth, and every hero part is visible at once; 33 keyboard tab stops from the skip link through nav, hero, demo, FAQ, final call to action and footer, each with an outline of 2 px or more; forced colours: heading text solid, borders on cards, buttons, quiz and ladder, images visible (screenshots reviewed).
- Issues found and fixed in this phase: the sequence first ended at 1050 ms (chip delays), now 900 ms; the share image first came out at 318.8 KB, now 289.9 KB after a lossless re-deflate at zlib level 9 (budget 300 KB); my first Tab-order check could not fail (`asc || true`) and was corrected before the figures above were taken.
- G9: no layout change from N7 (full page compared with `D-full-1440.png` and `D-mobile-390.png`). G12 pass (all pairs; the new forced-colour rule uses only system colours). G13 pass. G14 n-a. G15 pass.
- G16 measured (fresh browser per case, so the image is chosen for the screen from the first request): landing images on first view 43 KB at 1440 × 1x, 95 KB at 1440 × 2x, 43 KB at 390 × 3x (budgets 350, 350, 150 KB); `landing.css` 27.4 KB (budget 30); `landing.js` + `landing-core.js` 13.1 KB (budget 14); layout shift 0.000; the only hosts are the page's own, Google Fonts and cdnjs (the CDN Bootstrap host is gone); web font is `display=swap` over a system stack, so the `h1` paints first.
- Not observed: a real phone (checked with desktop-Chrome emulation at 390, 320 and DPR 2 and 3), Safari and Firefox, a screen reader (live region and focus order are checked structurally and by keyboard, not by listening), the share card on a real network (only the tags and the file are checked).


### N9 — Documentation (2026-11-16) — `docs(landing): record the landing work in the tracker, structure and known issues`
- Tracker (`docs/design-system/team-ui-workflow.md`): new "Landing" row (dark `--lp-*` page, branch `feature/landing-night`, not yet merged); the "Từ vựng" row corrected from "chưa duyệt trên nhánh chính" to merged (`22c53ab`, F9 closed).
- `docs/project-structure.md`: the standalone landing page and its files.
- `.ai/context/known-issues.md`: LANDING-SCREENSHOTS-STALE (maintenance note with the capture steps), LANDING-DARK-THEME-D1 (decision for the project lead), LANDING-READING-LOGIN-RETURN (the Reading page drops the return URL; the landing links around it). Sweep findings and the closed SETS issue were recorded in N1 and N2.
- `.ai/context/bug-fix-log.md`: one entry for the landing defects (login-tab buttons, dead footer icons, blank page when the script fails, stale copy).
- Gates: docs only. G3 and G4 not re-run for code (nothing compiled changed); node 369 / 370 re-run after the edits (known Speaking failure only). G6 and G7 pass: `git diff --check` clean (apart from the unrelated, unstaged `appsettings.Development.json` blank line); changed paths are docs and the plan, all in 2.2.


### N10 — Full gate and self-review (2026-11-16) — `docs(landing): record the final gate`
- G1 pass: clean rebuild (`--no-incremental`) 0 errors, 58 warnings (equals the clean-rebuild baseline; all pre-existing nullable and analyzer warnings). `dotnet ef migrations has-pending-model-changes` (repo-local tool 9.0.4): "No changes have been made to the model since the last migration." No file under `Models/`, `Migrations/`, `.csproj`, `Program.cs` or `appsettings*.json` is in the branch diff.
- G2/G3 pass: full .NET suite 1760 passed (N0 baseline 1745 + 15 landing tests), 35 skipped, 0 failed. G4 pass: node 369 / 370 (baseline 344 / 345 + 3 + 7 + 15 new); the one failure is the known `test_speaking_catalog_packages.cjs` (Speaking owner, predates this work).
- G5 pass: `/` returns the landing page (default route) and anonymous `/Home/Index` redirects to it; `HomeController` is untouched; landing returns 200 anonymously and signed in.
- G6/G15 pass (scripts): colours only inside the `.lp` block, no `:root`, no `!important`, no `<style>`, no HTML strings or `eval` in the landing JS; `git diff --check` clean (the only flag is the unrelated blank line in `appsettings.Development.json`, never staged). Branch diff review: 31 changed paths, every one inside plan 2.1 or 2.2; the only binary besides the WebP images is `og-landing.png` (a deliverable). No credentials, mockups, working screenshots or `.tmp/` content in the diff.
- G8/G10/G11/G13 on the final build (headless Chrome, app on :5100): 31 common checks (seven sizes plus 200% zoom, anchors under the sticky nav, phone menu, launcher open and `Esc`, signed-in variant), 32 hero, 20 demo, 31 skills, 16 FAQ/CTA/footer and 17 motion/keyboard/forced-colour/performance checks: all pass. N1 in the browser: Standard `/Sets/1/Add` details open, `/Sets/Cards/5/Edit` clean; Premium same, image-search block present. Sweep re-run for Standard and Admin (136 page views): no `jQuery is not defined`, no unlabeled control, no unnamed control except the shared footer icons; the remaining findings are exactly the reported ones (footer `#` icons, YouTube 404, Folder without `h1`, launcher overlap).
- G11 reviewed by eye: forced-colour screenshots (ladder, demo, nav), the no-JavaScript render at 390 (nav as a wrapped row, question 1 as plain text with a registration link, FAQ and footer present), reduced-motion render; contrast: every pair in the script passes (lowest white on primary 5.28; large text 3.47).
- G12 pass. G14 n-a (no non-landing CSS touched). G16: `landing.css` 27.4 KB (30), `landing.js` + `landing-core.js` 13.1 KB (14), first-view landing images 43 / 95 / 43 KB (350 / 350 / 150), share image 289.9 KB (300), layout shift 0.000.
- Self-review against `AGENTS.md`: one agent, no subagents; Git limited to local commits on `feature/landing-night`; no push, PR, merge or deployment; no schema, protected-config or shared-file change; antiforgery, ownership checks and routes untouched; no new package, font or CDN (the CDN Bootstrap and the second font family were removed); `Layout = null` page with scoped tokens, so no shared UI file changed (D1 recorded for the lead).
- Not observed: a real phone, Safari and Firefox, a screen reader (live region and focus order checked structurally and by keyboard), the share card on a real network, Standard-account and Premium checkout flows, and the interactions listed under "Not driven" in N2. Two phrases are marketing rather than measurable and were kept from the owner's copy: the hero pill "Có trợ lý AI trả lời mọi câu hỏi tiếng Anh" and the AI card title "Trợ lý AI trả lời bất cứ lúc nào" (the free allowance is 15 messages a day).
- Remaining risks: D1 (dark page versus the team's Clay language) needs the lead's decision before merge; the screenshots go stale when the Vocabulary UI changes (known-issues); `/Home/Reading` drops the return URL (known-issues).
