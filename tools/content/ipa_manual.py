"""Record IPA that the author wrote by hand because no source entry exists (flagged ipaVerified=false).

Usage: py tools/content/ipa_manual.py <manifest.json> "term|/us/|/uk/" ["term|/us/|/uk/" ...]
At most 3 % of all cards may carry this flag (validate_vocab.py enforces it).
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import read_json, write_json  # noqa: E402


def main() -> None:
    path = Path(sys.argv[1])
    manifest = read_json(path)
    by_term = {c["term"]: c for c in manifest["cards"]}
    for item in sys.argv[2:]:
        term, us, uk = item.split("|")
        card = by_term[term]
        card["ipaUs"], card["ipaUk"] = us, uk
        card["ipaVerified"] = False
        card.pop("ipaSource", None)
        card["ipaNote"] = "written by the author; no source entry was found"
        print("manual IPA:", term.encode("ascii", "replace").decode(), us.encode("ascii", "replace").decode())
    write_json(path, manifest)


if __name__ == "__main__":
    main()
