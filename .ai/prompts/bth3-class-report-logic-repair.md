# Correct the BTH3 class report logic and diagram coverage

Activation condition: use this prompt only after the owner explicitly requests correction of the Word report. The 2026-11-09 review request itself authorized review, not this implementation.

## Outcome and evidence

Produce a corrected Vietnamese BTH3 class-diagram Word report addressing all five findings in `docs/superpowers/plans/2026-11-09-bth3-logic-review.md`. The current artifact is `C:/Users/chau1/Downloads/Nhom03_BTH3_T105_SoDoLop.docx`; its reviewed SHA256 is `5eaa0e5955ba39f3497248cc6fb433ba0fea7392e283108b7984df5dd9ff839f`. Keep this original. Suggested separate deliverable: `C:/Users/chau1/Downloads/Nhom03_BTH3_T105_SoDoLop_DaSuaLogic.docx`.

Use AGENTS.md, the documents skill and the existing artifact contract `.tmp/bth3/artifact.md`. Treat BTH1 and BTH2 as reference content. Preserve all 123 original detailed BTH1 use cases, with separately stated responsibilities. Native Word shapes/connectors are required for diagrams; only the supplied university logo may be a raster image. Preserve cover styling, heading-number periods, captions in the form `Hình N.`, contents and figure list.

## Mode and authority

Once activated by the owner's explicit correction request, use IMPLEMENT_LOCAL for the Word report and its task-local authoring/verification scripts only. Work as one agent. Inspect the current branch and preserve unrelated work. No application, persisted model, configuration, database, synchronization, Git/remote or deployment work is authorized. Existing source implementation caveats are evidence; correcting the report does not authorize fixing them.

## Ordered work

1. Read the active repair checklist and evidence in the review plan. Confirm the reviewed report has not been concurrently edited; if it has, preserve the newer file and reconcile changes. Reuse known current source; use CodeGraph first for a specific new indexed-source gap.
2. Correct the selected-writing-exercise contract so one exercise ID is provided when opening a specific exercise. Keep method declarations, input/output tables and repeated class views consistent.
3. Specify member-blocking authority and postconditions: class owner/Admin, self/owner restrictions, existing membership removal, Pending request rejection and blacklist creation. Keep kick and mute semantics distinct. Explicitly separate intended chat checks from the observed implementation caveat; do not imply a runtime fix occurred.
4. Model the internal responsibilities that record learning/time, avoid duplicate completion credit, update streak, evaluate/award badges and coordinate automatic notifications. Reuse existing analysis controls where suitable. These internal responsibilities are not newly invented user-facing use cases. Explain any infrastructure entities retained without a user-facing operation.
5. Replace all 40 identical authoring descriptions with action-specific required fields, parent/target IDs, authority, constraints, state/data changes and outcomes. Generic BanDo remains acceptable only where its required fields are specified. Audit other generic map contracts for missing identifiers and inconsistent inputs.
6. Add all six missing persistent graphical relationships listed in F1. Provide a proper Folder self association with distinct attachment points and role names. Include SpeakingVideo context for completion and WritingExercise/User context for the missing writing relationships. Split a crowded view instead of shrinking text or dropping relations.
7. Update counts and explanatory text after finalizing content. Keep original use-case counts separate from internal method counts. Repeated class views refer to the same classes, not duplicated models.
8. Use the existing bundled-runtime authoring/rendering workflow. Run the required artifact-operation marker once immediately before the first DOCX authoring command for this edit. Save the corrected DOCX separately. Update automatic fields with isolated hidden Word COM and close only the Word instance created for this task.
9. Export and render with the documents skill renderer. Inspect changed final pages directly; carry prior reviews forward only where page PNG hashes are identical. Verify all final pages through that combined review ledger and iterate on layout problems.

## Acceptance

- Every original BTH1 use-case ID remains present with a distinct actionable description; all selected entities retain correct typed attributes.
- Every declared persistent relationship is drawn at least once in the actual native diagram model. Table-only coverage is insufficient. Cardinalities, composition ends and dependency arrows agree with the stated meaning.
- The selected-exercise method contract can identify exactly which exercise is opened.
- The block-member transition accounts for membership, Pending requests and blacklist state; ordinary owner/assistant/member target rules are explicit.
- Activity/reward/notification data has identifiable processing responsibilities, including internal/background behavior where appropriate.
- None of the 40 authoring operations retains the shared generic placeholder. Specific inputs, constraints and resulting changes distinguish create/update/delete/publish/hide operations.
- The corrected DOCX is editable in Word, contains native diagrams, has no diagram raster images, has updated fields/captions/page references and is readable across every reviewed final page.
- Preserve the original artifact and references. Run document checks appropriate to the artifact; application builds/tests do not establish document correctness and are not needed without application edits.

## Vietnamese handoff

Report which logic findings were corrected, the exact output file, actual verification results and material remaining limits. Identify analysis design versus verified current implementation. Do not claim the existing application's chat or other behavior was fixed. Deliver only the corrected DOCX unless the owner asks for other artifacts.
