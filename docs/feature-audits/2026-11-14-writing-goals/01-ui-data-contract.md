# Stage 01 UI Data Contract & Inventory Handoff

Prepared on: 2026-11-14\
Branch: `fix/writing-goals-profile-polish`\
Client/Model: Gemini 3.8 Flash High\
Stage: 01 — UI first\
Consuming stages: Stage 02 (Writing backend), Stage 03 (Goals & Streak consistency), Stage 04 (Badges & Tiers), Stage 06 (UI Integration)

---

## 1. Inventory of Metrics, Awards & CTAs

### 1.1. Writing Module (`/Study/Writing`, `/Study/WritingExercises`, `/Study/WritingPractice`)

| Surface / Element | Current UI Representation | Classification | Source / Real Property | Missing Contract / Direction for Stages 02/05/06 |
| --- | --- | --- | --- | --- |
| `/Study/Writing` Hero Title & Description | Luyện Viết Song Ngữ Theo Ngữ Cảnh | Real / Functional | Static verified copy | Replaces hardcoded fake "Viết Email Chuyên Nghiệp (65%)". When Stage 02/06 backend provides `ContinueExercise`, bind actual last practiced lesson. |
| `/Study/Writing` Continue CTA | Bắt đầu ngay (`asp-action="WritingExercises"`) | Real CTA | `Model.SelectedLevelKey` | Stage 06: Deep-link to specific resume exercise ID if available. |
| `/Study/Writing` Level Selection Cards | 3 level cards (Cơ bản, Trung cấp, Nâng cao) | Real Data | `Model.Levels` (`Key`, `Title`, `DurationText`, `IconClass`, `AccentColor`, `IsSelected`) | Fully dynamic, client-side switch updates URL and content type hrefs without page reload. |
| `/Study/Writing` Content Type Cards | 6 topic cards (Emails, Diaries, Essays, Articles, Stories, Reports) | Real Data | `Model.ContentTypes` (`Key`, `Title`, `Description`, `IconClass`, `AccentColor`) | Real navigation to `/Study/WritingExercises?level={level}&contentType={type}`. |
| `/Study/Writing` Stat Card 1 | Dạng bài đa dạng (`@Model.ContentTypes.Count Dạng bài`) | Real Platform Data | `Model.ContentTypes.Count` | Replaced fake "12 Ngày" streak placeholder. Future stage 03/06 may expose confirmed writing practice streak. |
| `/Study/Writing` Stat Card 2 | Đánh giá câu viết: Thời gian thực | Real Capability | Platform capability | Replaced fake "4,250 Từ" counter placeholder. Future stage 02/06 can expose real cumulative word count if persisted. |
| `/Study/WritingExercises` Level Badge & Count | Cấp độ + `@Model.TotalCount` bài khả dụng | Real Data | `Model.SelectedLevelTitle`, `Model.TotalCount` | Fully bound to database count. |
| `/Study/WritingExercises` Filter Form | Topic dropdown, Status dropdown (if auth) | Real Interactive Form | `Model.TopicOptions`, `Model.StatusOptions`, `Model.SelectedTopic`, `Model.SelectedStatus` | Triggers submit on change; preserves all routing query parameters. |
| `/Study/WritingExercises` AI Creator | Textarea (max 4000 chars), Quota badge ("Tối đa 5 bài/ngày") | Real Contract | `data-writing-create-form`, CSRF, `Model.CanCreateFromAi` | Rate limit & circuit open handled with `TctAiAvailability`. |
| `/Study/WritingExercises` Exercise Cards | Catalog & Private cards with sentence counts, attempt counts, status badge | Real Data | `Model.Exercises`, `Model.MyExercises` (`Id`, `Title`, `PreviewText`, `Topic`, `SentenceCount`, `AttemptCount`, `StatusKey`, `StatusLabel`) | Shows honest status: "Chưa học", "Đang học", "Hoàn thành", "Đã khóa" (gói Standard). |
| `/Study/WritingExercises` Pagination | Page numbers & Prev/Next buttons | Real Navigation | `Model.CurrentPage`, `Model.TotalPages`, `Model.PageNumbers` | Retains all filter query params. |
| `/Study/WritingPractice` Top Bar | Breadcrumb link, Level pill, Status pill, Attempt pill, Progress pill & bar | Real Data | `Model.SelectedLevelTitle`, `Model.StatusLabel`, `Model.AttemptCount`, `Model.CompletedSentenceCount`, `Model.TotalSentenceCount` | All stats derived from real attempt & sentence progress. |
| `/Study/WritingPractice` Inline Sentences | Paragraphs with interactive sentence buttons | Real Data | `Model.Sentences` (`id`, `number`, `vietnameseText`, `placeholder`, `breakAfter`, `hasAccepted`, `acceptedText`) | English reference answer is kept secret (server evaluation only). |
| `/Study/WritingPractice` Feedback Panel | Banner (`#feedbackBanner`), Source note, Sentences breakdown, AI suggestion | Real Dynamic DOM | `lastEvaluation`: `passed`, `assessmentStatus`, `summaryTitle`, `summaryText`, `suggestedRewrite`, etc. | Three assessment statuses handled: `Accepted`, `NeedsRevision`, `NeedsReview`. |
| `/Study/WritingPractice` Local Drafts | Auto-save and indicator (`#draftIndicator`) | Real Client Contract | `localStorage` keyed with `wp_draft_u{ownerUserId}_ex{exerciseId}_{sentenceId}` | Isolated per user; removed on sentence acceptance; restored on reload. |

