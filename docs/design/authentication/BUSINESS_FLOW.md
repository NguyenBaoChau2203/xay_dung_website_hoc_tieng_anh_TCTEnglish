# BUSINESS_FLOW — Authentication contracts

Date: 2026-11-20  
Source of truth: [`AccountController.cs`](../../../TCTEnglish/Controllers/AccountController.cs)  
Related: [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) · [USER_FLOW.md](USER_FLOW.md)

## Rules

- Document only verified controller behavior.
- UI redesign must not change these contracts without explicit owner authority.
- Antiforgery required on all mutating POSTs listed below.

## 1. Session and entry

| Condition | Behavior |
| --- | --- |
| Guest opens `/Account/Login` | `View("Auth")`, `ActiveTab = login` |
| Guest opens `/Account/Register` | `View("Auth")`, `ActiveTab = register` |
| Authenticated opens Login or Register | Redirect `Home/Index` |
| Cookie sign-in | `AuthenticationProperties.IsPersistent = true` (no Remember Me field) |
| Logout | Sign out cookie + external cleanup → redirect Login |

## 2. Register

**POST** `/Account/Register` — antiforgery, classic form post (not AJAX).

| Input | Required | Rules |
| --- | --- | --- |
| `email` | Yes | Non-empty; `IsValidEmailAddress` |
| `fullName` | Yes | Non-empty (display name) |
| `password` | Yes | Length ≥ 6 |
| `readingLevel` | Optional | Normalized CEFR via `CefrLevels.Normalize(..., "A1")` |
| Terms checkbox | UI-only | Client `required`; not a server parameter |

**Outcomes**

| Result | Response |
| --- | --- |
| Validation fail / duplicate email | Re-render `Auth` with `ActiveTab=register`, `ViewBag.ErrorMessage` |
| Success | Create user (`Role=Standard`), TempData success → redirect Login |

**Not in contract:** confirm password, phone, marketing opt-in.

## 3. Login

**POST** `/Account/Login` — antiforgery, **JSON** response (AJAX from `account-auth-ui.js`).

| Input | Required |
| --- | --- |
| `email` | Yes |
| `password` | Yes |

**Outcomes**

| Result | JSON / side effect |
| --- | --- |
| Unknown / anonymized / bad password | `{ success: false, message: "Email hoặc mật khẩu không đúng." }` |
| Blocked + lock expired | Auto-unlock path may run; then sign-in if unlocked |
| Still blocked | `{ success: false, blocked: true }` (+ TempData lock reason/expiry for modal) |
| Success | Sign-in cookie → `{ success: true }` → client navigates Home + loader |

## 4. Social login

| Step | Behavior |
| --- | --- |
| `GET ExternalLogin?provider=` | Challenge Google or Facebook; `prompt=select_account` |
| Callback fail | Redirect Login |
| Callback success | Link/create user from claims → sign-in → Home (existing callback logic) |

UI must keep provider URLs intact; presentation-only changes allowed.

## 5. Forgot password

| Step | Behavior |
| --- | --- |
| GET | `ForgotPassword` view |
| POST invalid model | Re-render with validation |
| POST valid | Always same confirmation message (anti-enumeration) |
| If user exists | Token = GUID; expiry = UTC now + **15 minutes**; email reset link via `IAppEmailSender` |
| Confirmation view | Generic copy only |

## 6. Reset password

| Step | Behavior |
| --- | --- |
| GET | Bind `token` + `email` into `ResetPasswordViewModel` |
| POST invalid model | Re-render with ModelState |
| Token missing / mismatch | Error on Token |
| Token expired | Error: link expired/invalid |
| Success | BCrypt hash; clear token/expiry; bump `SecurityVersion`; TempData → redirect Login |

**Fields:** `NewPassword`, `ConfirmPassword` (server ModelState / VM validation), plus hidden token/email.

## 7. Errors the UI must surface (no new rules)

| Surface | Mechanism |
| --- | --- |
| Register server errors | `#server-error-alert` (`role="alert"`) |
| Login client errors | `#login-error-msg` |
| Login blocked | `#blocked-alert` and/or Bootstrap modal from TempData |
| Register success | SweetAlert2 from TempData on Login |
| Reset / Forgot validation | Field + summary messages on recovery views |

## 8. Explicit non-features (do not invent)

- Remember Me checkbox
- Confirm password on Register
- Email verification gate before first login (not in these actions)
- Changing token TTL, password min length, or OAuth providers in this redesign

## 9. Next

Journey diagrams and step narratives: [USER_FLOW.md](USER_FLOW.md).
