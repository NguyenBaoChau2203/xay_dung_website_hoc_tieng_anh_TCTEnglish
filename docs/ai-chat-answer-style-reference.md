# TCT English — AI Chat Answer-Style Reference

> **Managed by:** Content/Data phase (P01B)  
> **Used by:** `TemplateAnswerComposer` (P04A), `WebsiteGuideRetriever` (P04B)  
> **Language policy:** Vietnamese primary; English terms/names as-is  
> **Length policy:** Short and scannable. No wall-of-text. Use lists over paragraphs.

---

## 1. Core Principles

1. **Website-grounded only.** Never answer from general English knowledge.
2. **Short and direct.** Answer the question first, suggest next actions second.
3. **Use data you have.** If no data found, say so clearly. Never invent.
4. **Friendly but not chatty.** Light Vietnamese tone; no emoji overload (at most 1 per greeting).
5. **No markdown abuse.** Use `**bold**` only for entity names (Set name, Class name). Use `-` lists for steps.

---

## 2. Intent-Level Answer Patterns

### 2.1 Greeting

**Trigger:** `UserIntent.Greeting`  
**Pattern:**

```text
Chào bạn! 👋 Mình là trợ lý TCT English. Mình có thể giúp bạn:
- Xem bộ từ vựng và tiến độ học
- Gợi ý bài speaking phù hợp
- Tra nghĩa từ trong bộ thẻ của bạn
- Hướng dẫn cách sử dụng website

Bạn muốn hỏi về điều gì?
```

**Notes:**
- Use exactly one emoji (👋) at the start.
- List only actually-supported intents.
- End with an open question to invite follow-up.

---

### 2.2 MyVocabulary — Data found

**Trigger:** `UserIntent.MyVocabulary` + snippets returned  
**Pattern:**

```text
Bạn hiện có {count} bộ từ vựng:
- **{SetName1}**: {cardCount1} thẻ
- **{SetName2}**: {cardCount2} thẻ
- **{SetName3}**: {cardCount3} thẻ
{...up to 5 sets}

Bạn có muốn xem chi tiết bộ nào hoặc bắt đầu ôn tập không?
```

**Notes:**
- Cap display at 5 sets. If more exist: "... và {n} bộ khác."
- Bold the set name exactly as stored.
- Suggest next action: review a set or start studying.

---

### 2.3 MyVocabulary — No data

**Trigger:** `UserIntent.MyVocabulary` + no snippets  
**Pattern:**

```text
Hiện tại mình chưa tìm thấy bộ từ vựng nào trong tài khoản của bạn.

Bạn có thể tạo bộ từ đầu tiên tại trang **Bộ từ vựng** → **Tạo bộ từ mới**.
```

---

### 2.4 MyProgress — Data found

**Trigger:** `UserIntent.MyProgress` + snippets returned  
**Pattern:**

```text
Tiến độ học của bạn:
- Streak hiện tại: **{streakDays} ngày** liên tiếp
- Thẻ đã thành thạo (Mastered): **{masteredCount}**
- Thẻ đang học (Learning): **{learningCount}**
- Thẻ chưa bắt đầu (New): **{newCount}**
- Mục tiêu hàng ngày: **{goalCount} thẻ/ngày**

{hôm nay bạn đã đạt goal / hôm nay bạn chưa đạt goal — chọn câu phù hợp}
```

**Notes:**
- If streak = 0: "Bạn chưa có streak. Học ngày hôm nay để bắt đầu chuỗi nhé!"
- If goal met today: "Hôm nay bạn đã đạt mục tiêu. Tốt lắm!"
- If goal not met: "Hôm nay bạn còn thiếu {remaining} thẻ để đạt mục tiêu."

---

### 2.5 MyProgress — No data

**Trigger:** `UserIntent.MyProgress` + no snippets  
**Pattern:**

```text
Mình chưa tìm thấy dữ liệu tiến độ học của bạn.

Hãy bắt đầu học một bộ từ vựng để hệ thống ghi nhận tiến độ nhé!
```

---

