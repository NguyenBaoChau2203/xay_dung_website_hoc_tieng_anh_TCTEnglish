# TCT English - Project Overview

**Project**: TCT English - EdTech vocabulary and speaking platform
**Purpose**: Help TCT Company employees learn business English vocabulary and
improve speaking skills
**Type**: Internal corporate EdTech web application
**Academic context**: Final project for a Web Programming course

## Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Framework | ASP.NET Core MVC | .NET 10 |
| Language | C# | 11+ |
| ORM | Entity Framework Core | 10.0.2 |
| Database | Microsoft SQL Server | current |
| Frontend CSS | Bootstrap | 5.3 |
| Frontend JS | jQuery + Vanilla JS | current |
| Real-time | SignalR | current |
| Auth | Cookie + Google OAuth + Facebook OAuth | current |
| Password | BCrypt.Net-Next | 4.0.3 |
| Media | YoutubeExplode | 6.5.7 |
| Email | SMTP (Gmail) | current |

## Core Entities And Relationships

```text
User
|- Role: Admin | Teacher | Student
|- Status: active | blocked
|- Streak
|- Goal
|- Sets -> Cards -> LearningProgress
|- Folders (nested, ParentFolderId)
|- Classes (as Owner) -> ClassMembers -> ClassMessages
`- UserSpeakingProgress

SpeakingPlaylists
`- SpeakingVideos (YoutubeId, Level A1-C2, Topic)
   `- SpeakingSentences
      `- UserSpeakingProgress
```

## Feature Domains

### 1. Account Management

- Register / login
- Google OAuth + Facebook OAuth
- Password reset via email token
- Profile: avatar upload, full name, bio
- Security settings

### 2. Vocabulary Learning

- Sets: user-created flashcard collections
- Cards: term, definition, phonetic, example, image, topic
- Folders: hierarchical organization up to 3 levels
- Study modes: flashcard, quiz, write, matching, read
- Progress tracking per card: new | learning | mastered

### 3. Speaking Practice

- Playlist browsing by level and topic
- Practice flows such as shadowing and dictation
- Transcript extraction through YoutubeExplode
- Per-sentence progress tracking

### 4. Virtual Classroom

- Create/join classes with password
- Share folder libraries with class
- Real-time chat through `ClassChatHub`
- Member roles: Owner and Member

### 5. Goal And Streak System

- Daily learning goal
- Streak tracking
- Dashboard progress summary

### 6. Admin Panel

- Dashboard
- User management
- Speaking video management
- Auto unlock worker

## File Structure (Key Paths)

For the authoritative structure map, see `docs/project-structure.md`.

```text
TCTEnglish/
|- Program.cs
|- appsettings.json
|- Models/
|  |- DbflashcardContext.cs
|  |- entity classes
|  |- JsonVocabularySeeder.cs
|  `- SystemVocabularySeeder.cs
|- Controllers/
|  |- BaseController.cs
|  |- AccountController.cs
|  |- HomeController.cs          <- dashboard + public pages only
|  |- ClassController.cs
|  |- FolderController.cs
|  |- SetController.cs
|  |- StudyController.cs
|  |- ChatController.cs
|  |- VocabularyController.cs
|  |- SpeakingController.cs
|  |- GoalsController.cs
|  `- LearningApiController.cs
|- Areas/Admin/Controllers/
|- Services/
|  |- IClassService.cs + ClassService.cs
|  |- IStudyService.cs + StudyService.cs
|  |- IStreakService.cs + StreakService.cs
|  |- IFileStorageService.cs + LocalFileStorageService.cs
|  |- IAvatarUploadService.cs + AvatarUploadService.cs
|  |- IAppEmailSender.cs + SmtpAppEmailSender.cs
|  |- IYoutubeTranscriptService.cs + YoutubeTranscriptService.cs
|  |- OperationResult.cs
|  `- ImageUploadPolicies.cs
|- Security/
|  `- CurrentUserIdExtensions.cs
|- Hubs/
|  `- ClassChatHub.cs
|- Realtime/
|- Workers/
|  `- AutoUnlockWorker.cs
|- ViewModels/
|- Areas/Admin/ViewModels/
|- Views/
|  |- Home/
|  |- Class/
|  |- Folder/
|  |- Set/
|  |- Study/
|  |- Vocabulary/
|  |- Speaking/
|  |- Account/
|  `- Shared/
`- wwwroot/
```

Important note:
- The folder/project name is `TCTEnglish`, but the current code namespace root
  still remains `TCTVocabulary.*`.

## Environment And Configuration

Important runtime/config surfaces:
- `ConnectionStrings:DefaultConnection`
- `Authentication:Google:*`
- `Authentication:Facebook:*`
- `SmtpSettings:*`
- background job config such as `BackgroundJobs:AutoUnlockWorkerEnabled`

Do not treat checked-in appsettings secrets as a best practice. See
`docs/architecture-prioritized-backlog.md` and `.ai/context/known-issues.md`
for cleanup priorities.

## Team And Git Workflow

- `master` is the stable branch
- Use feature branches
- Use pull requests for merges
- Use conventional commit messages via `.agent/skills/commit-message/SKILL.md`
