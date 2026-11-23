# BrowseClasses join-by-code — ledger

**Date:** 2026-11-23

## Done

- [x] Nút **Tham gia bằng mã** trên `/Home/BrowseClasses` action bar
- [x] Modal nhập mã + mật khẩu; `POST /Home/JoinClassByCode` (reuse Class page pipeline)
- [x] Hint copy: Public/Private join bằng mã ngay trên Khám phá lớp

## Verify

- Local `:5100` student session: page HTML chứa `openJoinByCodeBtn` + `joinByCodeModal`
- Invalid join code → BadRequest (endpoint reachable with antiforgery)
- No server/API/schema change

## Limits

- Full browser UAT với mã Public/Private thật + needsPassword/pending: owner có thể thử trên UI
- Không push/PR/deploy
