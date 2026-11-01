"""Minimal loopback stand-in for whisper-server HTTP /inference."""
from __future__ import annotations

import argparse
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


def _body_for_mode(mode: str) -> tuple[int, bytes, str]:
    mode = (mode or "ok").strip().lower()
    if mode == "error":
        return 200, json.dumps({"error": "model not loaded"}).encode("utf-8"), "application/json"
    if mode == "html":
        return 200, b"<html>not json</html>", "text/html"
    if mode == "empty":
        return 200, b"{}", "application/json"
    if mode == "null":
        return 200, b"null", "application/json"
    if mode == "bad-text-type":
        return 200, json.dumps({"text": 123, "segments": []}).encode("utf-8"), "application/json"
    if mode == "segments-empty":
        # Missing explicit text + empty segments => adapter invalid_schema.
        return 200, json.dumps({"segments": []}).encode("utf-8"), "application/json"
    if mode == "segment-primitive":
        return 200, json.dumps({"text": "hello", "segments": [42]}).encode("utf-8"), "application/json"
    if mode == "segment-end-before-start":
        return (
            200,
            json.dumps({"text": "hello", "segments": [{"start": 3, "end": 1, "text": "hello"}]}).encode("utf-8"),
            "application/json",
        )
    if mode == "root-array":
        return 200, json.dumps([{"text": ""}]).encode("utf-8"), "application/json"
    # Default: legitimate silence-style verbose_json (adapter accepts empty text + segments).
    payload = {
        "text": "",
        "segments": [],
        "language": "en",
        "no_speech_prob": 0.99,
    }
    return 200, json.dumps(payload).encode("utf-8"), "application/json"


class Handler(BaseHTTPRequestHandler):
    server_version = "FakeWhisper/1.0"

    def log_message(self, format, *args):  # noqa: A003
        sys.stderr.write("fake-whisper: " + (format % args) + "\n")

    def do_POST(self):  # noqa: N802
        length = int(self.headers.get("Content-Length", "0"))
        if length:
            self.rfile.read(length)
        if self.path.rstrip("/") != "/inference":
            self.send_response(404)
            self.end_headers()
            return
        mode = getattr(self.server, "response_mode", "ok")
        status, body, content_type = _body_for_mode(mode)
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):  # noqa: N802
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"ok")


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8092)
    parser.add_argument("-m", "--model", default="")
    parser.add_argument("-l", "--language", default="en")
    parser.add_argument("-t", "--threads", default="2")
    parser.add_argument("-ng", "--no-gpu", action="store_true")
    parser.add_argument("--mode", default="")
    args, _unknown = parser.parse_known_args()
    mode = args.mode or os.environ.get("TCT_FAKE_WHISPER_MODE", "")
    if not mode and args.model:
        side = Path(args.model).resolve().parent / "tct-fake-mode.txt"
        if side.is_file():
            mode = side.read_text(encoding="utf-8").strip()
    if not mode:
        mode = "ok"
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    server.response_mode = mode  # type: ignore[attr-defined]
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
