# Goals Finalization Playbook

> Tài liệu này là playbook thực thi cho phần việc còn lại để hoàn thiện trang `Goals` ở mức production-ready.
> Khi giao việc cho agent, chỉ cần chỉ rõ phase trong tài liệu này.

## 1. Mục đích

Biến trang `Goals` từ trạng thái "đã có logic chính nhưng chưa hoàn thiện trải nghiệm" thành một flow hoàn chỉnh, ổn định, dễ kiểm thử và nhất quán với toàn bộ ứng dụng.

Tài liệu này tập trung vào:

- Phần còn lại cần làm để hoàn thiện `Goals` 100%.
- Chia nhỏ công việc thành từng phase độc lập nhưng có thứ tự.
- Mỗi phase có sẵn phạm vi, checklist, tiêu chí hoàn thành, cách verify và việc cần chuẩn bị cho phase sau.

## 2. Tài liệu này có gì khác với file Goals cũ

File [`docs/goals-implementation-execution-plan.md`](./goals-implementation-execution-plan.md) là kế hoạch triển khai Goals từ giai đoạn còn placeholder.

Tại thời điểm hiện tại, code thực tế đã có:

- `GoalsController`
- `IGoalsService` / `GoalsService`
- `GoalsViewModel`
- `UserDailyActivity`
- cập nhật goal bằng form POST
- weekly activity chart
- activity recording từ learning flow
- badge progress và badge unlock
- integration tests `GoalsPhase1` đến `GoalsPhase5`

Vì vậy:

- Không dùng file cũ làm nguồn sự thật cho phần việc còn lại.
- File này là playbook chuẩn cho phần việc **chưa xong**.

## 3. Baseline hiện tại của code

### 3.1 Những gì đã có thật trong code

- `GET /Goals` trả về dữ liệu thật từ `GoalsService`.
- `POST /Goals/UpdateGoal` đã cập nhật `User.Goal`.
- `Goals` page đã có modal editor, anti-forgery token, success toast.
- `Goals` page đã đọc `UserDailyActivity` cho biểu đồ 7 ngày.
- `LearningApiController` đã gọi vào `IGoalsService` để ghi nhận activity.
- Badge UI và badge data đã có dữ liệu thật.
- Test hiện có cho các phase cũ đều pass.

### 3.2 Những khoảng trống còn lại trước khi coi là hoàn thiện 100%

- Nút mở modal tạo/chỉnh sửa goal nhiều khả năng chưa hoạt động ổn định trên UI vì main layout chưa nạp Bootstrap JS bundle.
- Text/UX hiện chưa phân biệt rõ trạng thái "chưa có goal" và "đã có goal".
- Chưa có lớp verify đủ mạnh ở mức browser/UI interaction cho flow mở modal, submit, invalid submit, success state.
- Tài liệu hiện tại của repo vẫn còn chỗ mô tả Goals như placeholder hoặc chưa phản ánh chính xác trạng thái mới.
- Chưa có playbook cuối cùng dành riêng cho việc đóng nốt phần còn lại của Goals.

## 4. Nguồn sự thật cần đọc trước khi làm bất kỳ phase nào

Agent được giao phase nào cũng phải đọc ít nhất:

