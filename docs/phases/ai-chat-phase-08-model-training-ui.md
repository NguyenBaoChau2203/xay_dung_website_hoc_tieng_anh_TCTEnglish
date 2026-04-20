# AI Chatbox - Phase 08: Model Training UI

> Extension Phase: Adding an Admin UI tool to train the ML.NET internal dataset
> natively from the web interface, with automatic hot-reload of the trained model.

## 1. Task Definition

Implement an intuitive, one-click admin UI that reads the `intent-samples.seed.csv`
dataset, trains an ML.NET SDCA multiclass classification pipeline, securely saves
the output as `intent-classifier-model.zip` into the
`TCTEnglish/Services/AI/Internal/Data/` directory, and **automatically hot-reloads**
the new model into the running `MlNetAiQueryClassifier` without requiring an
application restart.

### Area

- Backend: `TCTEnglish/Services/AI/Internal/`
- Controllers: `TCTEnglish/Areas/Admin/Controllers/`
- ViewModels: `TCTEnglish/Areas/Admin/ViewModels/`
- Views: `TCTEnglish/Areas/Admin/Views/AiManagement/`

### Rules

1. **CPU-bound training**: ML.NET training is CPU-bound, not I/O-bound. Wrap
   the training pipeline in `Task.Run()` to avoid blocking the request thread.
   Use standard `async/await` only for I/O operations (file reads/saves).
2. **Security checks**: Controller must be locked behind `[Area("Admin")]` and
   `[Authorize(Roles = "Admin")]` with `[ValidateAntiForgeryToken]` on the POST
   training action.
3. **No domain leakage**: The trainer service must reuse `MlNetIntentDatasetLoader`
   for CSV loading and `MlNetIntentClassifierAssetResolver` for path resolution.
   Never duplicate those paths.
4. **Resiliency**: If training fails, catch exceptions gracefully and return error
   notices via TempData without bringing down the web host.
5. **ViewModels only**: Never pass raw data to Views; always project to ViewModels
   per AGENTS.md rule.
6. **Hot-reload**: After training, call `MlNetAiQueryClassifier.InvalidateModel()`
   so the next prediction request loads the new model automatically — no app
   restart required.

### Skills Used

- `Admin Panel` (.agent/skills/admin-panel)
- `Feature Scaffold` (.agent/skills/feature-scaffold)

---

## 2. Phase Split

### Phase 8.1 — Shared Types and Backend Trainer Service

- **Goal**: Extract shared ML.NET schema types, add the training result model,
  write the standalone training service, and refactor the classifier for hot-reload.
- **Tasks**:
  1. Create `MlNetTrainingInput.cs` — shared ML.NET input schema (`Text`, `Label`)
     extracted from the private `MlNetInput` class inside `MlNetAiQueryClassifier`.
  2. Create `MlNetTrainingResult.cs` — training outcome record with `Success`,
     `SampleCount`, `IntentCount`, `MicroAccuracy`, `MacroAccuracy`,
     `ErrorMessage`, and `Duration`.
  3. Create `IMlNetTrainerService.cs` with a single method:
     `Task<MlNetTrainingResult> TrainAndSaveModelAsync(CancellationToken ct)`.
  4. Create `MlNetTrainerService.cs`:
     - Inject `MlNetIntentDatasetLoader` and `MlNetIntentClassifierAssetResolver`.
     - Load dataset via `LoadSeedDatasetAsync()`.
     - Convert `MlNetIntentDatasetExample` → `MlNetTrainingInput` list.
     - Build ML.NET pipeline:
       `TextFeaturizing("Features", "Text")` →
       `MapValueToKey("Label")` →
       `SdcaMaximumEntropy` →
       `MapKeyToValue("PredictedLabel")`.
     - Wrap training in `Task.Run()` (CPU-bound).
     - Evaluate with cross-validation; capture Micro/Macro accuracy.
     - Save model to `ResolveSnapshot().ModelArtifactAbsolutePath`.
     - Return `MlNetTrainingResult`.
  5. Refactor `MlNetAiQueryClassifier.cs`:
     - Replace private `MlNetInput` with shared `MlNetTrainingInput`.
     - Replace `Lazy<ModelRuntime?>` with a `volatile` field + lock pattern.
     - Add public `void InvalidateModel()` method that clears the cached runtime
       so the next `Classify()` call triggers a fresh load from disk.
  6. Register `IMlNetTrainerService` → `MlNetTrainerService` as Scoped in `Program.cs`.

#### Files

| Action | File |
|--------|------|
| NEW | `Services/AI/Internal/MlNetTrainingInput.cs` |
| NEW | `Services/AI/Internal/MlNetTrainingResult.cs` |
| NEW | `Services/AI/Internal/IMlNetTrainerService.cs` |
| NEW | `Services/AI/Internal/MlNetTrainerService.cs` |
| MODIFY | `Services/AI/Internal/MlNetAiQueryClassifier.cs` |
| MODIFY | `Program.cs` |

