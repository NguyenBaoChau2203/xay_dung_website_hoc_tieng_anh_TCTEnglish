# BTH4 business logic and complete schema presentation

## Outcome and authority

Revise only the current BTH4 Word report and its companion academic SQL if a verified schema error needs correction. Preserve the submitted BTH3 unchanged, as explicitly requested. One agent investigates, authors, reviews and verifies. No application, migration, operational database, Git publication or deployment changes.

## Sources and approach

Reuse the BTH4 56-table/516-column design and the verified prior SQL evidence. Check concrete source behavior using CodeGraph before explaining business rules. Preserve the school template, cover, logo, heading/caption periods, automatic contents and figure list. Replace shortened repeated entities in ERDs with complete column lists matching the dictionary and SQL. Divide diagrams into legible complete views when needed; never remove attributes merely to fit a page.

Rewrite explanations in clear Vietnamese suitable for a university student. Explain the business meaning of one record, who creates it, what changes it, its data relationships, and a concrete example for every table. Replace oral-defense coaching and machine-oriented provenance/process passages with actual business analysis. Distinguish database constraints from service/transaction checks without claiming unimplemented rules are already enforced.

## Ordered work and acceptance

- [x] Audit existing prose, attributes, relations, cardinalities, SQL and concrete data flows; record verified issues and corrections.
- [x] Write complete per-table business explanations and readable end-to-end flows covering every table and original use case.
- [x] Generate editable Word ERDs with the same complete attributes for every repeated table and exact agreement with dictionary and SQL.
- [x] Remove the coaching/provenance section, simplify prose and preserve formatting and field navigation.
- [x] Verify the actual Word package, every diagram occurrence, all dictionary fields, SQL compatibility evidence and meaningful integrity cases if schema changes.
- [x] Refresh Word fields in an isolated instance; export, render and inspect every final page; deliver only revised final artifacts and a concise Vietnamese explanation of confirmed fixes.

## Work boundaries

The 56-table academic baseline remains the report scope; new application fields outside BTH3 are not silently included. The current deliverable is a full revision within that baseline. Source evidence, generation helpers and QA images stay in ignored task scratch. Preserve original BTH3 and a backup of the pre-revision BTH4.

## Completion evidence

The revised report is 97 physical pages (cover unnumbered; last printed page 96), 655594 bytes, SHA256 `52f68558ce5281b5beab1275b2f71cf23e36d9a01736bed3303f604c9a0f9412`. All 56 dictionaries/516 columns, 94 full native table occurrences, 82 actual FK registry entries and drawn relations, 123 use-case names/codes, 56 business explanations and 11 data flows passed semantic verification. Twenty-four rules distinguish SQL guarantees from required service/transaction behavior.

All 27 editable Word canvases were write-tested/restored in an isolated Word instance. Final canonical rasterization was reviewed for all 97 pages: 41 exact body-pixel carryovers from a completed 98-page first-render review and direct inspection of all 56 changed pages; all 96 printed footers were independently checked. The sole raster is the retained school logo. Original BTH3, original BTH4 and companion SQL hashes remain unchanged. Prior MariaDB evidence is carried only because the SQL is byte-identical; no fresh database or application test is claimed. Detailed evidence: `.tmp/bth4-revision/verification.json`, `visual-review-final.json` and `review-ledger.json`.

## Follow-up clarification of section 2

Replaced the architecture-classification table and section 2 prose with three student-facing subsections about data to retain, the business reason for separate tables and the meaning of class-to-table mapping. Only section 2 and its refreshed TOC entry changed visually. All other 95 page bodies match the completed prior render exactly; both changed pages were directly inspected and all 96 footers checked. The report remains 97 physical pages. All dictionary/schema/use-case/native-diagram checks pass. Final SHA256 `29015ab639f6002e1fe48f8e112ccf12cb65659c6957eeff946d657114a54eeb`; evidence: `.tmp/bth4-section2/review-ledger.json`. No application/database/SQL/BTH3 changes.