- `AGENTS.md`
- `docs/project-structure.md`
- `docs/architecture-prioritized-backlog.md`
- `.ai/context/known-issues.md`
- `.ai/context/coding-conventions.md`
- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/js/goals.js`
- `TCTEnglish/wwwroot/css/goals.css`
- `TCTEnglish/Views/Shared/_Layout.cshtml`
- các test `TCTEnglish.Tests/GoalsPhase*.cs`

## 5. Những rule không được vi phạm trong tất cả các phase

- Không thêm domain logic mới vào `HomeController`.
- Không truyền EF entity trực tiếp ra view.
- Không bỏ anti-forgery ở bất kỳ mutation nào.
- Không parse claims thủ công; dùng `GetCurrentUserId()` hoặc helper hiện có.
- Không sửa `Program.cs`, `appsettings.json`, `.csproj` nếu chưa có yêu cầu rõ ràng.
- Không tạo migration mới trong playbook này trừ khi có thay đổi thực sự về schema; mặc định kế hoạch này **không yêu cầu migration**.
- Nếu agent phát hiện phase mình đang làm cần thay đổi ngoài phạm vi này, phải dừng và nêu rõ lý do.

## 6. Định nghĩa "Goals hoàn thiện 100%"

Trang `Goals` chỉ được coi là hoàn thiện khi tất cả điều sau đều đúng:

1. Người dùng có thể mở flow tạo/chỉnh sửa goal từ mọi CTA liên quan.
2. Flow submit thành công, thất bại và validation error đều hiển thị đúng, không gây kẹt UI.
3. UI phân biệt rõ trạng thái chưa có goal và đã có goal.
4. Trang hoạt động ổn định trên mobile và desktop.
5. Có regression coverage đủ để bắt lỗi UI contract quan trọng.
6. Tài liệu trong repo không còn mô tả Goals là placeholder.

## 7. Phase Overview

| Phase | Tên | Mục tiêu chính |
|---|---|---|
| `Phase 0` | Preflight | Đồng bộ trạng thái thực tế trước khi sửa |
| `Phase 1` | Functional Unblock | Làm cho nút tạo/chỉnh sửa goal hoạt động ổn định |
| `Phase 2` | UX Completion | Hoàn thiện text, state, feedback và create/edit semantics |
| `Phase 3` | UI Hardening | Hoàn thiện responsive, accessibility và UI polish bắt buộc |
| `Phase 4` | Verification | Bịt lỗ hổng test cho UI contract và browser flow |
| `Phase 5` | Documentation Closeout | Đồng bộ lại docs và chốt trạng thái hoàn thành |

---

## 8. Phase 0 - Preflight

### Mục tiêu

Xác nhận lại trạng thái thật của code trước khi bắt đầu phase implementation.

### Agent cần làm gì

1. Đọc các file trong mục 4.
2. Xác nhận các phần sau đã tồn tại:
   - `GoalsController.Index`
   - `GoalsController.UpdateGoal`
   - `GoalsService.GetGoalsAsync`
   - `GoalsService.UpdateGoalAsync`
   - modal editor trong `Views/Goals/Index.cshtml`
   - `GoalsPhase1IntegrationTests` đến `GoalsPhase5IntegrationTests`
3. Kiểm tra `_Layout.cshtml` có đang nạp Bootstrap CSS nhưng thiếu Bootstrap JS hay không.
4. Chạy test Goals hiện có để lấy baseline pass/fail.

### Files cần đọc kỹ

- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/js/goals.js`
- `TCTEnglish.Tests/GoalsPhase1IntegrationTests.cs`
- `TCTEnglish.Tests/GoalsPhase5IntegrationTests.cs`

### Lệnh verify khuyến nghị

```powershell
$env:DOTNET_CLI_HOME='D:\TCTEnglish\.dotnet-home'
dotnet test TCTEnglish.Tests --filter 'FullyQualifiedName~GoalsPhase' --no-restore
```

### Definition of done

- Agent đã ghi nhận baseline hiện tại.
- Agent đã xác nhận rõ phase tiếp theo cần sửa gì đầu tiên.

### Chuẩn bị cho phase sau

- Chốt quyết định kỹ thuật:
  - Ưu tiên nạp Bootstrap JS ở layout chung hay vá riêng cho Goals.
- Khuyến nghị:
  - Ưu tiên sửa ở layout chung vì repo có nhiều màn khác cũng dùng `data-bs-*`.

---

## 9. Phase 1 - Functional Unblock

### Mục tiêu

Đảm bảo người dùng thực sự mở được flow tạo/chỉnh sửa goal từ UI và submit được bình thường.

### Phạm vi

Chỉ sửa những gì cần để unblock flow chức năng. Không polish text sâu ở phase này.

### Files dự kiến thay đổi

- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/js/goals.js`
- Có thể: `TCTEnglish/wwwroot/css/goals.css`

### Checklist implementation

1. Bổ sung Bootstrap JS bundle vào main layout nếu layout đang thiếu.
2. Đảm bảo các CTA Goals hoạt động:
   - nút đầu trang
   - nút empty-state khi chưa có goal
3. Đảm bảo modal invalid submit vẫn mở lại đúng cách khi server trả về view với `ShowGoalEditor = true`.
4. Đảm bảo `goals.js` không ném lỗi nếu `window.bootstrap` chưa sẵn sàng.
5. Smoke test nhanh các màn khác đang dùng `data-bs-*` trong cùng layout.

### Không được làm trong phase này

- Không redesign Goals page.
- Không đổi logic service.
- Không thêm schema/migration.
- Không thay đổi flow badge.

### Verify bắt buộc

1. Click CTA mở modal thành công.
2. Submit valid value cập nhật goal thành công.
3. Submit invalid value render lại lỗi và modal mở lại.
4. Không làm hỏng modal/alert/dropdown ở các màn khác dùng cùng layout.

### Lệnh verify khuyến nghị

```powershell
$env:DOTNET_CLI_HOME='D:\TCTEnglish\.dotnet-home'
dotnet test TCTEnglish.Tests --filter 'FullyQualifiedName~GoalsPhase1IntegrationTests|FullyQualifiedName~GoalsPhase5IntegrationTests' --no-restore
```

### Definition of done

- Nút mở modal Goals hoạt động ổn định.
- Flow submit hợp lệ và không hợp lệ đều usable.
- Không có regression rõ ràng ở những chỗ khác dùng Bootstrap behaviors.

### Chuẩn bị cho phase sau

- Nếu phase này chạm vào text/DOM, giữ selector ổn định.
- Nếu cần thêm cờ trạng thái create/edit, ghi rõ proposal cho phase 2 nhưng chưa nhất thiết phải làm ngay.

---

## 10. Phase 2 - UX Completion

### Mục tiêu

Làm cho trải nghiệm tạo/chỉnh sửa goal rõ ràng, nhất quán và không gây hiểu nhầm cho người dùng.

### Phạm vi

Tập trung vào semantics create/edit, copy, feedback, empty-state và consistency của Goals page.

### Files dự kiến thay đổi

- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- Có thể: `TCTEnglish/wwwroot/js/goals.js`

### Checklist implementation

1. Phân biệt rõ hai trạng thái:
   - chưa có goal
   - đã có goal
2. Thêm một contract rõ ràng cho view:
   - ví dụ `IsCreatingGoal`
   - hoặc `GoalEditorTitle`
   - hoặc `GoalPrimaryActionText`
3. Đồng bộ text UI:
   - CTA đầu trang
   - CTA empty-state
   - tiêu đề modal
   - nút submit
   - success toast
4. Ưu tiên dùng wording trung tính nếu muốn tránh branch text quá nhiều:
   - ví dụ `"Đã lưu mục tiêu ngày"` thay vì chỉ `"Cập nhật mục tiêu ngày thành công"`
5. Đảm bảo message khi chưa có goal không xung đột với progress block.
6. Đảm bảo validation message và form helper text rõ ràng, tiếng Việt thống nhất.
7. Giữ nguyên contract bảo mật:
   - anti-forgery
   - field prefix `GoalEditor.DailyGoal`
   - owner-bound update

### Không được làm trong phase này

- Không thêm hiệu ứng animation lớn.
- Không đổi business logic tính progress/badges nếu không thật sự cần.
- Không sửa layout ngoài Goals trừ khi là phụ thuộc tối thiểu.

### Verify bắt buộc

1. Người dùng chưa có goal thấy wording kiểu "Tạo/Đặt mục tiêu".
2. Người dùng đã có goal thấy wording kiểu "Chỉnh sửa/Cập nhật mục tiêu".
3. Submit thành công cho cả hai trạng thái vẫn ổn.
4. Invalid submit vẫn giữ đúng text và mở lại modal.

### Test nên có

- HTML/integration test cho state chưa có goal.
- HTML/integration test cho state đã có goal.
- Test cho success toast wording nếu wording được đổi.

### Definition of done

- Goals page không còn cảm giác "nút có mà chưa làm".
- Tạo goal và chỉnh sửa goal là cùng một flow nhưng rõ ràng về mặt UX.

### Chuẩn bị cho phase sau

- Nếu phase 3 cần browser test, thêm selector ổn định ngay từ phase này:
  - `data-testid`
  - hoặc class hook rõ ràng, không dễ đổi tên.

---

## 11. Phase 3 - UI Hardening

### Mục tiêu

Làm cho trang `Goals` đạt mức ổn định production về responsive, accessibility và tương tác UI.

### Files dự kiến thay đổi

- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/css/goals.css`
- `TCTEnglish/wwwroot/js/goals.js`

