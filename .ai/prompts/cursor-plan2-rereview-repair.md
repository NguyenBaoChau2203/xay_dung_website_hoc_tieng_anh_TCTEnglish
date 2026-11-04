# Cursor Auto — thực thi sửa vòng2 và hoàn tất Plan2

Owner giao thực thi trên nhánh hiện tại`feature/chau-ui-performance-audit`.
Một agent inline, không subagent/CLI AI/chat khác. Giữ toàn UIPlan1/dirtywork/
BTH2/data/workload. Website vẫn OpenAI`gpt-6-luna`/low, audioWhisper; IDE riêng.

Đọc AGENTS và neededskills, CodeGraph trước indexedsource; load activebatch:

1. `docs/feature-audits/2026-11-02-cursor-plan2-rereview.md`.
2. `docs/superpowers/plans/2026-11-02-cursor-plan2-rereview-repair.md`.
3. Ledger `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
4. BEbatch trong `docs/superpowers/plans/2026-11-02-chau-logic-performance-plan.md`
   và repairvòng1 khi cần. DESIGN.md/UIprocedure cho pagingintegration.

9probe cũ đãpass và fullsuite reviewer838pass/0fail/10skip/848, nhưng **Plan2
chưa nghiệm thu**. Không dùng việc9probe cũ xanh làm bằng chứng RP0–RP6 đãxong.
Review bổ sung4appprobesfail,1MariaDBsimultaneousfail,1DOMorderingrepro.

Thực thi RR0→RR4 và tiếp tục BE0/BE4–BE8 còn thiếu. Mỗi batch regressionred→fix
→green, không đổi expected/suppress/skip đểchebug; updateledger rồi tiếp tục.

- C1: MariaDB2transaction cùngđọcquota,còn2slot nhưng1deadlock khôngrecover.
  Testbarrier thật, locking/atomic+boundedretry/freshstate/idempotentidentity;
  chỉ CreateExecutionStrategy() không tự enable retry. Không globalblindretry.
- C5: ChangeTracker.Clear củaquotaservice detachconversation củachatcaller;
  sendthànhcông mà UpdatedAt khôngsave. Isolate/refreshquotaentry không clearall.
- C2: cancel ởSettle saureplycommit →ledgerReleased/pending0/settled0 dù có
  assistant+successlog. Stateawarefinalize/recovery, không release outcomeđãbiết.
- C3: finalizeexception saucommit→HTTP500; existingconversation JSretry cùng
  payload→2providercalls/2assistantrows. RandomreservationGuid không phảiintentID.
  Không autoretry unknownoutcome; nếuaddidentity giữroutes/contracts/ownership,
  known-result/recovery đúng, không claimremoteexactlyoncebilling.
- C4: Loadolder chèn luôntrướccùngnextSibling, đảo m0,m1,m2,m3 thànhm3,m2,m1,m0.
  TestactualproductionJS+DOM multipage/ties/scroll, không chỉassertbuttonexists.
- C6: UpdateEmail vẫn nhậnnot-an-email,302persist. Workflowvalidation vàreset
  concurrentconsume proof cònthiếu; Registerfix không đóngtoànBE1.

Probe ignored thamkhảo `.tmp/cursor-repair-review/RepairReviewTests.cs` vàTRX/
browser-paging-order.json. Promote regressions chínhthức khôngduplicatecompile;
artifactkhôngcó thì reportđủtrigger đểdựnglại. Giữ9probe cũ, node4 và relational
tests hiệncó; không reset/devDB hoặc lấysuccessCI/thờiđiểmcũ thay newevidence.

Sourcechanges trongscopePlan2; ProgramchỉDI/flagskhôngsecret cầnthiết. Schema
additive phảiEFskill/repo-localEF+Designer/snapshot+MariaDB10.5.23disposable
upgrade/repeat/preservation+pendingmodel. Không editappliedmigration/history,
DROP/reset, nângframework/packages tùytiện, secrets/backup/keytrongGit/log/argv.

Không paid/liveAI hoặc SMTP, không đổireceipt/credit/outbox/GoalAreas/IDOR404/
CSRF/roles. Không Task.Run/fireforget, không parallelEFsamecontext. Preserve
ports/process; runlocal bằnglauncherchuẩn khi cần, keysource không echo key.
Whisper không tựstart từbuild/hooks/folderopen.

Chỉlocal: không commit/push/PR/CIremote/merge/deploy/Plesk, giữgatefalse.
Hếtbatch correctness phải tiếp tục SpeakingWritingSQLpaging/SMTP/Profile/assets/
perf/browser theoplan; khôngdừng ởmộtfocusedfilterxanh. Externalgate chưa có
prerequisite ghiunverified/blockedcóchứngcứ và làmtiếpphầnđộclập, khôngdeploy.

Cuốilượt báo tiếngViệt C1–C6đãđóngbằngchứnggì, từngBE, actualtests pass/fail/
skip, provider/schema/browser/perf before-after, files/risks/blockers. Ghi ledger
đủđểreviewtiếp; không blanketDONE khi batch cònpending.