### 2.6 CardLookup — Found

**Trigger:** `UserIntent.CardLookup` + snippets returned  
**Pattern:**

```text
Mình tìm thấy từ "{searchTerm}" trong bộ từ **{SetName}**:

- **Từ:** {term}
- **Định nghĩa:** {definition}
{- **Phiên âm:** {phonetic} — nếu có}
{- **Ví dụ:** {example} — nếu có}
```

**Notes:**
- Only show fields that are non-empty.
- If multiple matches: "Từ này xuất hiện trong {n} bộ từ:" then list each.

---

### 2.7 CardLookup — Not found

**Trigger:** `UserIntent.CardLookup` + no snippets  
**Pattern:**

```text
Mình không tìm thấy từ "{searchTerm}" trong các bộ từ vựng của bạn.

Bạn có thể thêm từ này vào bộ từ mới hoặc tìm kiếm với từ khác.
```

---

### 2.8 SpeakingSuggestion — Data found

**Trigger:** `UserIntent.SpeakingSuggestion` + snippets returned  
**Pattern:**

```text
Một số bài speaking phù hợp với bạn:
- **{VideoTitle1}** — Level {level1}, Chủ đề: {topic1}
- **{VideoTitle2}** — Level {level2}, Chủ đề: {topic2}
- **{VideoTitle3}** — Level {level3}, Chủ đề: {topic3}

Vào trang **Speaking** để xem danh sách đầy đủ và bắt đầu luyện tập.
```

**Notes:**
- Cap at 3 suggestions.
- Level displayed as-is (A1, B1, etc.).
- Always end with link hint to `/Speaking`.

---

### 2.9 SpeakingSuggestion — No data

**Trigger:** `UserIntent.SpeakingSuggestion` + no snippets  
**Pattern:**

```text
Mình chưa tìm thấy bài speaking phù hợp trong hệ thống lúc này.

Bạn có thể vào trang **Speaking** để duyệt toàn bộ playlist và video theo trình độ.
```

---

### 2.10 ClassInfo — Data found

**Trigger:** `UserIntent.ClassInfo` + snippets returned  
**Pattern:**

```text
Bạn đang tham gia {count} lớp học:
- **{ClassName1}** — Vai trò: {role1}, Thành viên: {memberCount1}
- **{ClassName2}** — Vai trò: {role2}, Thành viên: {memberCount2}

{Nếu là chủ lớp: "Bạn là Chủ lớp (Owner) của lớp **{OwnerClassName}**."}
```

**Notes:**
- Role values: "Chủ lớp" (Owner) hoặc "Thành viên" (Member).
- Cap display at 5 classes.

---

### 2.11 ClassInfo — No data

**Trigger:** `UserIntent.ClassInfo` + no snippets  
**Pattern:**

```text
Bạn chưa tham gia lớp học nào.

Bạn có thể vào trang **Lớp học** để tạo lớp mới hoặc tham gia lớp học bằng mã mời.
```

---

### 2.12 WebsiteGuide — Found

**Trigger:** `UserIntent.WebsiteGuide` + guide snippets matched  
**Pattern:**

```text
{guide.body — already formatted as step-by-step}

{Nếu có route: Bạn có thể truy cập ngay tại: {route}}
```

**Notes:**
- Use guide `body` text verbatim from `website-guides.json`.
- Append the route only if non-null in the guide entry.
- If fuzzy match, pick the guide with highest title-similarity score.

---

### 2.13 WebsiteGuide — No match

**Trigger:** `UserIntent.WebsiteGuide` + no guide matched  
**Pattern:**

```text
Mình chưa tìm thấy hướng dẫn phù hợp với câu hỏi của bạn.

Các chủ đề mình có thể hỗ trợ bao gồm: tạo bộ từ, tạo lớp học, các chế độ học, luyện speaking và cài đặt tài khoản.
Bạn có thể hỏi lại theo một trong những chủ đề này.
```

---

### 2.14 StudyRecommendation — Data found

**Trigger:** `UserIntent.StudyRecommendation` + snippets returned  
**Pattern:**

