# Expand reviewed chatbot knowledge for the current website demo

Mode: IMPLEMENT_LOCAL, one agent, current branch. Owner requests more real
knowledge files and question scenarios, including the screenshot's mixed
A1-to-C1 / TOEIC450 / IELTS4.5 learning-path question. Preserve all dirty work.
No Git publication, new branch, protected configuration/project edit, schema
change, paid AI/audio/SMTP call, real-user data export or history rewrite.

## Evidence and design

WebsiteGuideRetriever loads only website-guides.json. Its general starter path
depends on a narrow deterministic phrase check; ComposeStudyRecommendation only
accepts the three starter IDs when there is no live recommendation. A deployed
classifier may still choose StudyRecommendation for a new learning-path question,
so a relevant general guide can end in the personal-data empty response.

Add explicit, versioned includes for reviewed static JSON packs. Share asynchronous
catalog reading between the live guide retriever and corpus adapter; preserve
single-file fixtures, stable IDs, per-pack source versions, corpus admission and
revocation behavior. No wildcard loading of arbitrary files or private data.
Use bounded, platform-scoped roadmap routing before stale-model classification;
keep security/external-fact guards first, and retain actual personal facts for
personal progress and daily recommendation questions. Generic available guidance
must not require creating an owned vocabulary set.

Author topic packs for learning roadmaps, study routines, practice methods,
media troubleshooting, account/access and onboarding/demo questions, plus a
separate held-out scenario file outside wwwroot. All recommendations point to
verified existing sections, distinguish general suggestions from assessed personal
plans, and never promise exam scores or a built-in certified placement/mock test.
CEFR/IELTS/TOEIC are not presented as interchangeable scales. Verify any necessary
external assessment statements against official IELTS/ETS references.

## Checklist and acceptance

- [x] Inspect the screenshot, source routing, corpus contracts and relevant bug memory.
- [x] Write failing behavioral cases for the exact screenshot, unseen roadmap
  paraphrases, stale classifier, available general guidance, corpus pack loading
  and include paths before the repair (13 failed/2 passed). Extend identity,
  version, cancellation and revocation regressions with the integrated catalog.
- [x] Add and review the static knowledge packs, question variants and held-out
  scenarios; verify their routes, claims and normalized question identity.
- [x] Wire explicit includes into retrieval and corpus with meaningful admission,
  cancellation, corruption/revocation and security regressions.
- [x] Run relevant .NET/Node suites and Release build, review the integrated diff,
  and record exact counts and failures. Keep deterministic and blocked transports.
- [x] Verify the screenshot wording and unseen variants through the local web,
  full/embedded chat, stream and reload when available. Preserve the existing
  5100 workload; restart only the launcher started in this chat, keeping its port.
- [x] Append bug memory and an observed-results ledger plus Vietnamese handoff with coverage counts,
  actual sample answers, file inventory, gaps and observed/browser limitations.

These are application-authored static guidance files, not persisted shared lesson
entities. They ship as source; no EF migration or SQL/shared-data export is needed
unless the scope later changes to supported database content (not authorized here).

Completion evidence: docs/feature-audits/2026-11-18-chatbot-demo-knowledge.md.
Six new packs add60 guides/480 variants; combined124 eligible guides/759 variants.
Separate120 novel paraphrases run against two classifiers;12 exact non-answer
expectations include11 out-of-scope and1 insufficient-evidence result. Related
.NET690/690; final new-class rerun277/277 overlaps that count; Node26/26;
Release build success. Actual HTTP, SignalR(7/10 chunks), full/embedded history,
418px iframe/CTA and1440/768/emulated390 checked; native zoom not claimed.
The web is left on5100 with ready health and preserved existing data/secrets.
