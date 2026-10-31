# Migration metadata closeout — owner-authorized inline fix

Finding P2: `LearningImportSourceHash` declares discovery attributes and Up/Down,
but has no BuildTargetModel. EF therefore exposes an empty historical target
model. This violates the project's migration metadata contract even though the
explicit additive SQL can execute successfully.

Impact: metadata only; no table/column operation, data conversion, new migration,
history mutation or production action. Preserve the existing migration source
and current snapshot byte-for-byte. The final snapshot is already the model at
this last migration (pending-model check passed).

1. Add a regression in SpeakingSchemaTests proving the historical target model
   contains SpeakingImportJob, nullable varchar(64) hash and its relationships.
   Confirm it fails before the metadata is present.
2. Restore the missing Designer partial from the EF9-generated final snapshot;
   keep discovery attributes in the existing source so they are not duplicated.
3. Build/run the regression, pending-model check and real disposable MariaDB
   migration/acceptance suite. Compare source/snapshot hashes to HEAD and review
   generated SQL. No new schema is authorized by this step.
4. Record actual results in the closeout review, commit as a separate fix and
   include it in the requested PR. CI must pass before merge; production remains
   held behind the false gate.

Acceptance: nonempty historical target model with the expected hash contract;
existing Up/Down and snapshot unchanged; local/provider checks and CI pass.
Remaining production acceptance is listed in the session closeout review.

Executed inline: metadata/test fix complete; schema tests4/4, full local suite
746/746 with actual disposable MariaDB10.5.23 and local Whisper, zero skips;
pending-model check passes. Generated SQL still adds only the nullable hash column.
Original migration source and snapshot have no diff against HEAD. Database and
review-owned ASR resources were cleaned up. Commit/CI evidence is in the PR.