---

### 1.2. Goals Module (`/Goals`)

| Surface / Element | Current UI Representation | Classification | Source / Real Property | Missing Contract / Direction for Stage 03 |
| --- | --- | --- | --- | --- |
| Streak Card | `@Model.CurrentStreak ngày`, Longest: `@Model.LongestStreak ngày` | Real Server Metric | `Model.CurrentStreak`, `Model.LongestStreak`, `Model.StreakMessage` | Currently calculated from daily review presence; Stage 03 will unify streak definition with UTC+7 business clock. |
| Mini Streak & Day Dots | 7-day columns/dots | Real Vocabulary Data (Re-labeled) | `Model.WeeklyActivity` (`DayLabel`, `FullLabel`, `ActivityCount`, `IsToday`) | Clarified to "Hoạt động ôn từ vựng 7 ngày qua" because `ActivityCount` is `CardsReviewed`. Stage 03 will supply unified all-skill study-day activity states. |
| Daily Goal Card | Progress % (`@Model.ProgressPercent%`), skill list, radial progress | Real Data | `Model.GoalCards` (`AreaLabel`, `TodayProgressValue`, `TargetValue`, `UnitLabel`, `ProgressPercent`) | Real progress across active skills. Deferred skills alerted honestly. |
| Skills Grid | 5 skill cards (Từ vựng, Luyện nói, Kỹ năng viết, Kỹ năng đọc, Kỹ năng nghe) | Real Data & Links | `vocabCard`, `speakingCard`, `writingCard`, `readingCard`, `listeningCard` | Deep-links to `/Vocabulary`, `/Speaking`, `/Study/Writing`, `/Study/Reading`, `/Study/Listening`. |
| Weekly Activity Chart | Bar chart with 7 days of vocabulary card reviews | Real Data (Re-labeled) | `Model.WeeklyActivity` (`HeightPercent`, `ActivityCount`, `IsToday`) | Removed misleading "Cập nhật theo thời gian thực" claim; updated to "Dữ liệu làm mới khi tải trang". |
| Badges Showcase | Badges grid with unlocked / locked tiles | Real Server Data | `Model.Badges` (12 existing seed badges, `Code`, `Name`, `Description`, `IconClass`, `IsUnlocked`, `ProgressPercent`, `ProgressLabel`) | 12 existing badges render honestly. Unsupported new badges kept out of live UI until Stage 04. |
| Goal Editor Modal | Skill select, stepper buttons, quick presets (5, 10, 20, 30, 50), submit | Real Form | `Model.GoalEditor`, `UpdateGoalInputViewModel`, CSRF | Unit badge dynamically updates between "thẻ / ngày", "video / ngày", and "bài / ngày". |

---

### 1.3. Profile Module (`/Account/Profile`)

