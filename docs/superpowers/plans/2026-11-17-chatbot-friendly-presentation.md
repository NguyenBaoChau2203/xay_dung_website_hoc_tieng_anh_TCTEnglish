# Friendly chatbot conversation, icons and a clean answer layout

Prepared: 2026-11-17. Executor: Gemini 3.8 Flash High.
Execution policy updated: 2026-11-17 for the owner's Antigravity-only exception.
This authoring turn is PLAN_ONLY. Implementation starts when the owner invokes
the [execution prompt](../../../.ai/prompts/gemini-chatbot-friendly-presentation.md).
Current branch: `fix/writing-goals-profile-polish`; HEAD:
`9bc9fc885f52de2b44c526c6c56dac71ed033a62`. The completed chatbot knowledge/CTA
implementation is currently uncommitted and is the required baseline. Preserve it.

## Owner requirements and outcome

The owner wants replies to feel cute, warm and close to the learner. Remove
technical answer labels such as "Trả lời từ dữ liệu trong hệ thống" and the
visible "Nguồn" list shown in the supplied screenshot. Keep useful learning
CTAs, improve the actual conversational wording, and add restrained friendly
icons. Apply the same presentation to full chat, the floating embedded chat,
new messages, streamed completions, loaded older messages and history reload.

Do not replace the source list with a new collapsed "Nguồn"/"Xem nguồn"
accordion or retain the routine technical footer for screen-reader users.
Keep provenance, access checking and source revalidation in the backend;
the requested change is the learner's presentation, not a weaker answer policy.

## Source context and allowed files

Read AGENTS.md, DESIGN.md, the team UI workflow and the active UI skills.
Use CodeGraph first; read bounded source only for missing/stale gaps.

| Boundary | Current implementation and intended work |
| --- | --- |
| `TCTEnglish/Views/Ai/_ChatSources.cshtml` | History renderer currently outputs state, notices, CTA nav and source list together; retain safe CTA projection and suppress routine metadata/source markup |
| `TCTEnglish/wwwroot/js/ai-chat.js` | `attachGrounding` serves HTTP/stream/older messages; `learnerStateLabel` currently returns technical labels; align presentation with Razor |
| `TCTEnglish/wwwroot/css/ai-chat.css` | Keep scoped Clay styles; remove unnecessary source-footer divider/space while styling the remaining actions/notices |
| `TCTEnglish/Services/AI/Rag/LearnerSourceLink.cs` | Keep the read-only route allowlist and labels; an optional pure icon mapping may be added without changing allowed destinations |
| `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` | Review fixed assistant introductions, transitions, guidance, suggestions, missing-data and refusal wording; edit copy only |
| `TCTEnglish/wwwroot/data/ai/website-guides.json` | Review the 64 active guides' answer bodies for a consistent voice; preserve IDs, routes, question/keyword aliases and factual capability boundaries |
| `TCTEnglish/ViewModels/AI/LearnerChatState.cs` and existing availability/notice helpers | Inspect real consumers; technical DTO labels may stay internal. Prefer a chat-local display policy over changing shared helper text used by other features |
| `scripts/tests/test_ai_chat_phase6_sources.cjs`, related chat Node/.NET tests | Adapt obsolete visible-label/source-list expectations while retaining meaningful source/access/stream/CTA checks |

Inspect current code rather than relying on old line numbers. The existing
grounding DTO carries status/outcome/freshness/routes and has server-side history
revalidation. Do not remove these fields, delete evidence, change retrieval,
retrain the classifier, add a provider call or rewrite stored conversation rows.

Local UI/copy work is allowed when the prompt is invoked. No branch creation/
switch/pull/reset/stash, commit/push/PR/CI/merge/deploy or paid/live SMTP/AI call.
Keep Program.cs, appsettings, project files, shared layouts and global tokens
protected. No schema change, shared lesson edits or data-package export is needed.

### Optional native subagents in Antigravity

Follow AGENTS.md's current owner execution policy. Only Antigravity with the
selected Gemini 3.8 Flash High model may optionally use available native
`invoke_subagent`/`define_subagent` tools. All other client/model combinations
remain single-agent. This plan does not authorize external AI chats/CLIs,
substitute models, hook/trust changes or forged model metadata. If the tools are
unavailable or a hook rejects the call, continue within the coordinating agent.
Do not claim native delegation was verified just because repository policy allows it.

Suggested division, only when useful:

- Copy worker: review or edit the already-authorized prose in
  `TemplateAnswerComposer.cs` and active FAQ bodies, and return the before/after
  copy matrix. Preserve factual values, guide IDs, routes and question aliases.
- Coordinating agent: own Razor/JS/CSS, CTA/icon presentation, shared helper
  edits, all test updates, integration and the final ledger/report.
