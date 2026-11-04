# Tin nhắn nhóm — local Docker + OpenAI speech

Mọi người pull nhánh/feature hiện tại để dùng bộ local Docker + OpenAI Speech-to-Text nhé.

1. Chạy `node scripts/check-local.cjs` và sửa mục MISSING.
2. `SETUP-AGENTS.cmd` một lần nếu chưa, rồi `SETUP-LOCAL.cmd` hoặc
   `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
3. Xác nhận MariaDB, web `/health/ready`, và `scripts/speech-status.ps1`
   (configured ≠ đã verify quota). Không in key.
4. Key: `scripts/set-local-ai-key.ps1` hoặc `OPENAI_API_KEY` theo precedence launcher.
5. Admin audio import: `scripts/local-media.ps1 -Action Setup` (FFmpeg pin), không
   cần cho mic PCM thông thường.

Website dùng GPT-6 Luna low cho văn bản, `gpt-transcribe` cho audio; model trong
Cursor/IDE là lựa chọn riêng. Không còn local Whisper. Live speech có tính phí chỉ
khi owner bật `TCT_OPENAI_SPEECH_LIVE=1`.
