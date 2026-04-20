from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_FILES = [
    "TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs",
    "TCTEnglish/Views/Ai/_ChatShell.cshtml",
    "TCTEnglish/wwwroot/data/ai/website-guides.json",
    "TCTEnglish/wwwroot/js/ai-chat.js",
    ".ai/context/bug-fix-log.md",
]

MOJIBAKE_MARKERS = [
    "Ã",
    "Ä",
    "â€™",
    "â€œ",
    "â€",
]


def strip_markdown_code(text: str) -> str:
    def strip_inline_code(line: str) -> str:
        result: list[str] = []
        in_code = False
        for char in line:
            if char == "`":
                in_code = not in_code
                continue
            if not in_code:
                result.append(char)
        return "".join(result)

    cleaned_lines: list[str] = []
    in_fence = False
    for line in text.splitlines(keepends=True):
        if line.lstrip().startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        cleaned_lines.append(strip_inline_code(line))

    return "".join(cleaned_lines)


def resolve_paths(args: list[str]) -> list[Path]:
    if args:
        paths = [Path(arg) for arg in args]
    else:
        paths = [Path(path) for path in DEFAULT_FILES]
    resolved = []
    for path in paths:
        resolved.append(path if path.is_absolute() else ROOT / path)
    return resolved


def check_file(path: Path) -> list[str]:
    issues: list[str] = []
    if not path.exists():
        return ["missing file"]

    try:
        data = path.read_bytes()
    except OSError as ex:
        return [f"read failed: {ex}"]

    try:
        text = data.decode("utf-8", errors="strict")
    except UnicodeDecodeError as ex:
        return [f"invalid utf-8 bytes: {ex}"]

    text_to_scan = strip_markdown_code(text) if path.suffix.lower() == ".md" else text

    if "\ufffd" in text_to_scan:
        issues.append("contains replacement character (\ufffd)")

    for marker in MOJIBAKE_MARKERS:
        if marker in text_to_scan:
            issues.append(f"contains mojibake marker '{marker}'")

    if "Â\u00a0" in text_to_scan or "Â " in text_to_scan:
        issues.append("contains mojibake marker 'Â ' or 'Â\u00a0'")

    return issues


def main() -> int:
    paths = resolve_paths(sys.argv[1:])
    failed = False

    for path in paths:
        issues = check_file(path)
        if issues:
            failed = True
            rel_path = path.resolve().relative_to(ROOT) if path.is_relative_to(ROOT) else path
            print(f"[FAIL] {rel_path}")
            for issue in issues:
                print(f"  - {issue}")
        else:
            rel_path = path.resolve().relative_to(ROOT) if path.is_relative_to(ROOT) else path
            print(f"[OK] {rel_path}")

    if failed:
        print("Encoding guard failed.")
        return 1

    print("Encoding guard passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
