# TCT English — Domain Glossary

Key terms and concepts used across the TCT English codebase.
AI assistants should use these exact terms consistently.

---

## Application Domain Terms

| Term | C# Entity/Property | Description |
|------|--------------------|-------------|
| **Set** | `Set` | A collection of flashcards created by a user |
| **Card** | `Card` | A single flashcard with Term, Definition, Phonetic, Example |
| **Folder** | `Folder` | A container for Sets; can be nested (ParentFolderId) |
| **Class** | `Class` | A virtual classroom shared by multiple users |
| **Class Owner** | `ClassMember` (Role="Owner") | User who created the class; full control |
| **Class Member** | `ClassMember` (Role="Member") | User who joined the class; view + use only |
| **Saved Folder** | `SavedFolder` | A folder shared into a class for class members |
| **Streak** | `User.Streak` | Count of consecutive days with study activity |
| **Goal** | `User.Goal` | Daily target number of cards to learn |
| **Speaking Video** | `SpeakingVideo` | A YouTube video used for speaking practice |
| **Playlist** | `SpeakingPlaylist` | Curated collection of Speaking Videos |
| **Sentence** | `SpeakingSentence` | A transcript line from a Speaking Video |
| **Level** | `SpeakingVideo.Level` | CEFR level: A1, A2, B1, B2, C1, C2 |
| **Topic** | `SpeakingVideo/Card.Topic` | Content topic tag (e.g., Business, Career) |

---

## Study Mode Terms

| Mode | View File | Description |
|------|-----------|-------------|
| **Flashcard mode** | `Views/Study/Study.cshtml` | Flip card to reveal definition; mark known/unknown |
| **Quiz mode** | `Views/Study/QuizMode.cshtml` | Multiple choice (4 options); distractors from same set |
| **Write mode** | `Views/Study/WriteMode.cshtml` | Type the term from definition; exact match grading |
| **Matching mode** | `Views/Study/MatchingMode.cshtml` | Drag term to its definition; max 8 pairs per round |
| **Read mode** | `Views/Study/Reading.cshtml` | Display cards as reading text for passive review |
| **Shadowing** | `Views/Study/Speaking.cshtml` | A-B repeat listening; user speaks along with audio |
| **Dictation** | `Views/Study/Speaking.cshtml` | Type what you hear; normalized grading |
| **Listening** | `Views/Study/Speaking.cshtml` | Comprehension questions about audio content |

> **Note**: The study-mode speaking screen (`Views/Study/Speaking.cshtml`) is owned by `StudyController`
> and is distinct from the speaking playlist area (`Views/Speaking/Index.cshtml`,
> `Views/Speaking/Practice.cshtml`) which is owned by `SpeakingController`.

---

## Technical Terms

| Term | Meaning in TCT English |
|------|----------------------|
| **IDOR** | Insecure Direct Object Reference — accessing another user's resource by guessing ID |
| **Anti-IDOR** | Ownership check in every parameterized query: `.Where(x => x.UserId == userId && x.Id == id)` |
| **CSRF** | Cross-Site Request Forgery — mitigated by `[ValidateAntiForgeryToken]` |
| **N+1 query** | EF loading relationships one-by-one in a loop (performance bug) |
| **AsNoTracking** | EF hint to skip change tracking for read-only queries (performance improvement) |
| **BaseController** | Parent controller class providing `GetCurrentUserId()` helper |
| **GetCurrentUserId()** | Helper method on `BaseController` using `CurrentUserIdExtensions` — always use this, never parse `ClaimTypes.NameIdentifier` inline |
| **DbflashcardContext** | The EF Core `DbContext` class for TCT English database |
| **Seeder guard** | `if (await context.X.AnyAsync()) return;` prevents duplicate seeding |
| **Idempotent** | An operation that produces the same result when executed multiple times |
| **AutoUnlockWorker** | Background `IHostedService` that unblocks users when `LockExpiry` passes |
| **OperationResult** | Shared success/failure wrapper returned by services; use instead of raw bool or exceptions |

---

## User Roles

| Role | Access Level | Description |
|------|-------------|-------------|
| `Admin` | Full access | Can manage all users, videos, system settings. Access `/Areas/Admin/` |
| `Teacher` | Extended access | Can create and manage classes; may have elevated permissions |
| `Student` | Standard access | Can create sets, study, join classes, use speaking features |

---

## Learning Progress Status Values

| Status | Meaning |
|--------|---------|
| `new` | Card has never been studied |
| `learning` | Card has been seen but not yet mastered |
| `mastered` | User has demonstrated knowledge of the card |

---

## CEFR Language Levels (Speaking Video Levels)

| Level | Description | TCT English Target |
|-------|-------------|-------------------|
| A1 | Beginner | Very basic vocabulary and phrases |
| A2 | Elementary | Simple sentences, everyday topics |
| B1 | Intermediate | Main points on familiar topics (most TCT employees) |
| B2 | Upper-Intermediate | Technical and professional topics |
| C1 | Advanced | Complex, nuanced communication |
| C2 | Mastery | Near-native professional communication |

---

## Speaking Video Topics (Standardized)

| Topic Tag | Content |
|-----------|---------|
| `Career` | Job interviews, career advancement, performance reviews |
| `Meeting` | Business meetings, presentations, conference calls |
| `Email` | Business email writing and formal communication |
| `Office` | Everyday office vocabulary and small talk |
| `Customer` | Customer service and client communication |
| `Technical` | Industry-specific and technical terminology |
| `Grammar` | Grammar-focused speaking examples |

---

## Configuration Keys (appsettings.json)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Authentication:Google:ClientId/ClientSecret` | Google OAuth credentials |
| `Authentication:Facebook:AppId/AppSecret` | Facebook OAuth credentials |
| `SmtpSettings:Host/Port/SenderEmail/Password` | Email sending configuration |
| `OpenAiApiKey` | OpenAI API key (if AI features used) |