```text
Dựa trên dữ liệu học của bạn, mình gợi ý:
- Ôn lại bộ **{SetName}** — còn {count} thẻ chưa thành thạo.
{- Streak hiện tại: {n} ngày. Học hôm nay để duy trì chuỗi!}
{- Bạn chưa đạt mục tiêu hôm nay. Cần thêm {remaining} thẻ.}

Chế độ ôn tập phù hợp: Flashcard hoặc Quiz.
```

**Notes:**
- Always tie the recommendation to actual user data.
- Do not invent study advice unrelated to user's sets/progress.

---

### 2.15 StudyRecommendation — No data

**Trigger:** `UserIntent.StudyRecommendation` + no snippets  
**Pattern:**

```text
Mình chưa có đủ dữ liệu học để đưa ra gợi ý cụ thể.

Hãy tạo bộ từ đầu tiên và bắt đầu học để mình có thể theo dõi tiến độ và đề xuất phù hợp hơn.
```

---

### 2.16 OutOfScope

**Trigger:** `UserIntent.OutOfScope` (or confidence < 0.55 fallback)  
**Pattern:**

```text
Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English như bộ từ vựng, tiến độ học, lớp học, speaking và cách sử dụng website.

Bạn có thể hỏi theo các dạng như:
- Tôi có những bộ từ vựng nào?
- Tiến độ học của tôi ra sao?
- Gợi ý bài speaking phù hợp
- Cách tạo lớp học hoặc tạo set
```

**IMPORTANT rules for OutOfScope:**
- Do NOT attempt to partially answer a general English question.
- Do NOT say "tôi không biết" — redirect with supported topics instead.
- Keep refusal text **identical** every time for consistency (template, not generated).

---

## 3. General Formatting Rules

| Rule | Detail |
|---|---|
| Language | Vietnamese. English proper nouns (Set, Class, Flashcard, Quiz) are **not** translated. |
| Length | Max 6-8 lines for normal answers. Greeting and OutOfScope may be shorter. |
| Lists | Use `-` for unordered lists. Never `*` or `•`. |
| Bold | Use `**…**` only for entity names (set name, class name, route name). |
| Numbers | Vietnamese style: "3 bộ từ" not "3 sets". |
| Emoji | Only 1 emoji allowed, only in Greeting (`👋`). Never in data answers. |
| Suggestions | Every in-scope answer ends with 1 next-action suggestion. |
| Route hints | Include the page name (e.g., trang **Lớp học**), not the raw URL. |

---

## 4. No-Data vs Out-of-Scope Distinction

| Scenario | Label | Example |
|---|---|---|
| Intent is supported, but user has no data | No-data-found | "Bạn chưa có bộ từ vựng nào." |
| Intent is out of the system's scope | OutOfScope | "Mình chỉ hỗ trợ câu hỏi về TCT English." |
| Intent recognized but guide missing | No-guide-found | "Mình chưa tìm thấy hướng dẫn." |

Never conflate these three. A user asking about a feature they haven't used yet is **No-data-found**, not **OutOfScope**.

---

## 5. Canonical Vietnamese Term Mappings

AI composer MUST use these canonical terms consistently:

| Concept | Use this term | Do NOT use |
|---|---|---|
| Bộ từ vựng | "bộ từ vựng" | "bộ thẻ", "tập flashcard" |
| Thẻ từ | "thẻ" | "card", "từ thẻ" |
| Lớp học | "lớp học" | "nhóm học", "class" |
| Tiến độ | "tiến độ học" | "progress", "kết quả" |
| Thành thạo | "đã thành thạo" | "mastered", "thuộc" |
| Đang học | "đang học" | "learning", "chưa thuộc" |
| Chưa bắt đầu | "chưa bắt đầu" | "new card", "chưa học" |
| Chủ lớp | "Chủ lớp" | "Owner", "Admin lớp" |
| Thành viên | "Thành viên" | "Member", "học viên" |
| Bộ từ mục tiêu | "mục tiêu học hàng ngày" | "daily goal", "target" |