- Optional review worker: after integration, inspect the scoped result for tone,
  accessibility and preserved behavior; report findings without concurrent edits
  or taking control of a browser already used by the coordinator.

Assign explicit non-overlapping file ownership before delegating. Subagents
inherit every task/data/configuration/Git/provider restriction; they may not
expand the task or weaken the tests. Only the coordinator operates shared
build/test output, the web server and interactive browser at a time. The
coordinator reviews each returned diff, resolves conflicts, performs final
integrated checks and reports actual delegation and remaining limits. Subagent
reports alone do not satisfy acceptance. No delegation is required to finish.

## Voice and icon specification

- Use Vietnamese "mình–bạn", familiar words, short connected sentences and
  helpful next steps. Sound like a kind study companion; keep explanations clear
  when the learner needs detail. Use "nhé"/"nha" naturally rather than ending
  every sentence the same way.
- Be warm without baby talk, excessive Gen Z slang, flirtation, guilt, pressure
  or repetitive praise. Avoid "bé yêu", claims that a learner is weak/lazy,
  and promises of guaranteed progress, perfect scores or immediate recovery.
- Answer the actual request first. Avoid repeated boilerplate about the system,
  algorithms, corpus, grounding, sources, confidence or internal provider names.
  Mention a service name only when the user asks or needs it to understand a
  real capability, for example OpenAI versus a limited internal Writing frame.
- Use a small coherent set such as 👋 🌱 📚 🎙️ ✍️ 🎧 📖 🎯 🏆 🔎. Usually one
  opening or a few meaningful section icons is enough. Do not stamp an emoji on
  every sentence, numeric metric, transcript line or error. Keep serious access/
  security/error messages calm. Do not add sarcasm or celebratory failure icons.
- CTA icons use the already loaded icon family, with visible text labels and
  decorative icons marked `aria-hidden`. No extra font/CDN/package. Native emoji
  may appear in authored assistant prose. Do not insert arbitrary HTML into
  stored text or disable Markdown/HTML sanitization to show an icon.
- Review all reply families: greetings, navigation, starter/personal suggestions,
  vocabulary/progress/class summaries, clarification, unavailable data, refusal,
  quota, provider errors and cancellation. Do not limit the change to the three
  starter buttons or add random client-side emoji to every raw response.
- Preserve exact user text, quoted lesson/transcript sentences, saved definitions,
  numeric values, names, IDs and route data. Friendly framing must not rewrite
  source excerpts or alter sentence/evidence hashes to make an answer look nicer.
  Fixed clarification/failure prose may change without changing its outcome.
- For general starter choices, say they are suggestions to begin with; avoid
  pretending they diagnose a personal weakness. Remove the long formal
  personalization disclaimer where a short, honestly framed suggestion suffices.
  Explicit requests for personal diagnosis still need an honest limitation.

### Copy examples — illustrative UI data, not invented learner facts

| Situation | Desired voice example |
| --- | --- |
| Greeting | "Chào bạn 👋 Hôm nay mình cùng luyện kỹ năng nào nhỉ?" |
| General starter | "Mình gợi ý vài lựa chọn nhẹ nhàng để khởi động hôm nay nhé 🌱" |
| Writing navigation | "Có nè ✍️ Bạn mở Writing, chọn cấp độ và dạng bài muốn luyện nhé." |
| Speaking navigation | "Mình cùng thử một câu ngắn nhé 🎙️ Bạn chọn video có transcript rồi nghe và nói theo." |
| Real personal recommendation | "Bộ **[tên bộ thực tế]** còn **[số thẻ thực tế] thẻ** cần ôn. Mình học tiếp một chút nhé 📚" |
| Ambiguous request | "Bạn muốn luyện nghe, nói, đọc hay viết? Cho mình biết kỹ năng bạn muốn thử nhé 🌱" |
| Missing lesson | "Mình chưa tìm thấy bài phù hợp. Bạn gửi thêm tên bài hoặc chủ đề để mình tìm tiếp nhé 🔎" |
| No personal progress | "Mình chưa thấy hoạt động học được ghi nhận cho phần này. Bạn có thể bắt đầu một bài rồi quay lại xem nhé." |
| Unavailable service | "Hiện mình chưa xử lý được yêu cầu này. Bạn có thể thử lại hoặc chọn một bài có sẵn để học tiếp nhé." |
| Quota reached | "Bạn đã dùng hết lượt hỏi hiện tại rồi. Mình tiếp tục khi có lượt mới nhé 🌱"; show a retry time only if actually supplied |
| Access/source unavailable | "Bài này hiện chưa mở được. Bạn chọn một bài khác nhé." |
| Out of scope | "Mình đồng hành cùng bạn trong TCT English nhé. Bạn thử hỏi về một kỹ năng hoặc cách dùng website nha 🌱" |
| Confirmed stop | "Mình đã dừng lại rồi. Khi nào sẵn sàng, bạn cứ gửi tiếp nhé." |
| Material historical-data notice | "Đây là thông tin lúc bạn hỏi. Bạn mở trang để xem cập nhật mới nhất nhé." |

