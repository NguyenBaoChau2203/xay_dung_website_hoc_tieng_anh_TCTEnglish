# UI acceptance matrix (G04) — round 2

Source views: AU01–06, CO01, SP01–03, WR01–03, GO01, AI01–02, AD01–09.

| ID | Role | URL | Desktop 1366x768 | Mobile 390x844 | Keyboard | Status |
| --- | --- | --- | --- | --- | --- | --- |
| AU01–06 | Auth | Account routes | Not run | Not run | Not run | Acceptance host :5110 not started |
| CO01 | Student | Dashboard | Not run | Not run | Not run | Same |
| SP01–03 | Speaking | Practice | Not run | Not run | Not run | ASR server binary also blocked |
| WR01–03 | Writing | Practice | API only | Not run | Not run | CR01 API/persistence covered |
| GO01 | Goals | Goals | Not run | Not run | Not run | |
| AI01–02 | AI | Chat | Not run | Not run | Not run | |
| AD01–09 | Admin | Admin | Not run | Not run | Not run | |

**Blocker detail:** Round 2 prioritized CR01–CR05 + provider concurrency. Spinning
`:5110` with a separate synthetic DB remains an open setup step (allowed; does not
require stopping owner `:5100`). No browser automation run was completed.
