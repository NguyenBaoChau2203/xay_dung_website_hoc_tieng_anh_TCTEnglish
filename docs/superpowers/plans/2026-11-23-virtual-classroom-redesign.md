# Virtual Classroom redesign — implementation plan

> **For agentic workers:** Execute task-by-task in this checkout. Do not spawn subagents. Local only until owner authorizes Git/PR/deploy.

**Spec:** [2026-11-23-virtual-classroom-redesign-design.md](../specs/2026-11-23-virtual-classroom-redesign-design.md)

## Files

| Responsibility | Path |
| --- | --- |
| Service contract | `TCTEnglish/Services/IClassService.cs`, `ClassService.cs` |
| Hub | `TCTEnglish/Hubs/ClassChatHub.cs` |
| Controller | `TCTEnglish/Controllers/ClassController.cs` |
| ViewModels | `ClassPageViewModel.cs`, `ClassDetailViewModel.cs`, `CreateClassViewModel.cs`, `SearchViewModel.cs` |
| Views/JS/CSS | `Views/Class/*`, `wwwroot/js/Class-Detail.js`, `wwwroot/css/class.css`, `Class-Detail.css` |
| Global search | `HomeController.Search`, `Views/Home/Search.cshtml` |
| Tests | `TCTEnglish.Tests/ClassServiceTests.cs` (+ hub helpers if needed) |

## Tasks

### P0 — Server truth

- [ ] Hub mute + chat-lock checks
- [ ] `AllowMemberToPost` on add folder
- [ ] Join pending → Success + status payload
- [ ] Detail VM `IsMutedForCurrentUser`, `CanSendChat`
- [ ] Tests

### P1 — Cover grid + find → Detail

- [ ] Managed/Joined lists + card fields
- [ ] `Class.cshtml` + `class.css` Clay grid
- [ ] Create cover UX; edit cover when practical
- [ ] Pre-join hero; search badges; no “mã”

### P2 — Teacher Detail

- [ ] Default tab Requests/Folders for managers
- [ ] Request actions stay on manage surface

### P3 — Chat + load-more

- [ ] Composer + escape
- [ ] `ClassMessages` page API + Tải thêm

### Verify

- [ ] `dotnet test` Class* (+ build)
- [ ] Browser 1440/768/375 notes in handoff
