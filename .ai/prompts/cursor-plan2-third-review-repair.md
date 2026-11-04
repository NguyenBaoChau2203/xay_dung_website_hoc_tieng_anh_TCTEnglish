# Cursor Auto — sửa review vòng3 và hoàn tất Plan2

Owner giao thực thi trên nhánh`feature/chau-ui-performance-audit`, một agent
inline. Giữ UIPlan1/BTH2/dirtywork/data/workload; không delegation/CLI AI/chat khác.
Website OpenAI`gpt-6-luna`/low, audioWhisper; không đổi theo model IDE.

Đọc AGENTS, neededskills và CodeGraph cho indexedsource; activecontext:

1. `docs/feature-audits/2026-11-02-cursor-plan2-third-review.md`.
2. `docs/superpowers/plans/2026-11-02-cursor-plan2-third-review-repair.md`.
3. Ledger `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
4. ActiveBEbatch trong `docs/superpowers/plans/2026-11-02-chau-logic-performance-plan.md`.

Reviewvòng3: fullfresh843pass/0fail/11skip/854; old9+4probes vàconcurrentreset
pass, Node5pass. **Plan2 chưa nghiệmthu**: diagnostic3casesfail vàChrome2page
historyorderwrong. Các lỗi mới:

- D1P1: cancel sau knownproviderreply trướcsaveDB. QuotaRelease dùng shared
  DbContext.SaveChanges flushassistant/successlogAdded của caller; cleanup sauđó
  xóauserprompt. Existingcaseuser0/assistant1/success1/ledgerReleased0; draftcase
  cũng release known20tokens. Táchwrites/unitofwork, lifecyclekhôngchỉ dựa vào
  replyCommitted. Recovery cho pending phải cóimplementation/proof, không chỉlog.
- D2P2: Speaking Take24public/my khôngcópagination/continuation; topic ởvideo25
  vẫnlisted mà0card, private25 chỉtrả24. Publicquery cònToListfull rồiTakeinmemory.
  Khôi phụccorrectness rồi trueSQLfilters/paging/count/loadmore theoBE4; không
  tăngcap haygiấutopic. Preserveownership/lockedroles/resume/credit.
- D3P2: Fragment đặttrướcLoadoldercontrol làmcontroltrôi xuống saupage1. Page2
  cũhơnlại chèn saupage1. Expectedm0..m5, actualm2,m3,m0,m1,m4,m5. Giữanchorđầu
  vàtestproductionDOM2–3pages, không chỉsinglepagehasMorefalse.

Thực thi T0→T1→T2→T3 rồi BE0/BE4–BE8 cònthiếu. Mỗi batch regressionred→fix→
green, updateledger vàtiếp tục; khôngdừng chỉvì focusedtests cũxanh. Artifact
ignored `.tmp/cursor-third-review/ThirdReviewTests.cs` vàTRX/DOMJSON làreference;
promote tests đúngsemantics, khôngcompile2bản, không sửaexpectedđểchebug.

DockerLinuxAPIpipe hiệnreviewer khôngkết nối được nênMariaDBC1 chưa rerun.
Kiểmactualenvironment/disposableidentity rồi chạyproviderproof; nếuchưa có
prerequisite ghiunverified, không dùngSQLitethaythế và vẫn làm phầnđộclập.

Giữ scopePlan2: ProgramchỉDI/flagskhôngsecret cầnthiết; schemanếu cần chỉadditive
theoEFskill/repo-localEF+Designer/snapshot+MariaDBupgrade/repeat/preservation+
pendingmodel. Không editappliedmigration/history, DROP/resetdevDB/hosting hoặc
nângframework/packages tùytiện. Không credentials/backup/key vàoGit/log/argv.

Fakeprovider/SMTP, không paid/liveAPI, noTask.Run/fireforget/parallelEFsamecontext.
Giữroutes/contracts/IDOR404/CSRF/roles/receipts/credits/outbox/5GoalAreas.
Runlocal khi cần bằnglauncherchuẩn, preservebusyport, không echo key. Build/hook
không tựstartWhisper.

Chỉlocal: không commit/push/PR/CIremote/merge/deploy/Plesk, gatefalse. Cuốilượt
báo tiếngViệt từngfinding/batch/evidence/passfailsskip/provider/browser/perf/
risks/blockers. Không gọiBE2/BE4haytoànPlan2DONE khi cònacceptance chưađạt.
