# Chatbot learning guidance, reviewed FAQ corpus and navigation CTAs

Mode: IMPLEMENT_LOCAL. Owner request: improve the chatbot's unhelpful no-history
answer, prepare common learner questions and provide direct learning CTAs.
Branch at start: fix/writing-goals-profile-polish. HEAD:
9bc9fc885f52de2b44c526c6c56dac71ed033a62. Working tree initially clean.
One agent; no commit/push/release, paid AI request or protected-config change.

## Findings and design

- StudyRecommendation currently returns no snippets without an eligible owned
  vocabulary set. TemplateAnswerComposer then gives the exact dead-end answer
  shown by the owner. Separate a general starter suggestion from personal facts.
- Website guides contain 41 entries, with old descriptions/routes. The live
  retriever uses keyword overlap; the corpus adapter admits only reviewed IDs.
  Maintain both boundaries when expanding the source-managed guide corpus.
- Grounding already persists and revalidates source routes for answers/history.
  Derive navigation CTAs from these allowed sources, not model-written URLs or
  a separate unverified action payload. Keep source revocation/history guards.

## Ordered checklist

- [x] Add failing regressions for Vietnamese navigation, no-history suggestions,
  curated question matching, permissions and actionable source routes.
- [x] Review current feature routes/entry points and author a broader FAQ corpus
  with common Vietnamese/accentless/English question variants. Keep original
  answer copy and evidence references. No fictional lesson IDs, prices, progress,
  scores, badge awards, response SLA or arbitrary external facts.
- [x] Expand guide/classifier coverage without weakening credential extraction,
  unscoped homework, private-source or lexical fact-admission protections.
- [x] Supply reviewed starter guide snippets for general recommendations. Keep
  actual personal recommendations preferred; no fake personalization or XP.
- [x] Admit reviewed added guide IDs into the corpus and incorporate question
  variants into searchable metadata; preserve stable IDs and source hashes.
- [x] Render at most three clear GET navigation CTAs from revalidated sources in
  full and embedded chat. Do not turn POST mutations, unsafe URLs or revoked
  sources into actions; embedded clicks must open the main application page.
- [x] Run focused/full relevant .NET and Node checks, inspect the browser when
  available, and record actual results and limitations.
- [x] Update bug memory and write a Vietnamese handoff with corpus counts,
  example questions, checks and known limits.

## Boundaries and acceptance

Reuse registered services and the existing chat/grounding contracts. Do not edit
Program.cs/appsettings/project files, retrain ML models or build paid embeddings.
New examples should work through deterministic fallback and the current runtime
classifier. No schema changes are planned. The FAQ is source-managed application
knowledge, not a supported database shared-content root; no learner/chat/secret
data is exported. Existing shared lesson packages remain unchanged.

Meaningful acceptance includes: a new user can ask what to study and receive
honest starter options with working routes; Vietnamese questions about Speaking,
Writing, Reading, Listening, Goals and other implemented pages reach the right
guide; navigation retains accessible CTAs after reload/history; cross-user and
revoked evidence still fail closed; unsupported answers stay honest. A broad FAQ
is not a guarantee that every free-form question can be answered.

Execution evidence belongs in
docs/feature-audits/2026-11-17-chatbot-learning-guidance.md. Ignored browser/test
artifacts stay under .appdata/local or .tmp. No live SMTP/AI is part of this task.
