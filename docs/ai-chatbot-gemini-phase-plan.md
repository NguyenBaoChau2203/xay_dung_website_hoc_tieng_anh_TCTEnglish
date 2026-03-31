# AI Chatbot Gemini-Only Execution Plan

Tai lieu nay la file huong dan duy nhat de AI agent doc va thuc hien tiep tren
nhanh `feature/ai-chatbot`.

Muc tieu cua nhanh sau khi hoan thanh plan nay:

1. Tinh nang `ai-chatbot` chay on dinh theo huong `Gemini-only`.
2. Khong con logic provider loan quanh giua `OpenAI`, `Ollama`, `Gemini`.
3. Code AI chat sach hon, de review hon, de merge vao `master` hon.
4. Owner chi can tu dien Gemini API key va test lai la co the ket thuc nhanh.

Plan nay thay the huong cu pha tron `OpenAI/Ollama/Gemini`.
Neu AI agent doc file nay, huong dung la:

- chot kien truc `Gemini-only`
- giu boundary hien tai
- refactor vua du de gon code
- khong de localhost Ollama o config merge-ready
- chay test va chot branch sach de merge

---

## Agent Rules Bat Buoc

AI agent phai lam dung cac buoc sau truoc khi sua code:

1. Doc `AGENTS.md`.
2. Doc theo dung thu tu:
   - `docs/project-structure.md`
   - `docs/architecture-prioritized-backlog.md`
   - `.ai/context/known-issues.md`
   - `.ai/context/coding-conventions.md`
3. Kiem tra `git status` va diff hien tai truoc khi edit.
4. Chi lam trong boundary dung cua tinh nang:
   - `TCTEnglish/Controllers/AiController.cs`
   - `TCTEnglish/Services/AI/*`
   - `TCTEnglish/ViewModels/AI/*`
   - `TCTEnglish/Views/Ai/*`
   - `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml`
   - `TCTEnglish/wwwroot/js/ai-chat.js`
   - `TCTEnglish/wwwroot/js/ai-chat-launcher.js`
   - `TCTEnglish/wwwroot/css/ai-chat*.css`
   - `TCTEnglish.Tests/*Ai*`
   - `docs/*ai*`
5. Khong dua domain logic moi vao `HomeController`.
6. Khong tao feature screen moi trong `Views/Home/`.
7. Khong overwrite local change khong lien quan.
8. Khong commit secret.
9. Giu UTF-8 khi sua file tieng Viet.
10. Chi sua `Program.cs` va `appsettings*.json` o phase da chi dinh ro rang.

Nguyen tac kien truc cho toan bo plan:

- Muc tieu cuoi cung la `Gemini-only`.
- Duoc giu `IAiProviderClient` de service layer khong bi dinh cung vao vendor.
- Khong can giu `OpenAI/Ollama` de "phong ho" nua, tru khi owner yeu cau ro rang.
- Moi phase phai ket thuc bang mot tom tat ngan bang tieng Viet:
  - ket qua
  - file da doi
  - checks da chay
  - rui ro/blocker con lai

---

## Definition Of Done Cho Toan Nhanh

Nhanh chi duoc xem la xong khi tat ca dieu sau deu dung:

1. App chi wire `GeminiProviderClient`.
2. Khong con `Program.cs` switch provider giua `OpenAI/Gemini`.
3. `appsettings.json` va `appsettings.Development.json` khong tro `localhost:11434`.
4. Default AI model la model Gemini hop le.
5. `GeminiProviderClient` gui `system_instruction` dung semantics cua Gemini.
6. Root repo khong con file rac nhu output redirect nham.
7. AI tests pass.
8. Full test suite pass.
9. Docs cuoi cung mo ta dung huong `Gemini-only`.
10. Owner chi can cap `AI:ApiKey` va smoke test la co the merge.

---

## Phase 0 - Preflight Review Va Chot Write Set

### Muc tieu

