# PR 145 Class integration review

## Scope and execution

- [x] Check the exact PR head and merge-tree output against origin/master.
- [x] Reproduce the five failing CI assertions with the original PR contents.
- [x] Reconnect the existing Class and Browse views, shared navigation and Class JavaScript to the new endpoints. Align legacy assertions to the current Vietnamese copy and verify the Teacher-only create route.
- [x] Require a current code for private enrollment and return 404 on direct outsider access. Rotate the code when a public class becomes private.
- [x] Preserve omitted visibility for existing edit callers and generate a code when shared data constructs a class. Keep codes outside the shared-data format.
- [x] Fix the unapplied migration backfill to keep all original ClassID digits. The migration adds Class columns and a unique index. Legacy classes stay discoverable. Review the Down path without applying its column drops.
- [ ] Verify Release build, relevant tests, and fresh/repeated migration on disposable MariaDB 10.5.23 with legacy and large class IDs.
- [ ] Verify the learner and Teacher/Admin flows at desktop and mobile widths. Record the actual browser and CI results before merging the reviewed head.

## Findings

- P1. Browse controller and Class UI contracts were split across PRs. The current head lacked the Browse view and the JavaScript for the new endpoints.
- P1. Visibility previously only constrained discovery. An outsider who reached a private class directly could still view its metadata and join by numeric ID.
- P1. MariaDB LPAD truncates values wider than the requested length. The original six-digit backfill could collide. Legacy data import also needs valid unique join codes without exporting them.
- P2. Four Class tests checked obsolete literal text and one AI route test assumed all learners could still create classes. Keep the protected-content assertions and cover both the allowed and denied roles.
