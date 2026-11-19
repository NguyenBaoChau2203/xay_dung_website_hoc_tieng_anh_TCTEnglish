# Tico in the shared page loader

Owner requests replacing the graduation-cap loader image with the project's fox.
One agent, current feature branch, local only. This is a small visual change;
reuse the shipped transparent peek sticker, without generation or dependencies.

- [x] Locate _Loader and page-loader.css. Consumers: learner _Layout and Admin
  _AdminLayout, both include the same partial and stylesheet. The owner's screenshot
  records the existing cap, circle, brand/status and progress track.
- [x] Replace the decorative cap with an eager, versioned local Tico image.
  Preserve loader IDs, script/global APIs, progress/status text and lifecycle.
  Size the image without distortion or the cap's opaque blue core.
- [x] Build and run the existing focused loader checks. Inspect Home/Admin loader
  in a browser at 1440/768/375–390, Escape/show/dismiss and reduced motion; record
  actual observations and unobserved zoom/hosting. No new unit test for this
  reversible asset/markup substitution. Leave the authorized local web running.

No Program.cs, appsettings, project, schema, secret, data, paid speech test or
release changes. Do not commit, push, create a PR or deploy.

## Verification

- Existing Node loader/dropdown suite: 12 passed, 0 failed/skipped. Loader script
  byte comparison after newline normalization matches HEAD exactly; lifecycle and
  all IDs/global APIs are unchanged. No new unit test for a visual substitution.
- Release launcher build/migration check succeeded; health/ready HTTP 200. The
  first restart hit the old launcher's held log handle, so a new ignored
  loader-tico-local-web.log was used without interrupting unrelated workloads.
- Real saved-local-account Chrome checks: Home and Admin/Dashboard, each at
  1440/768/390/375 (8 cases), loaded mascot, exact 104px box with object-fit contain,
  visible/in-bounds card, preserved status, show/Escape/dismiss, two reduced-motion
  cases, zero page errors. Screenshots inspected. No real voice/provider request.
- Browser fixture waits for the existing card-pop animation before measuring;
  an initial mid-animation width of 103.46px was a fixture timing failure, not a
  reason to change the loader behavior. Initial-load failsafe also settles first.
- Native zoom 200%, sidebar variants and live hosting were not separately
  observed for this fixed-overlay change. No claim these checks passed.
- Ignored evidence: .appdata/local/ai-voice-tests/loader-tico-evidence.json and
  loader-tico-{learner,admin}-{1440,375}.png. Source changes are only _Loader and
  page-loader.css; the existing mascot is reused without modification/generation.
