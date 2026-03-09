# TCT English — Project Overview

**Project**: TCT English — EdTech Vocabulary & Speaking Platform
**Purpose**: Help TCT Company employees learn business English vocabulary and improve speaking skills
**Type**: Internal corporate EdTech web application
**Academic context**: Final project for Web Programming course

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | ASP.NET Core MVC | .NET 10 |
| Language | C# | 11+ |
| ORM | Entity Framework Core | 10.0.2 |
| Database | Microsoft SQL Server | — |
| Frontend CSS | Bootstrap | 5.3 |
| Frontend JS | jQuery + Vanilla JS | — |
| Real-time | SignalR | — |
| Auth | Cookie + Google OAuth + Facebook OAuth | — |
| Password | BCrypt.Net-Next | 4.0.3 |
| Media | YoutubeExplode | 6.5.7 |
| Email | SMTP (Gmail) | — |

---

## Core Entities & Relationships

```
User
├── Role: Admin | Teacher | Student
├── Status: active | blocked
├── Streak (days consecutive study)
├── Goal (daily card target)
│
├── Sets → Cards → LearningProgress
├── Folders (nested, ParentFolderId)
│
├── Classes (as Owner)
│   ├── ClassMembers (Role: Owner | Member)
│   └── ClassMessages (SignalR)
│
└── UserSpeakingProgress

SpeakingPlaylists
└── SpeakingVideos (YoutubeId, Level A1-C2, Topic)
    └── SpeakingSentences (Text, StartTime, EndTime)
        └── UserSpeakingProgress (UserId, IsCompleted, Mode)
```

---

## Feature Domains

### 1. Account Management
- Register / Login (Email + Password)
- Google OAuth + Facebook OAuth
- Password reset via email token
- Profile: avatar upload, full name, bio
- Account settings: change password, email notifications

### 2. Vocabulary Learning
- **Sets**: User-created flashcard collections
- **Cards**: Term, Definition, Phonetic, Example, ImageUrl, Topic
- **Folders**: Hierarchical organization (up to 3 levels)
- **5 Study Modes**: Flashcard, Quiz (4-choice), Write (type answer), Matching, Read
- **Progress Tracking**: per-card status — new | learning | mastered

### 3. Speaking Practice
- **Shadowing**: A-B repeat with HTML5 audio API
- **Dictation**: Type what you hear, normalized grading
- **Playlist browsing**: Filter by Level (A1-C2) and Topic
- **Transcript extraction**: Automatic via YoutubeExplode
- **Progress tracking**: per-sentence, per-mode completion

### 4. Virtual Classroom
- Create/join classes with password
- Share folder libraries with class
- Real-time chat via SignalR (`ClassChatHub`)
- Class member roles: Owner (full control) | Member (view + use)

### 5. Goal & Streak System
- Daily learning goal: number of cards per day
- Streak counter: increments for each day with study activity
- Dashboard: show streak, today's progress, recent activity

### 6. Admin Panel (`/Areas/Admin/`)
- **Dashboard**: Total users, active today, system stats
- **User Management**: Block/unblock, lock duration, role management
- **Speaking Video Management**: Upload, edit, Level/Topic metadata, extract transcripts
- **AutoUnlockWorker**: Background service, checks every 60s, unlocks expired bans

---

## File Structure (Key Paths)

```
TCTEnglish/
├── Program.cs                    ← App entry, DI, middleware, seeding
├── appsettings.json              ← DB conn, OAuth secrets, SMTP config
├── Models/
│   ├── DbflashcardContext.cs     ← EF Core DbContext
│   ├── [Entities].cs             ← User, Set, Card, etc.
│   ├── JsonVocabularySeeder.cs   ← Reads wwwroot/data/system-vocabulary.json
│   └── SystemVocabularySeeder.cs
├── Controllers/
│   ├── BaseController.cs         ← GetCurrentUserId() helper
│   ├── AccountController.cs      ← Auth flows
│   ├── HomeController.cs         ← Dashboard + Study modes (largest controller)
│   ├── VocabularyController.cs   ← Set/Card CRUD
│   ├── SpeakingController.cs     ← Speaking modes
│   ├── GoalsController.cs        ← Goal management
│   └── LearningApiController.cs  ← AJAX progress endpoints
├── Areas/Admin/Controllers/
│   ├── DashboardController.cs
│   ├── SpeakingVideoManagementController.cs
│   └── UserManagementController.cs
├── Services/
│   ├── IAppEmailSender.cs + SmtpAppEmailSender.cs
│   ├── IAvatarUploadService.cs + AvatarUploadService.cs
│   └── IYoutubeTranscriptService.cs + YoutubeTranscriptService.cs
├── Hubs/ClassChatHub.cs          ← SignalR real-time chat
├── Workers/AutoUnlockWorker.cs   ← Background user unlock job
├── ViewModels/                   ← All typed ViewModels
├── Views/                        ← Razor .cshtml views
│   ├── Shared/_Layout.cshtml     ← Master layout
│   └── [Feature]/[Action].cshtml
└── wwwroot/
    ├── css/, js/, images/, uploads/
    └── data/system-vocabulary.json
```

---

## Environment & Configuration

```json
// appsettings.json (structure — not real values)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sql5112.site4now.net;Database=db_ac6471_tctdb;..."
  },
  "Authentication": {
    "Google": { "ClientId": "...", "ClientSecret": "..." },
    "Facebook": { "AppId": "...", "AppSecret": "..." }
  },
  "SmtpSettings": {
    "Host": "smtp.gmail.com", "Port": 587,
    "SenderEmail": "support.tctenglish@gmail.com"
  },
  "OpenAiApiKey": "..."
}
```

---

## Team & Git Workflow

**Team**: 3 developers — Nguyễn Bảo Châu, Huỳnh Phú Trọng, Trần Quốc Tiến

**Branch strategy**:
- `master` — stable, no direct commits
- Feature branches per developer
- Pull Requests for all merges
- Conventional commit messages (see `.cursor/skills/commit-message.md`)