Xac nhan hien trang branch, chot chinh xac nhung file can sua trong cac phase sau,
va tranh viec agent nhay vao edit khi chua hieu local worktree.

### Agent phai doc

- `TCTEnglish/Program.cs`
- `TCTEnglish/Services/AI/AiOptions.cs`
- `TCTEnglish/Services/AI/IAiProviderClient.cs`
- `TCTEnglish/Services/AI/AiProviderException.cs`
- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Services/AI/AiContextBuilder.cs`
- `TCTEnglish/Services/AI/GeminiProviderClient.cs` neu da ton tai
- `TCTEnglish/Services/AI/OpenAiProviderClient.cs` neu van ton tai
- `TCTEnglish/appsettings.json`
- `TCTEnglish/appsettings.Development.json`
- `TCTEnglish.Tests/GeminiProviderClientTests.cs` neu da ton tai
- `TCTEnglish.Tests/OpenAiProviderClientTests.cs` neu da ton tai
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

### Agent phai lam

1. Kiem tra `git status --short --branch`.
2. Liet ke cac dau vet OpenAI/Ollama con sot lai:
   - provider switch trong DI
   - default model `gpt-*`
   - localhost Ollama trong config
   - test hoac docs van noi theo huong da-provider
3. Xac nhan file rac neu co o root repo.
4. Chot danh sach file se sua o Phase 1, 2, 3, 4.
5. Khong refactor rong trong phase nay, chi review va chot scope.

### Khong duoc lam

- Khong xoa file code ngay trong Phase 0.
- Khong sua UI neu chua can.
- Khong doi logic controller/service chi de "clean cho dep".

### Tieu chi hoan thanh

- Agent noi ro:
  - file nao se sua
  - file nao se xoa
  - file nao khong duoc dung
- Khong co edit ngoai scope review nho.

### Chuan bi cho phase sau

Sau Phase 0, agent phai san sang buoc vao refactor `Gemini-only` ma khong hoi lai
owner "co giu OpenAI/Ollama nua khong?".

### Cau lenh owner co the dua

`Lam Phase 0 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Phase 1 - Refactor Kien Truc Sang Gemini-Only

### Muc tieu

Loai bo kien truc multi-provider khong can thiet, nhung van giu abstraction sach o
muc `IAiProviderClient`.

### File du kien duoc sua

- `TCTEnglish/Program.cs`
- `TCTEnglish/Services/AI/AiOptions.cs`
- `TCTEnglish/Services/AI/IAiProviderClient.cs` neu can
- `TCTEnglish/Services/AI/OpenAiProviderClient.cs` co the bi xoa o cuoi phase
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- `TCTEnglish.Tests/OpenAiProviderClientTests.cs` co the bi xoa o cuoi phase

### Agent phai lam

1. Trong `Program.cs`, dang ky thang:
   - `builder.Services.AddScoped<IAiProviderClient, GeminiProviderClient>();`
2. Bo logic `switch provider` hoac `if providerName == ...`.
3. Bo `AiProviderNames` neu no chi ton tai de phuc vu provider switching.
4. Neu `AiOptions` dang giu field `Provider` chi de DI switch, bo field do.
5. Chi giu nhung option con can cho Gemini:
   - `ApiKey`
   - `BaseUrl`
   - `Model`
   - `Temperature`
   - `MaxOutputTokens`
   - budget / timeout / retry neu dang duoc dung
6. Sau khi tat ca ref da duoc cap nhat:
   - xoa `OpenAiProviderClient.cs`
   - xoa `OpenAiProviderClientTests.cs`
7. Khong sua business flow `AiChatService` trong phase nay tru khi bat buoc do compile.

### Khong duoc lam

- Khong bo `IAiProviderClient`.
- Khong doi UI.
- Khong doi controller flow neu chi can refactor DI/options la du.

### Tieu chi hoan thanh

