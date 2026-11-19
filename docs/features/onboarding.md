# First-run onboarding: personal starter decks

A new account sees a pop-up right after its first sign-in. Four short steps (goals, topics, level and pace, a preview) end by
creating 2 to 4 **ordinary personal sets** in a folder "Lộ trình của tôi". The cards are complete copies of the reviewed system
vocabulary (image, IPA, meaning, examples, details). The sets have every personal-set feature; the system vocabulary is never
changed. Plan and evidence: [2026-11-19-onboarding-personal-decks](../superpowers/plans/2026-11-19-onboarding-personal-decks.md).

## How it works

| Part | Where |
| --- | --- |
| State of one user (one row, cascade on user delete) | table `UserOnboardings`, `Models/UserOnboarding.cs`, `Models/DbflashcardContext.Onboarding.cs`, migration `AddUserOnboarding` |
| Goals, the 48 topics (system sets A01–C08), which topic fits which goal | `Services/Onboarding/OnboardingCatalog.cs` |
| Ranking and deck building (pure, deterministic, no AI) | `Services/Onboarding/OnboardingDeckBuilder.cs` |
| Who is prompted, preview, create, refill, banner | `Services/Onboarding/OnboardingService.cs` |
| JSON API (`api/onboarding/*`) | `Controllers/OnboardingApiController.cs` |
| Pop-up host and scripts | `Views/Shared/_OnboardingHost.cshtml`, `wwwroot/js/onboarding-core.js` (pure logic), `onboarding.js` (dialog), `wwwroot/css/onboarding.css` |
| "Bộ thẻ của tôi" and deck detail additions | `PersonalSetService` (`ApplyTileExtrasAsync`), `Views/Set/{Index,_SetTile,_FolderTile,Detail,_StarterRefill}.cshtml`, `onboarding-refill.js`, `personal-sets.css` |

The host partial is included once in `Views/Home/Index.cshtml`, `Views/Vocabulary/Index.cshtml` and `Views/Set/Index.cshtml`. It renders
nothing for guests, and nothing if anything fails, so those pages are never affected.

## Rules

- **Who is prompted:** an account created on or after `OnboardingService.DefaultEligibleFromUtc` (2026-11-19) that owns no set and has no
  learning progress. Everyone else gets a `NotEligible` row and is never prompted automatically; they can still open the flow with
  "Tạo thêm từ sở thích" on `/Sets` (link `/Sets#tao-them-tu-so-thich`).
- **Re-prompting:** "Để sau" (or Esc) closes it; it comes back at the next visit at least 20 hours later, at most 2 prompts in all; the
  second skip sets `Skipped`.
- **Ranking:** CEFR window by level (A1 → A1–A2, A2 → A1–B1, B1 → A2–B2, B2 → B1–B2); when a chosen topic has nothing inside the window
  (folder A holds A1–A2 only, folder B B1–B2 only) the nearest levels fill the deck. Topics are mixed round-robin, cards with an image
  first, every term at most once across the decks, the user's own sets and the "Đã biết" words. 25 words a deck (10–30), 2–4 decks, at
  most 100 cards. Decks are listed easy to hard.
- **"Đã biết"** only removes a card from the decks being created and is remembered (lower-case terms) so a refill never brings it back.
  Nothing is marked as mastered.
- **Idempotent:** the request id is claimed inside the transaction; the same id and payload returns the stored result, the same id with
  another payload is a 409; any failure rolls back the folder, sets and cards together.

## How to change goals and topics

1. Goals and their copy: `OnboardingCatalog.Goals`; which topics fit a goal: `GoalTopics`. A test requires every goal to have at least four topics.
2. A new system set as a topic: add its code and exact set name to `OnboardingCatalog.Topics` (a test compares all 48 with the committed manifests
   by code and name) and give it a cover word in `VocabularyCoverCatalog` / `data/content/vocabulary-covers.json`.
3. Deck names and the phrase-deck rule: `OnboardingDeckBuilder` (`GoalDeckName`, `FormGroups`).

## Trying it locally

Create a new account at `/Account/Login` (the register tab) and sign in: the pop-up opens by itself on Trang chủ. There is no admin switch to
"reset" a user and local SQL must not be used for that: make another new account instead. Existing accounts are unaffected; use
"Tạo thêm từ sở thích" on `/Sets` to open the flow by hand. The scripted checks used for the evidence keep their accounts' passwords outside the repository.

## Not covered by automated tests

- Two requests with the same request id at the same instant: relies on the InnoDB row lock taken by the claim `UPDATE` (SQLite has no row locks).
- Real MariaDB execution of the migration beyond applying it to the local Docker database.