### Checklist implementation

1. Audit responsive ở các breakpoint tối thiểu:
   - 320px
   - 375px
   - 768px
   - 1024px+
2. Kiểm tra các vùng dễ vỡ layout:
   - progress circle text
   - streak card
   - weekly chart
   - badge cards
   - toast
   - modal footer buttons
3. Kiểm tra keyboard/accessibility:
   - tab đến CTA Goals
   - mở modal
   - đóng modal
   - focus quay lại hợp lý
   - tooltip/bar chart không che nội dung quan trọng
4. Nếu chart tooltip đang là CSS-only, xác nhận vẫn usable bằng keyboard và screen reader tối thiểu.
5. Đảm bảo toast không che nội dung quan trọng trên mobile.
6. Đảm bảo empty-state và badge-state không bị layout jump hoặc overflow.
7. Không thêm animation nặng hoặc confetti.

### Không được làm trong phase này

- Không mở rộng scope sang leaderboard, weekly email, gamification mới.
- Không viết lại toàn bộ CSS nếu chỉ cần chỉnh targeted fixes.

### Verify bắt buộc

1. Trang usable trên mobile và desktop.
2. Không overflow text trên modal và cards.
3. Keyboard flow hoạt động tối thiểu cho create/edit goal.
4. Success toast không che CTA chính trên màn nhỏ.

### Bằng chứng nên thu thập

- screenshot desktop
- screenshot mobile
- note ngắn về các breakpoint đã kiểm tra

### Definition of done

- Goals page không còn issue rõ ràng về responsive/accessibility cơ bản.

### Chuẩn bị cho phase sau

- Ổn định selector/test hook để browser test không mong manh.
- Ghi lại các hành vi UI phải được cover ở phase 4.

---

## 12. Phase 4 - Verification

### Mục tiêu

Bịt lỗ hổng coverage còn thiếu để lỗi kiểu "backend pass nhưng UI không dùng được" không lặp lại.

### Phạm vi

Tập trung vào test cho UI contract và browser behavior của flow Goals.

### Files dự kiến thay đổi

- `TCTEnglish.Tests/GoalsPhase*.cs`
- Có thể thêm test file mới trong `TCTEnglish.Tests/`
- Có thể thêm script verify trong `scripts/` nếu không cần dependency mới

### Checklist implementation

1. Bổ sung integration test cho HTML contract nếu chưa có:
   - layout có Bootstrap JS bundle nếu phase 1 chọn hướng này
   - modal markup tồn tại đúng
   - state create vs edit render đúng text
   - invalid submit trả về HTML với `data-open-on-load="true"`
2. Giữ lại và mở rộng các test quan trọng đã có:
   - anti-forgery
   - invalid field prefix
   - owner-only update
   - success toast
3. Nếu môi trường cho phép browser automation:
   - thêm một browser smoke test hoặc script chạy bằng Playwright
   - cover tối thiểu:
     - mở `/Goals`
     - click CTA
     - nhập value
     - submit
     - thấy success state
4. Nếu browser test chưa thể commit vào repo mà cần dependency mới:
   - không ép thêm package mới
   - ghi rõ cách chạy smoke ngoài repo bằng tooling sẵn có

### Không được làm trong phase này

- Không đổi logic sản phẩm chỉ để làm test pass.
- Không thêm test framework mới nếu phải sửa `.csproj` mà chưa được duyệt.

### Verify bắt buộc

1. `dotnet test` pass cho toàn bộ Goals tests liên quan.
2. Có ít nhất một lớp verify bắt được regression UI contract.
3. Nếu có browser smoke, phải ghi rõ command và expected result.

### Lệnh verify khuyến nghị

```powershell
$env:DOTNET_CLI_HOME='D:\TCTEnglish\.dotnet-home'
dotnet test TCTEnglish.Tests --filter 'FullyQualifiedName~GoalsPhase' --no-restore
```