| Surface / Element | Current UI Representation | Classification | Source / Real Property | Missing Contract / Direction for Stages 03/04 |
| --- | --- | --- | --- | --- |
| Header Stat Chips | `@Model.StreakDays ngày` | Real Data | `Model.StreakDays` | Currently reads `user.Streak`. Stage 03 reconciles with Goals current streak. |
| Stat Cards | Streak days, Danh hiệu đạt được (`@Model.EarnedBadges?.Count ?? 0`) | Real Data | `Model.StreakDays`, `Model.EarnedBadges.Count` | Retains real zeros; no fake placeholders. |
| Quick Action Link | "Mục Tiêu & Lộ Trình Hàng Ngày" | Real Navigation | Link to `/Goals` | Removed fake "bảng xếp hạng" claim; truthful description. |
| Thói quen rèn luyện | Khung giờ, Tỷ lệ mục tiêu, Kỹ năng luyện nhiều nhất, Thời gian ước tính, Ngày luyện | Real Data | `Model.Habits` (`UsualHourText`, `GoalRateText`, `TopSkillText`, `EstimatedTimeText`, `PracticeDaysText`) | Powered by real `LearningHabitService` segments and activities. |
| Danh hiệu đã đạt (Empty State) | `_StateMessage` component linking to `/Goals` | Honest Empty State | `Model.EarnedBadges == null \|\| !Model.EarnedBadges.Any(b => b.IsUnlocked)` | Removed 3 invented locked badge cards (`Khởi đầu chủ động`, `Ngọn lửa kiên trì`, `Trí nhớ siêu phàm`). |
| Danh hiệu đã đạt (Earned State) | Unlocked badges list | Real Data | `Model.EarnedBadges.Where(b => b.IsUnlocked)` | Renders real earned badges from database. |

---

## 2. Missing Backend Fields & Contracts (Handoff to Stages 02 & 03)

### 2.1. Writing Index Resume Contract (Stage 02 & 06)
`WritingIndexViewModel` currently contains only navigation taxonomy:
```csharp
// Current:
public class WritingIndexViewModel
{
    public string SelectedLevelKey { get; set; } = string.Empty;
    public string SelectedLevelTitle { get; set; } = string.Empty;
    public List<WritingLevelCardViewModel> Levels { get; set; } = new();
    public List<WritingContentTypeCardViewModel> ContentTypes { get; set; } = new();
}
```
**Requested expansion for Stage 02/06**:
```csharp
// Proposed optional fields for authenticated learner resume:
public WritingContinueExerciseViewModel? ContinueExercise { get; set; }
public int? UserConfirmedWritingStreakDays { get; set; }
public int? UserTotalWordsWritten { get; set; }

public class WritingContinueExerciseViewModel
{
    public int ExerciseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string LevelKey { get; set; } = string.Empty;
    public string ContentTypeKey { get; set; } = string.Empty;
    public int CompletedSentenceCount { get; set; }
    public int TotalSentenceCount { get; set; }
    public int ProgressPercent { get; set; }
}
```

### 2.2. Event Boundaries for Practice Day vs. Full Exercise Completion (Stage 02 & 03)
- **Practice-Day Activity Event**: Occurs when a learner gets their **first accepted sentence** on a business date (UTC+7). This event:
  - Confirms study activity for that day to maintain or increment streak.
  - Does NOT award completion XP or increment the Writing Daily Goal counter.
  - Can occur on repeat exercises.
- **Exercise Completion Event**: Occurs when the **entire exercise** is completed for the first time. This event:
  - Awards 20 XP completion credit.
  - Increments the Writing Daily Goal counter (first completion only).
  - Subsequent repeats of the same exercise do not mint duplicate lifetime exercise completion awards.

### 2.3. Unified All-Skill Daily Activity Contract (Stage 03)
`GoalsViewModel.WeeklyActivity` currently only projects `CardsReviewed` from `UserDailyActivities`.\
Stage 03 is requested to supply verified all-skill study-day activity states:
```csharp
public class DailyActivityDayViewModel
{
    public DateTime Date { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public string FullLabel { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool HasConfirmedActivity { get; set; } // Any skill confirmed
    public int VocabularyCardsReviewed { get; set; }
    public int SpeakingPracticedCount { get; set; }
    public int WritingSentencesAccepted { get; set; }
    public bool IsGoalAchieved { get; set; } // All active daily targets met
}
```

---

