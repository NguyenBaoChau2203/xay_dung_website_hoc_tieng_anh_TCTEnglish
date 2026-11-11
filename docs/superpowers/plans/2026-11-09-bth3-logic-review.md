# BTH3 class report logic review and repair plan

## Review outcome and authority

The initial owner-requested logic review found five actionable issues and was REVIEW_ONLY. The owner subsequently activated the document correction on 2026-11-09; the separate corrected DOCX and its verification are now complete, as recorded below. No application, schema, configuration, Git, CI or release change was authorized or performed.

Reviewed artifact: `C:/Users/chau1/Downloads/Nhom03_BTH3_T105_SoDoLop.docx`, 99 physical pages. All page numbers below are printed page numbers; physical PDF pages are one greater because the cover is unnumbered. SHA256: `5eaa0e5955ba39f3497248cc6fb433ba0fea7392e283108b7984df5dd9ff839f`.

References: the unchanged submitted BTH1 DOCX, `.tmp/bth3/original-usecases.json`, the actual Word XML and rendered PDF, `.tmp/bth3/content.py`, `.tmp/bth3/geometry.json`, current model declarations and focused current service/controller evidence obtained through CodeGraph, with bounded reads for omitted source. The repository branch observed during review was `feature/chau-speaking-clay-ui`; the owner may be working concurrently, so inspect the actual checkout before later work.

## Findings

### F1 P2 Draw the six persistent relations currently specified only in tables

The report specifies 82 persistent relationships. Only 76 distinct declared persistent relationships occur in its drawing geometry. Six have no graphical representation anywhere:

| Class A | Class B | Multiplicity near A / near B | Suggested affected view |
| --- | --- | --- | --- |
| Folder | Folder | 0..1 / 0..* | Figure 5 or 6, printed 17–18 |
| SpeakingVideo | UserSpeakingVideoCompletion | 1 / 0..* | Figure 11, printed 32 |
| User | WritingAcceptedAnswer | 0..1 / 0..* | Figure 15 or 33, printed 45 or 88 |
| WritingExercise | UserWritingAttempt | 1 / 0..* | Figure 16, printed 46 |
| WritingExercise | UserWritingSentenceProgress | 1 / 0..* | Figure 16, printed 46 |
| WritingExercise | UserWritingExerciseProgress | 1 / 0..* | Figure 16, printed 46 |

The Folder self association is intentionally described in a note, but that note does not draw the relationship. The remaining omissions make the ownership of per-video completion and per-exercise progress hard to establish from the diagrams. This is incomplete diagram coverage, not an inverted-cardinality finding. The declared multiplicities all agree with the optionality of their corresponding source keys.

Cause: `.tmp/bth3/content.py:375` excludes self associations and chooses node sets that never put the other missing pairs together. The old `verify.py` checked the REL inventory count, not whether every relation was drawn. Minimal repair: add the needed context classes to appropriate views, provide a real self association with distinct ports and role labels, and validate the union of actual drawn persistent relations against REL. Split a view if required for legibility; update figure counts/captions if the number changes.

### F2 P2 Supply the selected exercise identity in the writing method contract

Figure 15 on printed 45 and B1-11-01 on printed 52 declare `chonBaiViet(trinhDo: String, chuDe: String): KetQua`. The same trace row lists an exercise ID as input and promises one exercise with its sentences as output. Level and topic can match multiple exercises and therefore do not identify the chosen one. The class has no documented selected-exercise state that supplies the missing identity.

Minimal repair: add `baiId: Integer` to the selection/opening operation, or distinguish list filtering from opening a selected exercise while keeping the same original BTH1 coverage. Align the diagram signature, table signature, inputs and output wording. Current source confirms that writing evaluation and practice resolve a specific exercise ID; see `TCTEnglish/Services/WritingService.cs:156` and the `ResolveWritingExerciseAccessAsync(exerciseId, userId)` call. The issue is an internally inconsistent analysis contract, not a claim that the C# method must have the same Vietnamese name.

### F3 P2 Complete the member-blocking transition and its role checks

B1-13-08 on printed 62 describes `chanThanhVien` only as checking role limits and blocking within the class, with a blacklist row as output. It names ClassBlacklist and ClassMember but omits the affected ClassJoinRequest state. The postcondition does not explain whether an already joined person still has membership or whether a Pending request can later be approved.

