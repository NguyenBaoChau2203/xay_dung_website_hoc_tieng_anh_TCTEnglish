"""Catch tampered vendor bytes and stale Razor script paths on every host."""
import hashlib
import json
from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "TCTEnglish/wwwroot/lib/pinned"


class PinnedBrowserAssetTests(unittest.TestCase):
    def test_every_vendor_file_is_manifested_and_matches_upstream_hash(self):
        entries = json.loads((ASSETS / "provenance.json").read_text(encoding="utf-8-sig"))
        names = [entry["Name"] for entry in entries]
        self.assertEqual(len(names), len(set(names)), "duplicate provenance entry")
        actual = {p.name for p in ASSETS.iterdir() if p.is_file() and p.name != "provenance.json"}
        self.assertEqual(actual, set(names), "unmanifested or missing vendor file")
        for entry in entries:
            with self.subTest(name=entry["Name"]):
                self.assertEqual(Path(entry["Name"]).name, entry["Name"])
                self.assertRegex(entry["Source"], r"^https://(?:cdn\.jsdelivr\.net/npm/[^@]+@\d+\.\d+\.\d+/|raw\.githubusercontent\.com/[^/]+/[^/]+/[0-9a-f]{40}/)")
                self.assertEqual(hashlib.sha256((ASSETS / entry["Name"]).read_bytes()).hexdigest(), entry["Sha256"])

    def test_razor_references_resolve_to_manifested_assets(self):
        entries = json.loads((ASSETS / "provenance.json").read_text(encoding="utf-8-sig"))
        names = {entry["Name"] for entry in entries}
        references = []
        for page in (ROOT / "TCTEnglish").rglob("*.cshtml"):
            for name in re.findall(r"~/lib/pinned/([^\"'<>?\s]+)", page.read_text(encoding="utf-8-sig")):
                references.append(name)
                with self.subTest(page=str(page.relative_to(ROOT)), name=name):
                    self.assertIn(name, names)
                    self.assertTrue((ASSETS / name).is_file())
        self.assertTrue(references, "expected application references to vendored assets")


if __name__ == "__main__":
    unittest.main()