- App compile voi mot provider duy nhat la Gemini.
- Khong con code path `OpenAI` hoac `Ollama-via-OpenAI`.
- Khong con docs/test khang dinh app ho tro nhieu provider neu code thuc te da bo.

### Chuan bi cho phase sau

Sau Phase 1, app da ro kien truc. Phase 2 chi tap trung vao runtime behavior dung
cua `GeminiProviderClient`, khong phai xu ly DI confusion nua.

### Cau lenh owner co the dua

`Lam Phase 1 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Phase 2 - Sua GeminiProviderClient Cho Dung Semantics Va On Dinh

### Muc tieu

Dam bao request/response cua Gemini duoc map dung, dac biet la `system_instruction`,
model default, auth/config errors, timeout, retry va parsing.

### File du kien duoc sua

- `TCTEnglish/Services/AI/GeminiProviderClient.cs`
- `TCTEnglish/Services/AI/AiOptions.cs`
- `TCTEnglish/Services/AI/AiProviderException.cs` neu can them/doi error code
- `TCTEnglish.Tests/GeminiProviderClientTests.cs`

### Agent phai lam

1. Xac dinh ro default Gemini:
   - `BaseUrl` mac dinh theo Google Gemini REST API
   - `Model` mac dinh la model Gemini hop le
2. Sua payload request:
   - message `system` dau tien phai map vao `system_instruction`
   - `assistant` -> `model`
   - `user` -> `user`
   - khong dua `system` vao `contents` nhu mot `user` message nua
3. Parse response sao cho:
   - lay text assistant dung
   - lay usage metadata dung
   - fail ro rang khi empty response
4. Bao loi ro rang khi:
   - sai API key / auth
   - rate limit
   - provider unavailable
   - timeout
   - network
   - invalid configuration
5. Neu `AiOptions` dang co default model khong phu hop cho Gemini, sua ngay trong phase nay.

### Test bat buoc phai co

1. Success path.
2. Empty response.
3. `401/403`.
4. `429`.
5. `5xx`.
6. Timeout.
7. Network exception.
8. Request body co `system_instruction`.
9. Default model path khong roi vao `gpt-*`.

### Khong duoc lam

- Khong doi giao dien.
- Khong doi controller flow.
- Khong skip test provider-level.

### Tieu chi hoan thanh

- `GeminiProviderClient` hoat dong dung theo huong Gemini.
- Tests provider-level bao ve duoc cac edge case quan trong.
- Khong con bug "Provider = Gemini nhung model mac dinh van la gpt-4o-mini".

### Chuan bi cho phase sau

Sau Phase 2, agent co the wiring config mau va cleanup repo ma khong lo runtime
Gemini van sai nen test manual tro nen vo nghia.

### Cau lenh owner co the dua

`Lam Phase 2 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Phase 3 - Sua Config Mau Va Don Repo Theo Huong Gemini-Only

### Muc tieu

Lam cho repo o trang thai merge-ready ve mat config va hygiene:
khong con localhost Ollama, khong con file rac, khong con docs/test gay nhieu.

### File du kien duoc sua

- `TCTEnglish/appsettings.json`
- `TCTEnglish/appsettings.Development.json`
- `docs/ai-chatbot-gemini-phase-plan.md` neu can cap nhat nhe
- `docs/ai-chatbot-gemini-owner-checklist.md` neu can dong bo thong tin
- file rac o root repo neu co
- test/docs con noi theo huong OpenAI/Ollama neu van sot lai

### Agent phai lam

1. Doi config mau sang Gemini:
   - `AI:BaseUrl` tro toi Gemini
   - `AI:Model` la model Gemini hop le
   - `AI:ApiKey` de rong trong repo
   - khong giu `AI:Provider` trong config mau
2. Khong commit API key that.
3. Xoa file rac o root repo neu no chi la output redirect nham.
4. Xoa comment/doc/test setup van mo ta OpenAI/Ollama neu khong con dung.
5. Neu owner checklist dang viet theo huong multi-provider, cap nhat lai thanh
   Gemini-only.