---

### Phase 8.2 — Admin Controller and ViewModel

- **Goal**: Scaffold the `AiManagementController` with proper ViewModels.
- **Tasks**:
  1. Create `AiManagementViewModel.cs` in `Areas/Admin/ViewModels/`:
     - `bool ModelExists`
     - `string ModelPath`
     - `long? ModelFileSizeBytes`
     - `DateTime? ModelLastModifiedUtc`
     - `bool DatasetExists`
     - `string DatasetPath`
     - `int? DatasetSampleCount`
  2. Create `AiManagementController.cs` in `Areas/Admin/Controllers/`:
     - `[Area("Admin")]`, `[Authorize(Roles = "Admin")]`.
     - Inject `MlNetIntentClassifierAssetResolver`, `MlNetIntentDatasetLoader`,
       `IMlNetTrainerService`, and `MlNetAiQueryClassifier`.
     - `GET Index`: Resolve snapshot, build `AiManagementViewModel`
       (including file size and last-modified from `FileInfo` if model exists,
       and dataset sample count from `LoadSeedDatasetAsync()`), return View.
     - `POST TrainModel`: Guarded with `[ValidateAntiForgeryToken]`.
       Call `_trainerService.TrainAndSaveModelAsync(ct)`.
       On success, call `_classifier.InvalidateModel()` for hot-reload.
       Serialize `MlNetTrainingResult` into `TempData["TrainingResult"]`.
       Redirect to `Index`.
       On exception, set `TempData["TrainingError"]` and redirect.

#### Files

| Action | File |
|--------|------|
| NEW | `Areas/Admin/ViewModels/AiManagementViewModel.cs` |
| NEW | `Areas/Admin/Controllers/AiManagementController.cs` |

---

### Phase 8.3 — Frontend Integration

- **Goal**: Provide the visual experience with Bootstrap 5 and SweetAlert2.
- **Tasks**:
  1. Create `Index.cshtml` in `Areas/Admin/Views/AiManagement/`:
     - **Model Status Card** (Bootstrap 5 card):
       - Icon + badge: 🟢 "Đã huấn luyện" or 🔴 "Chưa có model".
       - If model exists: file size (formatted), last modified date.
       - If model missing: guidance text to train.
     - **Dataset Status Card**:
       - Show dataset path, sample count, status badge.
     - **Train Button**:
       - "🚀 Huấn luyện Model" button.
       - On click: SweetAlert2 confirm dialog
         ("Bạn có chắc muốn huấn luyện lại model AI?").
       - On confirm: submit hidden form → show SweetAlert2 loading spinner.
     - **Training Result Toast** (on page load if `TempData["TrainingResult"]` exists):
       - SweetAlert2 success/error toast showing: accuracy, sample count, duration.
     - **Training Error Toast** (if `TempData["TrainingError"]` exists):
       - SweetAlert2 error toast.
  2. Update `_AdminLayout.cshtml` sidebar:
     - Add link to AI Management under the "Hệ thống" section:
       ```html
       <li class="nav-item">
           <a asp-area="Admin" asp-controller="AiManagement" asp-action="Index"
              class="nav-link text-white rounded-2 px-3 py-2
                     @(currentController == "AiManagement" ? "active" : "")">
               <i class="bi bi-robot me-2"></i> AI Management
           </a>
       </li>
       ```

#### Files

| Action | File |
|--------|------|
| NEW | `Areas/Admin/Views/AiManagement/Index.cshtml` |
| MODIFY | `Areas/Admin/Views/Shared/_AdminLayout.cshtml` |

---

## 3. Verification Plan

### Build and Test

```bash
dotnet build TCTEnglish.sln
dotnet test TCTEnglish.Tests --filter "FullyQualifiedName~MlNet"
```

### Manual Verification

1. Login as Admin → Sidebar shows "AI Management" link under "Hệ thống".
2. Navigate to AI Management page → Model Status and Dataset Status cards display
   correct information.
3. Click "Huấn luyện Model" → SweetAlert2 confirm dialog appears.
4. Confirm → Training runs → Redirect back to Index with success toast showing
   accuracy, sample count, and duration.
5. Model file `intent-classifier-model.zip` is created/updated in
   `Services/AI/Internal/Data/`.
6. **Without restarting the app**, test the chatbox → The new model is active
   immediately (hot-reload via `InvalidateModel()`).
7. Test error case: rename `intent-samples.seed.csv` temporarily → Click train →
   Error toast appears gracefully.

---

> Notice: This follows the same protocol as all previous AI Chat phases.
> You must sequentially perform these phases (8.1 → 8.2 → 8.3), ensuring the
> code cleanly compiles before proceeding to the next.
