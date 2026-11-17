# BTH4 data classes and relational database report

## Authorized outcome and scope

Create a Vietnamese BTH4 Word report for TCT English using the existing school cover and formatting, with native editable Word ER diagrams, detailed data dictionaries, relationships, keys, constraints, normalization and a runnable companion schema SQL file. Follow the BTH4 reference PDF's academic structure while preserving BTH3 use-case and entity coverage.

One agent performs authoring and review. Application code, persisted models, migrations, existing databases, shared-data packages, credentials, Git history and deployment remain unchanged. A disposable local MariaDB instance may be used to test the schema and synthetic integrity cases; never import or modify the application's database. SQL is a clean-schema academic artifact, not a production upgrade.

## Sources and deliverables

- Retained formatting reference: `C:/Users/chau1/Downloads/Nhom03_BTH1_T105.docx`.
- BTH3 source and review evidence: `.tmp/bth3-fix` and the retained completed BTH3 report.
- Academic section reference: `C:/Users/chau1/Downloads/BTH4_NguyenBaoChau_2424802010046.pdf`.
- Current data model: `TCTEnglish/Models`, context mappings and provider-generated relational metadata, inspected without starting the web host.
- Deliverables: `C:/Users/chau1/Downloads/Nhom03_BTH4_T105_CoSoDuLieu.docx` and `C:/Users/chau1/Downloads/Nhom03_BTH4_T105_TaoCSDL.sql`.
- Scratch and evidence: `.tmp/bth4`.

## Ordered work and acceptance

- [x] Extract current schema metadata without database access, reconcile all 56 BTH3 entities and 516 prior attributes, and explicitly record current additions rather than silently changing counts.
- [x] Distill the retained template and inspect the BTH4 reference's distinct page patterns; record the artifact contract and immutable hashes.
- [x] Specify all selected tables, columns, SQL types, NULL/defaults, PK/FK/unique/index rules, update/delete behavior and application-only constraints. Preserve roles, content/progress distinctions and all original functional coverage.
- [x] Produce complete schema SQL without secrets, seed data, DROP statements or production targets; test on a disposable MariaDB when available, including meaningful unique/FK/delete cases and representative queries.
- [x] Draw editable overall/domain ER views with legible PK/FK labels and cardinalities; verify every declared FK occurs in a diagram.
- [x] Build Word with automatic contents/figure list, numbered captions/headings with trailing periods, detailed dictionaries, normalization caveats, SQL examples and a short oral-defense guide.
- [x] Verify report/schema inventories, actual Word tables and graphical coverage, refresh fields using an isolated Word instance, render with the bundled renderer and visually review every final page.
- [x] Deliver the verified Word and SQL files in Vietnamese with exact checks and any material limitations.

## Completion evidence

The final DOCX has 89 physical pages (cover unnumbered, last printed page 88), 25 native editable Word canvases and one raster school logo. The isolated Word COM export refreshed the contents/figure list and write-tested/restored a text shape in every canvas. Final SHA256: `7dee6cbdd218b5618e787d2ad0768e2b7f353b24e98858c5bec3eb1ca91c12da` (642,364 bytes).

The companion SQL SHA256 is `9df9b5a3d8e95fb2818c8d807cd8af8f14c909d7a6a9cb0539a8708d96182337` (57,297 bytes). It creates 56 tables, 516 columns, 56 primary keys, 82 FKs, 99 secondary indexes including 33 UNIQUE, and 58 CHECK constraints. The ResultVideoId FK and CHECKs are explicitly proposed academic additions. The schema remains within BTH3 scope; the current source's 14 additional entities and 147 additional properties are explicitly outside that baseline.

`test_database.py` passed 22 meaningful synthetic integrity/delete/transaction/query checks on MariaDB 10.5.23. `compare_live_schema.py` separately matched all 516 live columns and nullable flags, 82 FK update/delete rules, 56 PK definitions and 99 secondary indexes to the documented design. SQL identifier names are within MariaDB's limit; one overlong source index name is shortened deterministically. The disposable no-network container was stopped and removed after evidence collection; existing application databases and other workloads were not queried or modified.

`verify_report.py` checks the actual Word dictionaries, all 123 original use-case IDs, all eight internal responsibility references, all graphical FK IDs, native-only diagrams, quoted string defaults, heading punctuation, template media/custom XML preservation and source hash. The bundled canonical renderer produced all 89 final page PNGs. Final v5 visual QA includes 86 exact body-pixel carryovers from completed v4 review plus direct inspection of all three changed pages; all 88 printed page numbers were independently checked. Evidence is in `.tmp/bth4/verification.json`, `database-verification.json`, `live-schema-verification.json`, `visual-review-v5.json` and `review-ledger.json`.

This is local artifact/database verification. It does not claim application runtime, CI or production deployment acceptance. No application/model/configuration/migration changes, data synchronization, commit, push or deployment were performed by this task.