### Khong duoc lam

- Khong dua key that vao `appsettings*.json`.
- Khong de `localhost:11434` ton tai trong config merge-ready.
- Khong xoa docs neu chua co file thay the ro rang.

### Tieu chi hoan thanh

- Repo sample config phan anh dung huong Gemini-only.
- Khong con file rac.
- Docs chinh khong con mau thuan voi code.

### Chuan bi cho phase sau

Sau Phase 3, moi verification o Phase 4 se phan anh sat thuc te merge/deploy hon.

### Cau lenh owner co the dua

`Lam Phase 3 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Phase 4 - Verification Sau Refactor Va Fix Regression Neu Co

### Muc tieu

Chung minh branch van on sau khi refactor Gemini-only, va sua regression neu test
hoac smoke flow phat hien van de.

### File du kien duoc sua

- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- test helper lien quan neu can
- cac file trong AI boundary neu verification lam lo regression thuc su

### Agent phai chay

1. `dotnet test TCTEnglish.Tests -c Release --no-restore --filter Ai`
2. `dotnet test TCTEnglish.Tests -c Release --no-restore`

### Manual smoke toi thieu can huong dan hoac tu thuc hien neu du dieu kien

1. Mo `/AI/Chat`.
2. Gui first message khi chua co `conversationId`.
3. Kiem tra draft conversation duoc tao dung.
4. Kiem tra lich su hoi thoai hien dung.
5. Kiem tra launcher embed van mo dong duoc.
6. Kiem tra `429`.
7. Kiem tra `503`.
8. Kiem tra message markdown dai van render duoc.

### Neu test fail

Agent phai:

1. Chi sua trong AI boundary.
2. Uu tien fix regression do phase 1-3 tao ra.
3. Khong mo rong scope sang feature khac.

### Tieu chi hoan thanh

- AI tests pass.
- Full suite pass.
- Khong con regression ro rang trong chat flow.

### Chuan bi cho phase sau

Sau Phase 4, branch da san sang cho merge-readiness pass cuoi.

### Cau lenh owner co the dua

`Lam Phase 4 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Phase 5 - Merge Readiness Va Handoff Cuoi

### Muc tieu

Dong branch o trang thai sach, de owner chi can bo API key va merge.

### Agent phai lam

1. Kiem tra lan cuoi:
   - khong con OpenAI/Ollama leftovers
   - khong con localhost Ollama trong config merge-ready
   - khong con file rac
   - docs huong dan owner phu hop voi code hien tai
2. Chot lai exact config owner phai dien:
   - `AI:ApiKey`
   - `AI:BaseUrl` neu con de mo
   - `AI:Model` neu owner muon doi model
3. Khong tao commit neu owner chua yeu cau.
4. Bao cao cuoi bang tieng Viet, ngan gon, ro:
   - ket qua
   - file da doi
   - checks da chay
   - rui ro/blocker con lai

### Tieu chi hoan thanh

- Owner nhin vao la biet:
  - code da Gemini-only
  - can dien key o dau
  - can test gi truoc khi merge

### Cau lenh owner co the dua

`Lam Phase 5 trong docs/ai-chatbot-gemini-phase-plan.md cho toi`

---

## Thu Tu Khuyen Nghi

Lam dung thu tu:

1. Phase 0
2. Phase 1
3. Phase 2
4. Phase 3
5. Phase 4
6. Phase 5

Khong nen nhay thang vao verification truoc khi cleanup kien truc va config.

---

## Muc Tieu Cuoi Cung Cho Agent

Neu agent doc den day, ket luan dung la:

- Nhanh nay phai ket thuc theo huong `Gemini-only`.
- Khong can giu `OpenAI/Ollama` de dev free nua.
- Giu abstraction vua du, khong over-engineer.
- Uu tien code sach, config ro rang, test on, docs de owner dung.
