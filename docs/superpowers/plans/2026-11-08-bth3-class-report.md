# BTH3 class diagram report

## Authorized outcome
Create one Vietnamese Word report for practical assignment 3, identifying and drawing the TCT English class model. Cover every detailed BTH1 use case, including functions that BTH2 omitted. Preserve the established university cover and formatting. Use editable native Word shapes and connectors for every diagram. Work locally with one agent; do not modify application behavior, database, credentials, or Git history.

## Evidence and design
- Reference: `C:/Users/chau1/Downloads/Nhom03_BTH1_T105.docx`; read-only inspection and rendering.
- BTH2 is supporting context, not the scope boundary.
- Source: current model definitions, DbContext and focused service contracts through CodeGraph. 56 persisted model classes were found. Separate entity classes from analysis control classes and external service contracts.
- UML notation: OMG UML 2.5.1. Use associations and multiplicities, composition only for exclusive lesson content, and dashed dependencies for control/service usage. Class-role enums do not imply User subclasses. Do not use include/extend in class diagrams.
- A class view may serve several use cases. Every original detailed ellipse must have an explicit trace row; split combined create/delete/update actions into separate responsibilities.

## Ordered checklist
- [x] Inspect references and current source; capture immutable source hashes and inventory.
- [x] Distill the reference layout into `.tmp/bth3/artifact.md` and inspect reference pages.
- [x] Define class attributes, responsibilities, typed operation contracts, relations and constraints.
- [x] Establish exhaustive BTH1-to-class/operation coverage, including all seven writing functions.
- [x] Clone the reference cover/styles/page furniture and author the BTH3 body.
- [x] Generate native class diagrams with readable labels, explicit multiplicities and uncluttered routes.
- [x] Update automatic contents, figure list, figure numbers and page numbers with Word.
- [x] Render all pages with the bundled document renderer and inspect every page; repair and repeat as needed.
- [x] Verify reference unchanged, diagrams editable, only the logo is a raster image, all coverage rows/classes present and no application files changed.
- [x] Prepare the final DOCX and concise Vietnamese handoff for delivery.

## Acceptance
The DOCX opens in Microsoft Word, native shape text can be edited, all 56 model classes are documented, every detailed BTH1 ellipse is traced, all required class relations have valid endpoints/multiplicities, and all rendered pages are legible without clipping, overlaps, empty captions or stale field errors. Distinguish the analysis control model from the current implementation. No build or application tests are needed because this task changes no application code.

## Final evidence
- Output: `C:/Users/chau1/Downloads/Nhom03_BTH3_T105_SoDoLop.docx`, 99 pages, 964182 bytes.
- SHA256: `5eaa0e5955ba39f3497248cc6fb433ba0fea7392e283108b7984df5dd9ff839f`.
- `verify.py` and `final_gate.py`: PASS. Validated all 516 typed attributes within their individual class dictionaries, all 166 operation rows and 123 original use-case IDs, all 35 native canvases/captions, 82 specified relationships, unchanged reference/logo and updated automatic fields.
- Word COM verified 35 canvases and one inline image; native text editing was exercised. The final PDF was rasterized with the bundled document renderer. All 99 pages were visually reviewed, with pixel-equivalent carry-forward and direct inspection of all changed pages. Evidence: `.tmp/bth3/review-ledger.json`.
- Connector geometry has no coincident segments between different relationships and no lines crossing class interiors. Cardinality labels remain outside class boxes and inside the canvas.
- No application, protected configuration, database, Git history, CI or production changes were made. Existing owner files were preserved.
