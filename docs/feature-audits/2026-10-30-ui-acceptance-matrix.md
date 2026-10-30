# UI acceptance matrix draft (G04)

Source views: AU01–06, CO01, SP01–03, WR01–03, GO01, AI01–02, AD01–09 from
`docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md`.

| ID | Role | URL | Desktop 1366x768 | Mobile 390x844 | Keyboard | Status |
| --- | --- | --- | --- | --- | --- | --- |
| AU01–06 | Auth | Account routes | Not run | Not run | Not run | Needs acceptance app on port 5110 + synthetic user |
| CO01 | Student | Dashboard/home | Not run | Not run | Not run | Same |
| SP01–03 | Student | Speaking practice | Not run | Not run | Not run | Requires mic/ASR or mocked unavailable path |
| WR01–03 | Student | Writing practice | Partial (API) | Not run | Not run | REV01 API/persistence covered; browser matrix pending |
| GO01 | Student | Goals | Not run | Not run | Not run | |
| AI01–02 | Student | AI chat | Not run | Not run | Not run | |
| AD01–09 | Admin | Admin area | Not run | Not run | Not run | |

**This run:** browser matrix **Not run** — no isolated acceptance host on 5110 was
started (owner app on 5100 left untouched). Evidence for Writing meaning-preservation
is from automated API tests, not screenshots.