Adapt these examples to the real state and body; do not paste a friendly sentence
that makes a false promise or changes a refusal/failure into success. Placeholder
brackets in the personal example are explanatory and must never render literally.

## Ordered implementation plan

1. Record current branch, dirty-file baseline, existing server/port and affected
   consumers. Review only the active chatbot ledger and symptom-related bug memory.
   Prefer the existing current-code server on5102; do not assume5100 is current.
2. Inventory the prose that learners actually see across the reply families above.
   Save a short before/after copy matrix in the new execution ledger before edits.
   Preserve factual fields and source text, and separate UI text from wire enums.
3. Decouple ordinary footer rendering from technical grounding presentation in
   both Razor and JS. Do not simply hide `.ai-chat-sources`, since it contains
   CTAs. Render useful actions without the state badge, source heading/list,
   duplicate source links or a divider/empty gap left behind.
   Where assistant-authored guidance repeats a raw navigation path, replace
   that line with a natural invitation and the validated CTA. Do not strip URLs
   from quoted lesson content or user messages with a global text replacement.
4. Keep meaningful failure/clarification/changed-data information as one concise
   friendly inline message when necessary. Avoid a second footer repeating the
   same error already in the answer. Keep live-region announcements useful.
   Preserve cancelled/unverified/revoked outcomes and their blocked actions.
5. Review and revise authored assistant copy across the active families. Cosmetic
   edits in TemplateAnswerComposer and FAQ bodies are authorized; calculations,
   personal-data retrieval, classification, security checks and quotas are not.
   If FAQ body text changes, bump the source version and review the effect on
   existing source freshness. No paid embedding build or historical DB rewrite.
6. Add a consistent small icon treatment to authored prose, skill bullets and
   CTA labels. Keep at most3 distinct safe actions, source revalidation, `_top`
   iframe navigation, keyboard names/focus, and HTML/text encoding intact.
7. Add/update meaningful behavioral tests and inspect full/embedded chat, first
   response, stream completion, refresh and older-history loading. Preserve the
   current backend checks that prove privacy and grounding even when no evidence
   UI is visible. The coordinating agent reviews the complete integrated diff
   and documents actual limits, whether or not native subagents were used.

## Verification and handoff

- Normal successful answers display no technical status label, "Nguồn" list or
  replacement disclosure; assistive technology also avoids this routine clutter.
- The same chat contains natural prose and icons with working, deduplicated CTA
  text; no lost/double icons on stream completion or history reload. Partial
  streaming, Stop, retry and stale callbacks retain their existing protections.
- Refusal, no result, quota, unavailable/cancelled and source-revoked cases remain
  honest, accessible and gentle. Hidden source lists do not authorize unsafe links.
- Check actual1440/768/375–390 widths, keyboard, long Vietnamese/English text,
  reduced motion and native zoom200% when observable. No CSS zoom substitution.
- Run the Release build and relevant chat JS/.NET tests. Where a changed string
  is the intentional design, update copy expectations while keeping behavioral
  assertions. Do not delete grounding/privacy tests or assert only screenshots.
  Old tests that require a route inside answer prose should verify the actual
  grounding destination and rendered safe CTA when that prose is intentionally
  replaced; this keeps navigation evidence without forcing technical copy.
  For a full .NET run, reuse the documented `xUnit.MaxParallelThreads=2` override
  if appropriate; do not change CI/project configuration. Existing unrelated
  Writing UI fixture failures are not part of this design task.
- No SMTP or paid AI acceptance. Ordinary tests keep their blocked/fake transports.
  Start local only with the canonical launcher and preserve existing listeners.

Write `docs/feature-audits/2026-11-17-chatbot-friendly-presentation.md` with the
copy matrix, changed files, browser/test evidence and limits. Update bug memory
for actual fixes. Report in Vietnamese, distinguishing local checks from CI and
release. Do not label unobserved checks as passed.

## Launch message

In Antigravity, select Gemini 3.8 Flash High and paste:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/gemini-chatbot-friendly-presentation.md and implement the plan locally on the current branch. Make all authored chatbot replies warm, cute and natural in Vietnamese, remove routine technical status/source UI completely, keep safe learning CTAs, and add restrained friendly icons. Preserve the existing uncommitted knowledge/CTA/backend work and all grounding/privacy protections. Follow AGENTS.md: Antigravity with Gemini 3.8 Flash High may optionally use available native subagents with non-overlapping ownership; otherwise work inline. The coordinator must integrate, review and verify full chat, the floating iframe, streaming and history reload, then report in Vietnamese. No new branch, paid AI/SMTP call, commit, push or deployment.
```
