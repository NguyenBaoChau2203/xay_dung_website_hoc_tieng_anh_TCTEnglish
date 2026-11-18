# Chatbot learning guidance and question-bank execution ledger

Date: 2026-11-17. Local branch: `fix/writing-goals-profile-polish`.
Starting HEAD: `9bc9fc885f52de2b44c526c6c56dac71ed033a62`; initially clean.
Owner authorized local chatbot/data/CTA improvement. One agent implemented,
reviewed and verified. No Git publication or release stage was performed.

## Result and maintained source

The canonical application FAQ remains
`TCTEnglish/wwwroot/data/ai/website-guides.json`, now version 2.0.0:

- 66 stored entries: 64 admitted, reviewed guides and two legacy entries still
  excluded from live retrieval/corpus (`study-read`, `ai-chatbox-usage`).
- 279 authored synthetic question examples, 278 distinct after accent/case/
  punctuation normalization. Examples are not real conversations or ML training.
- 25 added guides cover starter learning, Speaking import/transcript/microphone/
  scoring/recent videos, Writing creation/feedback/recent lessons, practice modes,
  Goals/badges, account changes and notification preferences.
- Existing stable IDs are preserved. Several outdated routes/claims were
  corrected, including `/Practice`, `/Sets`, `/Goals` and the Writing exercise
  list. Guide bodies avoid fabricated metrics, prices, content IDs or awards.

Question examples are searchable aliases in the live guide retriever and corpus
metadata. An exact reviewed question reaches its guide before a stale intent
model can misroute it. Credential/prompt extraction checks still run first;
unreviewed lesson questions retain the existing lexical/source admission path.
This is application guidance, not unrestricted question answering.

## Behavior changes

`WebsiteGuideRetriever` supplies three reviewed starter guides for a general
study recommendation. `TemplateAnswerComposer` uses them only when there is no
actual personal vocabulary recommendation. The answer explicitly calls these
general starter choices, not a diagnosis of the user's skill or weaknesses.
Existing personal recommendations remain preferred; no learner progress, XP,
goals or badges are fabricated/awarded by asking a question.

The full chat, embedded chat and server-rendered history derive up to three
navigation buttons from revalidated grounding sources. They retain normal
citations. Navigation requires a known read-only local route and current source
freshness; unavailable/unverified states cannot offer a CTA. `_top` targets open
the application's main page from an iframe. Source revocation and access checks
are unchanged. No model-written URL is promoted into an action.

The changed feature partial `_ChatSources` is consumed by full and embedded chat;
new/streamed/older client messages use `ai-chat.js`. Scoped `ai-chat.css` consumes
existing Clay tokens; global layouts, tokens and shared-site partials were not
changed. Existing IDs, antiforgery, stream/cancel and history contracts remain.

## Verification evidence

| Check | Observed result |
| --- | --- |
| Initial meaningful .NET regressions | 9 failed / 9 passed before the behavior change |
| Initial CTA behavior regression | 1 failed / 1 passed before rendering was added |
| Final focused .NET suite | 336 passed, 0 failed, 0 skipped |
| Curated aliases | All 279 examples select their intended guide through retriever AND answer service |
| Held-out questions | Vietnamese/accentless/English navigation and general starter wording tested separately |
| Source/security regression | Sensitive/unrelated requests remain ungrounded; existing RAG privacy/access/revocation cases included |
| Chat Node suite | 24 passed, 0 failed |
| Full Release suite | 1,711 passed, 0 failed, 53 opt-in/environment skips; xUnit collection concurrency limited to 2 |
| Native browser | Chrome DevTools MCP, local MariaDB web, existing synthetic local Admin |
| Exact screenshot wording | General starter response, answered outcome and three real CTAs; no owned sets on the test account |
| History reload | Same three CTAs restored by server-rendered Razor |
| Responsive | Actual inner widths 1440/768/390, no horizontal overflow; current actions fit |
| Keyboard | Actual Tab/Shift+Tab moved between CTAs; visible solid focus outline observed |
| Embedded navigation | Reloaded the saved conversation in the actual launcher iframe; clicking Writing CTA navigated the top page to the exercise list |
| Route smoke | 25 distinct FAQ destinations fetched with the local authenticated browser; all HTTP 200, no failures |
| Native zoom 200% | Not yet verified; emulated narrow widths do not prove this |