### Definition of done

- Regression quan trọng của Goals không còn phụ thuộc hoàn toàn vào test service/controller.

### Chuẩn bị cho phase sau

- Ghi lại bằng chứng pass test, screenshot và command thực tế.
- Liệt kê những docs nào đang stale để phase 5 xử lý dứt điểm.

---

## 13. Phase 5 - Documentation Closeout

### Mục tiêu

Đồng bộ toàn bộ docs liên quan sau khi Goals hoàn thiện, để agent hoặc developer khác không bị dẫn sai bởi tài liệu cũ.

### Files dự kiến thay đổi

- `docs/architecture-prioritized-backlog.md`
- `.ai/context/known-issues.md`
- Có thể: `docs/goals-implementation-execution-plan.md`
- Có thể: tài liệu này nếu cần cập nhật trạng thái cuối

### Checklist implementation

1. Tìm tất cả mô tả cũ nói Goals là placeholder hoặc chưa có flow thật.
2. Cập nhật backlog/known-issues cho khớp với trạng thái mới.
3. Nếu `docs/goals-implementation-execution-plan.md` vẫn còn giá trị lịch sử:
   - giữ file
   - thêm note nói đó là plan cũ / historical
4. Nếu có known issue riêng của Goals vừa được fix:
   - cập nhật `.ai/context/known-issues.md`
   - nếu được xem là bug fix, append vào `.ai/context/bug-fix-log.md` theo format hiện có
5. Đảm bảo người đọc repo sau này biết:
   - Goals page là feature thật
   - phần còn lại đã hoàn tất
   - playbook nào là authoritative cho future tweaks

### Không được làm trong phase này

- Không sửa code production nếu không có lý do.
- Không âm thầm đổi trạng thái backlog khi phase trước chưa thật sự pass.

### Verify bắt buộc

1. Search repo không còn những mô tả sai rõ ràng về Goals placeholder.
2. Các docs chính phản ánh đúng trạng thái mới.
3. Handoff note cuối cùng đủ cho team tiếp quản.

### Definition of done

- Docs, backlog, known issues và execution playbook đều đồng bộ với code thật.

### Chuẩn bị cho công việc sau cùng

- Sau phase này có thể coi Goals đã đóng.
- Các cải tiến về sau chỉ nên là enhancement mới, không còn là "hoàn thiện nốt".

---

## 14. Thứ tự thực hiện khuyến nghị

Làm đúng thứ tự sau:

1. `Phase 0`
2. `Phase 1`
3. `Phase 2`
4. `Phase 3`
5. `Phase 4`
6. `Phase 5`

Không nhảy thẳng sang `Phase 4` hoặc `Phase 5` khi `Phase 1` chưa xong, vì vấn đề lớn nhất hiện tại là flow UI có thể chưa mở được modal.

## 15. Mẫu handoff bắt buộc cho mỗi phase

Agent hoàn thành phase nào cũng phải để lại handoff theo format này:

```md
## Goals Finalization - Phase Handoff

### Phase
- Phase:
- Objective:

### Files changed
- ...

### What changed
- ...

### Verification
- Build:
- Tests:
- Manual/browser checks:

### Risks
- ...

### Ready for next phase
- ...
```

## 16. Hướng dẫn gọi agent cực ngắn

Khi giao việc, chỉ cần nói theo mẫu:

- "Đọc `docs/goals-finalization-playbook.md` và thực hiện Phase 1"
- "Đọc `docs/goals-finalization-playbook.md` và thực hiện Phase 3"

Agent phải:

1. đọc phase được giao
2. chỉ làm đúng phạm vi phase đó
3. verify đúng checklist của phase
4. để lại handoff theo mục 15

## 17. Tiêu chí chốt cuối cùng

Khi tất cả phase đã hoàn tất, repo phải đạt các điều kiện sau:

- Goals UI usable thực tế, không chỉ pass integration test.
- Nút tạo/chỉnh sửa mục tiêu hoạt động và rõ nghĩa.
- Flow validation/success có feedback ổn định.
- Responsive và accessibility cơ bản đạt chuẩn nội bộ.
- Docs không còn dẫn sai trạng thái của Goals.