## 3. Badge Tier & Mystery Specification (Handoff to Stage 04)

### 3.1. Five Defined Tiers & Vietnamese Labels
1. `chill`: **Nhập môn chill** (First small milestones)
2. `cuon`: **Bắt đầu cuốn** (Repeated habit & momentum)
3. `chien-than`: **Hệ chiến thần** (Sustained challenge & mastery)
4. `trum-cuoi`: **Trùm cuối server** (Elite long-term achievements)
5. `bi-an`: **Ủa, bí mật nè!** (Hidden mystery group)

### 3.2. Ten Proposed New Awards
| Code | Name | Group | Proposed Server Rule |
| --- | --- | --- | --- |
| `writing-mood` | Bút mới lên mood | chill | 3 distinct first-completed Writing exercises |
| `study-checkin` | Check-in bằng bài học | chill | 3 confirmed active business dates |
| `writing-deadline` | Deadline cũng phải nể | cuon | 10 distinct first-completed Writing exercises |
| `xp-mood` | XP lên mood | cuon | 500 legitimately accumulated XP |
| `keyboard-form` | Bàn phím vào form | chien-than | 25 distinct first-completed Writing exercises |
| `steady-flame` | Giữ lửa không seen | chien-than | Longest confirmed streak 14 days |
| `keyboard-boss` | Trùm cuối bàn phím | trum-cuoi | 50 distinct first-completed Writing exercises |
| `monthly-flame` | Giữ lửa xuyên tháng | trum-cuoi | Longest confirmed streak 30 days |
| `secret-plot-twist` | Plot twist thứ 13 | bi-an | 13 distinct first-completed Writing exercises |
| `secret-quiet-effort` | Hệ chăm ngầm | bi-an | 21 confirmed active business dates |

### 3.3. Mystery Badge Security & Privacy Contract
- **Locked State**: Locked mystery badges MUST NOT leak their title, description, metric rule, target threshold, or progress percentage through HTML text, JSON payloads, or DOM data attributes.
  - Locked display title: `"Huy hiệu bí ẩn"`
  - Locked display description: `"Ủa, bí mật nè! Hãy tiếp tục chăm chỉ học tập để khám phá điều bất ngờ."`
  - Locked display icon: `fa-solid fa-question` / `fa-solid fa-user-secret`
  - Progress: hidden (not exposed as `12/13`).
- **Unlocked State**: Once verified and awarded by the server, the full details (`Plot twist thứ 13`, `Hệ chăm ngầm`) become visible to the owner.
- **Client Award Restriction**: Awards are strictly minted on the server in authorized transactions. No client-side unlock logic or click-to-unlock.

---

## 4. Visual Prototype Fixture (Documentation / Preview Only)

> [!NOTE]
> The following component snippet is a design system reference for Stage 04 & 06. It is intentionally NOT included in live Razor views during Stage 01, keeping live UI 100% faithful to currently deployed server capabilities.

```html
<!-- DEMO / PREVIEW ONLY: GenZ Badge Tier & Mystery Card Design -->
<div class="demo-badge-tier-filter d-flex gap-2 mb-3">
    <button type="button" class="btn btn-sm btn-primary rounded-pill">Tất cả</button>
    <button type="button" class="btn btn-sm btn-outline-secondary rounded-pill">Nhập môn chill</button>
    <button type="button" class="btn btn-sm btn-outline-secondary rounded-pill">Bắt đầu cuốn</button>
    <button type="button" class="btn btn-sm btn-outline-secondary rounded-pill">Hệ chiến thần</button>
    <button type="button" class="btn btn-sm btn-outline-secondary rounded-pill">Trùm cuối server</button>
    <button type="button" class="btn btn-sm btn-outline-secondary rounded-pill">Ủa, bí mật nè!</button>
</div>

<!-- Locked Mystery Badge Card Example -->
<div class="goals-badge-tile goals-badge-tile--mystery-locked" title="Huy hiệu bí ẩn (Chưa mở khóa)">
    <div class="goals-badge-icon-wrap goals-badge-icon-wrap--mystery" aria-hidden="true">
        <i class="fa-solid fa-question" style="font-size: 16px;"></i>
    </div>
    <span class="goals-badge-name">Huy hiệu bí ẩn</span>
</div>
```
