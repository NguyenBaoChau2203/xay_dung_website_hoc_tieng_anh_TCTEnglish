# UI acceptance matrix (G04) — sentinel pass

Host: `http://127.0.0.1:5110` against MariaDB `tct_acceptance_ui5110` (sentinel
`s3_ui_5110`) on labeled acceptance container port 3308. Owner `:5100` left running.

Browser: Cursor IDE browser — Login desktop 1366×768 + mobile 390×844 screenshots
under `.tmp/ui-acceptance-5110/screenshots/`; Tab key exercised on login form.
HTTP matrix JSON: `.tmp/ui-acceptance-5110/http-matrix-final.json`.

| ID | Role | URL | Desktop | Mobile | Keyboard | Status |
| --- | --- | --- | --- | --- | --- | --- |
| LAND | Public | `/` | 200 | — | — | Pass |
| AU01 | Auth | `/Account/Login` | 200 + screenshot | 200 + screenshot | Tab OK | Pass |
| AU02 | Auth | `/Account/Register` | 200 | — | — | Pass |
| AU03 | Auth | `/Account/ForgotPassword` | 200 | — | — | Pass |
| AU04 | Auth | `/Account/ResetPassword` | 200 | — | — | Pass |
| AU05 | Auth | `/Account/Profile` | 302 login | — | — | Expected unauth |
| CO01 | Student | `/Home/Index` | 302 login | — | — | Expected unauth |
| CL01 | Student | `/Home/Class` | 302 | — | — | Expected unauth |
| FO01 | Student | `/Home/Folder` | 302 | — | — | Expected unauth |
| VO02 | Student | `/Vocabulary/Index` | 302 | — | — | Expected unauth |
| SP01–02 | Speaking | `/Speaking*` | 302 | — | — | Expected unauth (ASR loopback ready) |
| WR01–02 | Writing | `/Home/Writing*` | 200 | snapshot | — | Pass (public shell) |
| WR03 | Writing | `/Home/Writing/Practice` | 302 | — | — | Expected unauth |
| GO01 | Goals | `/Goals` | 302 | — | — | Expected unauth |
| LS01 | Listening | `/Home/Listening` | 200 | — | — | Pass |
| RD01 | Reading | `/Home/Reading` | 302 | — | — | Expected unauth |
| GR01 | Grammar | `/Home/Grammar` | 200 | — | — | Pass |
| NT02 | Notifications | `/Notification/Index` | 302 | — | — | Expected unauth |
| AD01 | Admin | `/Admin` | 302 | — | — | Expected unauth |
| PR01/CT01 | Public | Privacy/Contact | 200 | — | — | Pass |
| HZ01 | Ops | `/health/ready` | 200 | — | — | Pass |

**Limits:** Seed users use test hash passwords (not interactive login). Authenticated
desktop/mobile deep flows for Speaking/Goals/Admin remain open without a bcrypt
acceptance account. `/Set` bare route 404 (legacy `/Home` routes used elsewhere).
