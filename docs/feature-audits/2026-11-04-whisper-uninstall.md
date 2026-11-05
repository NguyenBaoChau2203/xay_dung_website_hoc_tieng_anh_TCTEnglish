# Gỡ Whisper trên máy — 04/11/2026

Owner yêu cầu xóa các thành phần Whisper đã cài. Một agent, local-only; giữ web,
key, DB, runtime uploads, dirty work và workloads khác. Không commit/push/remote
CI/deploy, không paid API, không reset/prune Docker volumes hoặc images chung.

## Đã gỡ

| Thành phần | Kết quả |
| --- | --- |
| `.appdata/local/retired-speech-2026-11-04/artifacts` | Absent,43 files/109780875bytes (~104.7MiB) đã xóa |
| Model `ggml-tiny.en.bin` |77,704,715bytes, thuộc43 files đã xóa |
| `whisper-bin-x64.zip`, whisper EXE/DLL và native dependencies riêng trong archive | Thuộc43 files đã xóa, không còn |
| Docker `ghcr.io/ggerganov/whisper.cpp:main` | Removed/untagged, IDsha256:13d0e7c873c59a262dca621b57ad28de40e9927ac883d936dfd8459142c90db4 |

Docker image có logical size1,668,444,381bytes (~1.55GiB). Đã kiểm image ID/tag
và0 containers (kể cả stopped) dùng image trước khi docker image rm không-force.
Sau xóa không còn image Whisper trong inventory. Không suy logical image size
là số bytes ổ Windows giải phóng; layers/storage của Docker được quản lý riêng.

Tool Remove-Item artifacts bị automatic policy review từ chối trước khi thực thi:
chỉ thông báo “blocked by policy”, không có lý do chi tiết. Không đổi tool/shell
hoặc xóa vòng qua parent để bypass. Sau khi được hướng dẫn đúng literal path,
owner trực tiếp chạy lệnh và xác nhận **“Đã xóa thư mục”**. Tool xác minh thư mục
artifacts không còn. Ghi rõ phần owner xóa, không nhận đó là tool đã xóa thành công.

## Những gì còn được giữ

-6file audio/log lịch sử992647bytes: đủ6 SHA256 trước/sau khớp, không dùng làm
  runtime, không xóa recording/history để làm tìm kiếm keyword bằng0.
-2file state.json/speech.lock cũ1426bytes và README trong ignored archive. Tool
  xóa metadata riêng cũng bị policy chặn; chúng còn nguyên, không có engine đọc
  hoặc process dùng. Không cố truncate/overwrite hoặc đổi phương pháp để bypass.
-FFmpeg tại `.appdata/local/media` phục vụ Admin/legacy audio import OpenAI;
  Docker/Python/.NET/Visual C++ dùng chung không bị gỡ vì không phải Whisper riêng.
-Source history/provenance `whisper-audio`, old audits và guard cấm startup được
  giữ; đó không phải một cài đặt Whisper có thể chạy.

## Inventory và verification

-0process Windows có tên Whisper,0Whisper command trên PATH.
-0Whisper Scheduled Task/Windows Startup entry; không có env name Whisper/TCT_ASR
  ở Process/User/Machine. Không in env credential values.
-0Whisper Docker container trước gỡ,0Whisper image sau gỡ.
-Không thấy Whisper app trong HKCU/HKLM uninstall registry; không thấy installer
  Whisper/ggml trong Downloads hoặc folder Whisper/HuggingFace ở các root đã kiểm.
-Không thấy package paths Whisper/openai-whisper/faster-whisper/whisperx trong
  owner Python local, uv-managed Python/cache và pip cache đã kiểm; các default
  Whisper/HuggingFace model-cache paths không có. Không tuyên bố quét mọi venv
  trong mọi dự án khác hoặc xóa global shared dependencies.
-Artifact delete target resolved chính xác trên D, đã kiểm ancestry và descendants
  không reparse point, inventory43 chỉ .bin/.dll/.exe/.zip, không process tham chiếu.
-Key-store length/mtime/protection attributes không đổi; Speech configured từ
  User Secrets AI:ApiKey, modelgpt-transcribe, VerifiedLive=false.
-Web vẫn đang chạy: GET http://localhost:5100/health/ready trả200/ready.
-Không đổi application/runtime source, không cần chạy lại full build/test cho
  việc gỡ file/image unused; xác minh filesystem/runtime là bằng chứng phù hợp.

Artifacts kiểm chứng ở ignored `.tmp/whisper-uninstall/`; không chứa key hoặc
hash key. Cài đặt engine/model/image trong phạm vi xác định đã được gỡ; hai
metadata nhỏ còn lại được ghi rõ, không gọi đó là đã xóa mọi dấu vết lịch sử.
