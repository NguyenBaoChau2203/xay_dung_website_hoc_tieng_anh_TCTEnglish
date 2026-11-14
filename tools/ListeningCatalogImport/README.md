# Explicit local Listening catalog authoring

This tool implements the owner-authorized REACH A1–C2 batch. It never runs at
startup, registers a public API or uses application AI/audio fallback. It does
not change schema. The application project/configuration is unchanged.

The committed source manifest is `data/listening/reach-a1-c2-catalog.json`.
Discovery, staged captions, backups, baseline hashes and local-ID receipts stay
under ignored `.appdata/local/reach-listening-import`. Run from repository root.

```powershell
# Metadata discovery: each completed playlist/search result is cached.
node tools/ListeningCatalogImport/discover.mjs reach
node tools/ListeningCatalogImport/discover.mjs channel REACH-l5p C1
node tools/ListeningCatalogImport/discover.mjs channel REACH-l5p C2
node tools/ListeningCatalogImport/discover.mjs channel VietFCE C1
node tools/ListeningCatalogImport/discover.mjs channel VietFCE C2
# Requires the previously captured baseline-rechecked.json.
# Writes selected-progress.json; preserves an existing curated manifest.
node tools/ListeningCatalogImport/select.mjs

# Captions-only staging, without DB access or audio/AI fallback.
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action Stage
# Snapshot is hashes only. First invocation captures; later ones prove original
# rows still exist unchanged, allowing additive authoring/UAT rows.
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action Snapshot -SkipBuild
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action DryRun -SkipBuild
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action Import -Pilot -SkipBuild
# Verify the six-level pilot in the web before completing the batch.
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action Import -SkipBuild
# Repeat must say create=0, unchanged=15 at every level.
pwsh -NoProfile -File scripts/import-listening-catalog.ps1 -Action Import -SkipBuild
```

The wrapper reads the existing ignored Docker environment, passes its connection
only through child-process environment and restores it afterwards. The writer
requires `127.0.0.1:3307/tctenglish_dev`, user `tct_dev`, and holds an exclusive
file lock plus MariaDB advisory lock. It does not start/reset the database.

Full preflight validates distribution, IDs, bounds, public/embed evidence and all
staged caption times before writing. Network results are staged first. Private
copies remain independent. Public IDs are compared ordinally after materializing
them, including public drafts, to avoid MariaDB collation conflating letter case.
Conflicting public content stops the batch without overwrite. Each complete lesson
and its children commit in one transaction. Receipts are written atomically after
commit; interrupted receipt publication resumes by matching the complete content.

No transcript, translation, quiz, vocabulary, speaker identity or country is
invented. Track metadata alone is not proof of retrieved usable captions. Empty
or timed-out retrieval produces video-only content with honest manifest status.

After authoring, export each receipt's `lessonId` through the canonical command:
`scripts/shared-data.ps1 -Action Export -Kind ListeningLesson`. Stable keys are
`tct.listening.youtube.<sha256>` and packages `listening-youtube-<sha256>`, where
the hash is SHA256 of the exact UTF-8 case-sensitive ID. Read shared-data Status
first; retain existing bindings and immutable versions. Use the application's
Release build and `-SkipBuild -DatabaseReady` in the export loop. Never modify
`data/shared/production.json`. Validate and sync twice, then verify last-sync
catalog hash and preserved baseline rows. Follow the canonical shared-data guide.

Provider regressions use the existing guarded acceptance factory and a separate
disposable MariaDB10.5.23 target (3339 in this execution), never the owner's DB.
Automated tests use synthetic captions and do not contact YouTube/OpenAI.