Current source is explicit: `TCTEnglish/Services/ClassService.cs:796` permits the class owner or Admin, prevents self/owner blocking, removes an existing ClassMember, marks Pending requests Declined, and adds ClassBlacklist. Approval also rechecks the blacklist at line 921. Document those effects and add ClassJoinRequest to the relevant method data/dependency view. Keep kicking distinct: kicking removes membership and does not by itself create a blacklist entry.

Also spell out the ordinary owner/assistant/member target rules for mute and kick. Avoid presenting a generic phrase such as "quyền quản lý" as a complete permission definition. The existing report warns that stored chat configuration does not prove runtime enforcement. Current ClassChatHub.SendMessage and SendImageMessage check class access but do not check IsMuted/IsChatLocked; this is an implementation caveat, not authority to fix the app during document review. A later document correction must distinguish the intended design rule from observed implementation behavior.

### F4 P2 Identify producers for activity, streak, badges and background notifications

Figures 22–24 on printed 63–65 introduce activity, badge and notification processing data, but MucTieu exposes only viewing goals, editing goals and viewing streak/badges. Figure 23 is titled "Các lớp xét và trao huy hiệu" yet no operation explicitly evaluates and awards a badge. Viewing awarded badges is a different responsibility from granting one after a learning event.

Across all 166 trace operations, four classes have no explicit operation data reference: UserFeatureTimeLog, LearningCompletionCredit, LearningNotificationOutbox and NotificationJobRun. Their presence is not itself wrong; background/internal entities do not need their own user-facing use cases. The model nevertheless needs to explain their processing responsibilities and the connection to completed learning.

Current source: `TCTEnglish/Services/GoalsService.cs:297` records learning activity, calls UpdateStreakAndRewardsAsync and RefreshUserBadgesAsync, then coordinates goal notifications. `TCTEnglish/Services/IGoalsService.cs:12`–19 declares activity/streak operations and per-skill completion updates. Writing evaluation calls the completion-credit service before awarding completion activity; listening completion invokes the goals service. Minimal repair: add explicitly labeled internal methods/collaborations for recording activity, applying a completion credit, updating streak, evaluating/awarding badges and processing automatic notification events. Reuse existing analysis classes where coherent; explain background scheduling rather than inventing new user use cases or persisted entities.

### F5 P2 Replace the shared placeholder specification for 40 authoring operations

All 40 BienSoanHocLieu operations in section 19 (printed 88–92) use the identical processing sentence and identical generic input/output labels. For example publishing and hiding a reading passage both say only to check validity and save changes. This establishes name coverage but does not specify the different state transitions or required parent identifiers. It does not meet the requested detail level.

142 of 166 operation signatures use the default `duLieu: BanDo`. A structured map is a valid analysis type; its use alone is not an error. The defect is that many rows do not define its specific required fields, constraints or result. Minimal repair: distinguish each action's inputs, target/parent IDs, authority, changes and failure conditions. Examples: publishing sets IsPublished=true; hiding sets it=false; adding a writing sentence identifies WritingExerciseId and enforces unique SortOrder within that exercise; editing a speaking sentence validates its video and StartTime/EndTime; deleting content explains dependent data handling. Preserve every original use-case ID and do not replace distinct operations with one generic CRUD paragraph.

## Verified strengths and limits

- Actual DOCX trace rows cover all 123 original detailed BTH1 use cases, with 166 distinct operation rows. This proves inventory coverage, not complete behavior specification.
- All 56 current model classes and 516 scalar attribute names/types match the report model dictionaries; the prior final gate compared those dictionaries against the actual individual Word attribute tables.
- All 82 declared parent-end multiplicities match the nullability of the corresponding source key. No inverted mandatory/optional parent relationship was found by that check. Domain restrictions still require prose constraints.
- ClassMember.Role correctly models per-class Owner/Assistant/Member; User.Role is separate. Shared learning content and per-user results are separated. Per-recipient Notification read state and payment callback deduplication are described coherently.
- Native editable Word diagrams, automatic numbering and existing rendering checks remain valid for the unchanged artifact. Figures 11 and 16 were visually rechecked while verifying missing context relations.
- This was document/source inspection, not browser acceptance or application execution. No claim is made that every current runtime path implements every proposed analysis rule.

