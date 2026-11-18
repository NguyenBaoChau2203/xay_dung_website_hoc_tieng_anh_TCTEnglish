# TCT Image Upload pattern

Canonical UI for uploading a single image in Admin (and reusable learner tools).
Values come from [DESIGN.md](../../DESIGN.md) and `--tct-*` tokens — do not invent a second palette.

## When to use

| Pattern | Use |
| --- | --- |
| **Modal** (default for authoring) | Edit/Create screens where the page already has a save form; stage file/URL then POST with the parent form |
| **Inline** | Only when the whole page *is* the upload step and a modal adds friction |

Do **not** invent a parallel AJAX upload endpoint unless the feature explicitly requires it. Prefer staging into the parent MVC form (`IFormFile` + antiforgery).

## Assets

| Piece | Path |
| --- | --- |
| Spec (this file) | `docs/design-system/tct-image-upload.md` |
| Partial | `TCTEnglish/Views/Shared/_TctImageUploadModal.cshtml` |
| CSS | `TCTEnglish/wwwroot/css/tct-image-upload.css` |
| JS | `TCTEnglish/wwwroot/js/tct-image-upload.js` |
| Gallery | `docs/design-system/preview.html` → `#image-upload` |

Root class: `.tct-image-upload`. Always wrap the host page in `tct-theme`.

## States

1. **Empty** — dashed dropzone, image icon, “Thả ảnh vào đây, hoặc duyệt”, supports line.
2. **Dragging** — soft primary fill, stronger dashed border, “Thả tệp vào đây”.
3. **Uploading** — file row with thumb, name, size, progress %, cancel selection.
4. **Completed** — progress 100% + success check; primary **Import** enabled.

Also: separator “hoặc”, URL field + Upload URL, footer Cancel + Import.

## Copy (Vietnamese)

- Title: `Tải ảnh` (feature may override, e.g. `Tải ảnh bài đọc`)
- Drop: `Thả ảnh vào đây, hoặc duyệt`
- Dragging: `Thả tệp vào đây`
- Supports: `Hỗ trợ: PNG, JPG, JPEG, WEBP, GIF` (trim to policy)
- URL label: `Thêm URL tệp`
- Buttons: `Hủy`, `Import`, URL button `Tải URL`

## Tokens

Use only existing tokens: `--tct-surface`, `--tct-border`, `--tct-primary*`, `--tct-primary-soft`, `--tct-success*`, `--tct-text`, `--tct-text-muted`, `--tct-radius-lg` / `md`, `--tct-shadow-overlay` / `soft`, `--tct-dur`, `--tct-ease`, `--tct-target-min`, `--tct-focus-ring`.

## Accessibility

- `role="dialog"` + `aria-modal="true"` + labelled title.
- Esc closes; focus returns to the open trigger.
- Dropzone is keyboard-activable (browse control).
- Progress announces via `aria-valuenow` / polite status text.
- Respect `prefers-reduced-motion` (no long progress animation).

## MVC wiring

1. Parent form: `enctype="multipart/form-data"`, antiforgery.
2. Hidden/staged fields: file input, URL input, optional remove flag.
3. Modal Import copies selection into those fields and updates the page thumbnail.
4. Real persistence happens on the parent Save POST (e.g. `IFileStorageService` + feature policy).

## Anti-patterns

- Glassmorphism / purple gradients / fake Help Centre links.
- Hover-only dropzone with no keyboard path.
- Storing blobs only in JS without a form field for POST.
- New hex colours or icon CDNs.
