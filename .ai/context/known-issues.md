# TCT English — Known Issues & Technical Debt

This document tracks known bugs, technical debt, and architectural warnings.
AI assistants should check this before suggesting solutions.

---

## Known Bugs (Unresolved)

### BUG-001: Speaking Page 500 Error
- **Symptom**: `/Speaking/Video/{id}` throws NullReferenceException
- **Root Cause**: `SpeakingSentence` loaded without `.Include(v => v.SpeakingVideo)`
- **When it triggers**: Accessing speaking sentences that reference video properties
- **Fix**: Add `.Include(s => s.SpeakingVideo)` to the query in `SpeakingController`
- **Status**: Known — check if fixed in current codebase

### BUG-002: AutoUnlockWorker Re-unlocking Immediately
- **Symptom**: Admin blocks a user, but user gets automatically unblocked within minutes
- **Root Cause**: `LockExpiry` is set to `null` instead of a future `DateTime`
- **When it triggers**: After admin blocks a user
- **Fix**: Always set `LockExpiry = DateTime.UtcNow.AddHours(duration)` when blocking
- **Status**: Known — verify `UserManagementController.BlockUser` action

### BUG-003: Goal Progress Not Persisting
- **Symptom**: Daily goal counter resets incorrectly
- **Root Cause**: `GoalsController` action not fully async, `UserId` parsed as string
- **When it triggers**: Updating goals for certain users
- **Fix**: Use `int.TryParse()` + fully async action chain
- **Status**: Known — verify `GoalsController`

### BUG-004: Seeder Running on Every Startup
- **Symptom**: Duplicate vocabulary entries appear after each restart
- **Root Cause**: Seeder guard condition missing or referencing wrong `DbSet`
- **When it triggers**: Every application startup
- **Fix**: Add `if (await context.X.AnyAsync()) return;` as first line in seeder
- **Status**: Known — check all seeder files

### BUG-005: SignalR Class Chat Silent Failures
- **Symptom**: Messages sent but not received in real-time by other members
- **Root Cause**: Hub method name case mismatch: C# `SendMessage` vs JS `.on("sendMessage")`
- **When it triggers**: Two users in the same class chat
- **Fix**: Match exact method name including case between server and client
- **Status**: Known — check `ClassChatHub.cs` and the corresponding JS

---

## Technical Debt

### TD-001: HomeController Too Large [HIGH]
- **File**: `Controllers/HomeController.cs` (~38KB)
- **Issue**: Handles Dashboard, Vocabulary, Study modes, Folder management, Classes — too many concerns
- **Impact**: Hard to test, maintain, and extend
- **Recommended fix**: Extract into `VocabularyController`, `StudyController`, `FolderController`
- **Effort**: Large — requires service extraction first

### TD-002: GetCurrentUserId Pattern Duplicated [MEDIUM]
- **Locations**: Every controller (6+ controllers)
- **Issue**: `int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)` repeated everywhere
- **Impact**: Not DRY, inconsistent error handling
- **Recommended fix**: Create `BaseController` with `protected int GetCurrentUserId()` method
- **Effort**: Small — check if `BaseController.cs` already exists

### TD-003: No Service Layer for Vocabulary [MEDIUM]
- **File**: `HomeController.cs`, `VocabularyController.cs`
- **Issue**: EF queries inline in controllers, no service abstraction
- **Impact**: Cannot unit test, controllers too fat
- **Recommended fix**: Create `ISetService`, `ICardService` with implementations
- **Effort**: Medium

### TD-004: ViewBag Usage in Some Views [LOW]
- **Locations**: Some admin views use `ViewBag` instead of typed ViewModels
- **Issue**: No compile-time safety, no IntelliSense
- **Recommended fix**: Create dedicated ViewModels for these views
- **Effort**: Small per view

### TD-005: Secrets in appsettings.json [SECURITY]
- **File**: `appsettings.json`
- **Issue**: OAuth credentials, SMTP password potentially in source control
- **Impact**: If repo goes public, credentials exposed
- **Recommended fix**: Use User Secrets for development, environment variables for production
- **Effort**: Small

### TD-006: No Pagination on Some List Views [MEDIUM]
- **Locations**: Class member list, search results
- **Issue**: Loading all records at once — performance risk at scale
- **Recommended fix**: Add `.Skip().Take()` with page parameter
- **Effort**: Small per view

### TD-007: Missing Database Indexes [MEDIUM]
- **Issue**: High-traffic query columns may lack indexes
- **Columns to check**: `LearningProgress.UserId`, `LearningProgress.CardId`,
                         `SpeakingVideo.Level`, `SpeakingVideo.Topic`
- **Recommended fix**: Add migration with `.CreateIndex()` calls
- **Effort**: Small

---

## Architecture Warnings

### ⚠️ NEVER Do These in This Codebase

1. **Modify `Program.cs` without explicit request** — breaks all service registrations
2. **Generate migrations with `DropTable`/`DropColumn`** without user approval and backup
3. **Add `Console.WriteLine` in production code paths** — use `ILogger<T>` instead
4. **Change EF navigation property names** — breaks migrations
5. **Rename existing database columns** — EF generates DROP + ADD (data loss!)
6. **Use `.Result` or `.Wait()` on async methods** — causes deadlocks in ASP.NET Core
7. **Pass EF entities directly to Views** — use ViewModels always

### ⚠️ Be Careful With

1. **`AutoUnlockWorker`** — background service runs every 60s; ensure `LockExpiry` is always set
2. **Seeder files** — always verify guard conditions before modifying
3. **OAuth callback paths** — misconfiguring these causes redirect loops
4. **SignalR hub method names** — must match exactly between C# and JS (case-sensitive)
5. **`DbflashcardContext.OnModelCreating`** — adding config here can affect migrations

---

## Resolved Issues (History)

| Issue | Resolution | PR/Commit |
|-------|-----------|-----------|
| Speaking video duration not saved | Fixed duration parsing in admin form | commit a59a766 |
| Admin video filters missing | Added Level/Topic dropdowns to management view | commit 2d45edd |
| Vietnamese diacritics in video titles | Fixed encoding in YouTube title extraction | commit 2d45edd |
| Account datetime UTC issue | Changed to UTC timestamps | commit 910e683 |