Machine-readable evidence: `.tmp/bth3/logic-audit-evidence.json`. Check command: bundled Python runtime with `-X utf8 .tmp/bth3/check_logic_evidence.py`. The assertions completed successfully and reported the six diagram omissions and other content gaps; this is not an overall logic PASS.

## Repair order after owner authorization

Repair activated on 2026-11-09 by the owner's follow-up asking whether the fixes had been made. Continue the intended correction locally and deliver a separate corrected DOCX. The earlier review evidence and reviewed original remain immutable. Application fixes and Git/release actions remain outside scope. Working authoring files are isolated under `.tmp/bth3-fix`.

- [x] Preserve the reviewed DOCX and source references; write a distinct corrected DOCX in Downloads.
- [x] Correct F2's contract and F3's explicit permissions/postconditions before drawing changes.
- [x] Specify F4's internal activity/reward/notification responsibilities without adding invented user-facing use cases or data models.
- [x] Rewrite all 40 authoring specifications in F5 with action-specific inputs, constraints and outcomes. Audit other generic map contracts for the same issue.
- [x] Draw every relationship from F1 and the collaborators required by corrected responsibilities; split crowded views when needed.
- [x] Align all repeated class views, signatures, tables, counts and scope statements. Count user use cases separately from internal operations.
- [x] Strengthen the semantic acceptance checks: every persistent relation drawn at least once; selected exercise identity supplied; blocking affects membership and pending requests; internal producers identified; no repeated generic authoring placeholder.
- [x] Refresh Word fields and pagination using an isolated hidden Word instance; export, run the bundled renderer, inspect all changed pages and reuse only pixel-identical prior page reviews.
- [x] Validate native editability, logo-only raster media, all original use-case IDs, individual class attributes and readable diagram endpoints/cardinalities. Report exact results in Vietnamese.

No application fix, migration, data synchronization, paid API request, commit, push, PR, remote CI or deployment is part of this repair plan.

## Prepared execution prompt

See `.ai/prompts/bth3-class-report-logic-repair.md`. The owner activated this document-only scope on 2026-11-09 and the correction is complete.

Short launch message, for owner use only:

> Correct the BTH3 Word report following `.ai/prompts/bth3-class-report-logic-repair.md`. Keep the reviewed original, create a separate corrected DOCX, complete all five findings and validate the editable diagrams and final pages. Do not change the application.

## Completed correction and acceptance evidence

Output: `C:/Users/chau1/Downloads/Nhom03_BTH3_T105_SoDoLop_DaSuaLogic.docx`; 1,093,985 bytes; SHA256 `c0c2a80fa018157eb12c8cba65cee86f5a6d4b6ce93fb7b10bb0a681d007d608`. The reviewed original's SHA256 remains `5eaa0e5955ba39f3497248cc6fb433ba0fea7392e283108b7984df5dd9ff839f`; its open user-owned Word session was preserved.

All five findings are corrected. The final report has 110 physical pages, 40 native editable Word canvases, 56 entities, 516 typed scalar attributes, 19 analysis controls and 6 service interfaces. All 123 original detailed BTH1 use cases have trace rows covering 166 use-case operations; 8 additional internal operations are explicitly distinguished. All 82 persistent relationships are drawn. All 40 authoring operations have distinct inputs, outputs and effects.

The isolated hidden Word COM session refreshed fields, table of contents, figure list and pagination, and independently tested text editing on every one of the 40 canvases before restoring the original text. The school logo is the only raster media. Geometry checks found no node-interior crossings, clipped text or overlapping relationship labels.

`verify_corrected.py` passed against the actual final DOCX. The bundled canonical renderer generated 110 page images from the Word-exported PDF. Final visual QA inspected 15 changed pages and carried forward 95 exact body-pixel matches to previously completed visual reviews; the numbered footers on all 109 non-cover pages were independently checked. All 110 final pages are accounted for. Evidence: `.tmp/bth3-fix/verification.json`, `.tmp/bth3-fix/visual-review-v2.json` and `.tmp/bth3-fix/review-ledger.json`.

These checks cover the report and its consistency with inspected source; they do not claim application runtime or browser acceptance. No application code/configuration/schema/data, remote CI or deployment was changed.