Focused command: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj
--configuration Release --filter <chat/RAG/guide/classifier/SMTP-isolation filters>`.
Exact commands/results are retained in ignored `.appdata/local/chat-learning-*.log`.
The 24 Node checks execute production chat JS, including CTA safety, duplicate
destinations, iframe targets, history sources and late/cancelled stream handling.

An additional exploratory run including `test_ai_outage_ui.cjs` produced 37
passed / 2 failed. Its two legacy Writing-creation fixtures no longer match the
already-committed Writing client contract; neither that client nor that test
file was changed by this chatbot task. This is not reported as a passing broad
Node suite. The dedicated chat suite above is green.

## Test isolation issue found during broader verification

The first full-suite attempt was stopped after
`AccountFunctionalCompletionTests.EmailChange_WithoutSmtp_DoesNotReplaceLoginEmail`
unexpectedly returned success using a Development test host that inherited local
SMTP configuration. The default factory blocked AI/audio, but did not replace
the real SMTP transport. Do not claim that initial run had no SMTP activity or
that a synthetic recipient's mailbox was observed. No credentials were printed
or changed; no intentional live SMTP acceptance was part of this task.

`TestWebApplicationFactory` now installs a blocked SMTP transport before optional
test-specific overrides. Tests explicitly providing a fake can still model
successful delivery. Two regressions cover this default and override behavior;
the original email-change-without-SMTP regression now passes. This affects test
infrastructure only; application SMTP settings/registrations remain untouched.

The interrupted full run also hit a timing-sensitive projection-deadline check.
That check passes in the focused 336-case run. A subsequent default-concurrency
full run completed with 1,704 passed / 2 failed / 53 skipped: one obsolete
empty-state assertion (updated to the intentional starter behavior), and one
grounded-source projection failure that passed in focused testing. The final
full run below passed with reduced collection concurrency. Higher-concurrency
stability is not certified, and no timeout/assertion was loosened.

## Maintaining and expanding the bank

Keep one canonical JSON source, not a second copied answer database. For a new
guide, add a stable ID, concise Vietnamese `title`/`body`, real `route`, `topic`,
specific `keywords`, distinct `questions` and a review date. Use actual source/
screen evidence; internal source paths belong in review documentation rather
than the publicly served JSON.

Admit the reviewed ID in `GuideCorpusAdapter.ReviewedIds`; choose public versus
authenticated visibility deliberately. Add an allowed navigation route only
when its GET contract has been verified, keeping server and client policies
aligned. Add meaningful held-out wording and source-access regression checks;
do not merely add exact examples until a test count is reached. Restart the
local app after changing its startup-loaded FAQ. No ML retraining or paid
embedding build is required for these guide questions.

Examples for manual testing (UI data, not agent instructions):

| Learner question | Expected destination/behavior |
| --- | --- |
| Gợi ý cho tôi học gì hôm nay? | Personal recommendation when supported; otherwise three general starter choices |
| Luyện nói ở đâu? | Speaking CTA |
| Dán link YouTube để luyện nói ở đâu? | Speaking import guidance, respecting permissions/transcript availability |
| Tìm video Speaking vừa học ở đâu? | Speaking recent-video section guidance |
| Mình muốn luyện viết tiếng Anh | Writing exercise-list CTA |
| Tìm lại bài viết gần đây ở đâu? | Writing recent-exercise guidance |
| Tạo bài Writing từ mô tả ở đâu? | Actual description-versus-paste creation flow |
| OpenAI lỗi còn tạo bài viết được không? | Honest limited internal-template fallback explanation |
| Luyện nghe ở đâu? | Listening CTA, not an invented Speaking lesson |
| Tôi muốn luyện đọc ở đâu? | Reading CTA |
| Xem các danh hiệu nhận được ở đâu? | Goals and badge conditions; no invented award |
| Xem danh hiệu trong trang cá nhân ở đâu? | Profile CTA |
| Đổi địa chỉ email ở đâu? | Account settings and verification guidance |
| Bật nhắc học hằng ngày ở đâu? | Account notification preferences; no delivery guarantee |

## Data, configuration and release limits

No persisted model/mapping changed; no migration or schema upgrade is required.
No shared lesson, package/catalog/production manifest, private learner content
or secret was exported. The FAQ is source-managed application knowledge outside
the supported database content-package roots. Program.cs, appsettings and project
files remain unchanged. No production/staging/remote CI claim is made.

The original process at port 5100 was preserved. Browser verification used an
owned Release instance at 5102, stopped while rebuilding to avoid the Windows
apphost lock. Final server/readiness status is recorded in the final update.

## Final update

- Final command: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj
  --configuration Release --logger 'console;verbosity=minimal' -- xUnit.MaxParallelThreads=2`.
  Result: **1,711 passed, 0 failed, 53 skipped, 1,764 total**, 5m36s, exit 0.
  The run rebuilt the current app/tests successfully. The runner override is
  documented by [xUnit RunSettings](https://xunit.net/docs/config-runsettings#maxparallelthreads)
  and supports the installed runner; no project/CI configuration was changed.
- Subsequent focused new/held-out chatbot suite:36/36. Final chat Node:24/24.
  The separate exploratory legacy Writing fixture failures remain outside this
  change, as recorded above; this is not a claim that every Node file is green.
- The launcher iframe was418px wide with no horizontal overflow. Its Writing
  CTA opened `/Home/Writing/Exercises` in the top page, confirmed by the real
  resulting h1. Native full-page widths1440/768/390 also had no overflow.
- Native zoom shortcuts did not change measured DPR/viewport in the automated
  browser, so200% zoom remains unverified. A screenshot file-save request was
  rejected by the browser tool's workspace-root policy; native image capture
  was used for visual inspection without bypassing that boundary.
- The current owned web is running on `http://localhost:5102`; `/health/ready`
  returned `{"status":"ready"}`. The pre-existing5100 process remains preserved.
- Application/configuration/package boundaries were reviewed. No persisted model,
  migration, package export, credential rewrite, commit, push, CI or deployment.
