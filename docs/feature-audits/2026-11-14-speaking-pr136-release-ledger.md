# Speaking PR136 integration and staging release

Owner authorized branch push, PR creation, merge to master and CI/staging monitoring
on 2026-11-14. Production gate must remain false. This ledger is updated with the
actual final release evidence before handoff; pending stages are not success.

## Initial state

- Branch fix/speaking-catalog-transcript-polish, seven local commits beyond master;
  no divergent master commits and no PR merge conflict.
- Local web port5100 was already stopped; no other process was terminated.
- Staging deployment gate true; production deployment gate false, unchanged.
- PR136: https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/pull/136
- Public content packages pass immutable-history validation against origin/master.
  Historical byte-preserved CRLF packages are reviewed with cr-at-eol whitespace
  interpretation; their published bytes are not normalized or rewritten.

## CI repair

First app run43536075626 passed application tests but failed browser regressions:
269 passed,1 failed. test_speaking_catalog_local_packages.cjs depended on ignored
local acquisition drafts. Added portable nonsecret evidence summaries for90 public
sources and made the same transcript/FK/checksum/timing checks use tracked inputs.
A fresh fixture with only public manifests/evidence/packages and the test passes
without an .appdata directory. No app/schema/package bytes changed in this repair.
Full audio/timing QA remains pending; staging authorization does not turn source
captions into a completed acoustic review. Final CI and staging evidence pending.
