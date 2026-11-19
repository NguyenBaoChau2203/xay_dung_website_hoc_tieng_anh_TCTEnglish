"""Fixture tests for validate_vocab.py: one good manifest and one failing fixture per rule.

Run: py -m unittest discover -s tools/content/tests -v
"""
from __future__ import annotations

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_vocab as v  # noqa: E402


def good_card(term: str = "mango") -> dict:
    return {
        "term": term, "partsOfSpeech": ["noun"], "cefr": "A1", "ipaUs": "/ˈmæŋɡoʊ/", "ipaUk": "/ˈmæŋɡəʊ/",
        "ipaSource": "https://en.wiktionary.org/wiki/mango", "ipaVerified": True,
        "definition": "quả xoài", "meaningEn": "a sweet tropical fruit with yellow flesh",
        "examples": [
            {"text": f"I eat a ripe {term} every summer.", "vi": "Mùa hè nào tôi cũng ăn một quả chín."},
            {"text": f"This {term} is very sweet.", "vi": "Quả này rất ngọt."},
        ],
        "collocations": [{"text": f"fresh {term}", "vi": "tươi"}],
    }


def good_manifest(count: int = 25) -> dict:
    return {
        "schemaVersion": 1, "folder": "Từ vựng theo chủ đề – Cơ bản (A1–A2)",
        "set": {"code": "A03", "name": "Trái cây", "description": "#a1 #fruit", "topic": "Trái cây", "wordListBasis": "NGSL"},
        "cards": [good_card(f"fruit{chr(97 + i)}") for i in range(count)],
    }


def run(manifest: dict, stage: str = "content", require_images: bool = False) -> tuple[list[str], list[str]]:
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "data" / "content" / "vocabulary" / "a" / "a03.json"
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps(manifest, ensure_ascii=False), encoding="utf-8")
        errors: list[str] = []
        warnings: list[str] = []
        v.check_file(path, stage, require_images, errors, warnings, {"terms": {}, "cards": 0, "unverified": 0})
        return errors, warnings


class ValidateVocabTests(unittest.TestCase):
    def assertFails(self, mutate, needle: str, **kw) -> None:
        m = good_manifest()
        mutate(m)
        errors, _ = run(m, **kw)
        self.assertTrue(any(needle in e for e in errors), f"expected an error containing {needle!r}, got {errors}")

    def test_good_manifest_has_no_errors(self):
        errors, _ = run(good_manifest())
        self.assertEqual(errors, [])

    def test_wrong_card_count(self):
        m = good_manifest(24)
        self.assertTrue(any("expected 25" in e for e in run(m)[0]))

    def test_duplicate_term(self):
        self.assertFails(lambda m: m["cards"][1].update(term=m["cards"][0]["term"]), "duplicate term")

    def test_invalid_part_of_speech(self):
        self.assertFails(lambda m: m["cards"][0].update(partsOfSpeech=["noun2"]), "partsOfSpeech invalid")

    def test_level_outside_folder(self):
        self.assertFails(lambda m: m["cards"][0].update(cefr="B2"), "not allowed")

    def test_empty_definition(self):
        self.assertFails(lambda m: m["cards"][0].update(definition=""), "definition is empty")

    def test_definition_too_long(self):
        self.assertFails(lambda m: m["cards"][0].update(definition="một hai ba bốn năm sáu bảy tám chín mười mười một"), "definition has")

    def test_mojibake_is_rejected(self):
        self.assertFails(lambda m: m["cards"][0].update(definition="quáº£ xoÃ i"), "corrupted")

    def test_ipa_without_slashes(self):
        self.assertFails(lambda m: m["cards"][0].update(ipaUs="ˈmæŋɡoʊ"), "IPA between slashes")

    def test_verified_ipa_needs_a_source(self):
        self.assertFails(lambda m: m["cards"][0].update(ipaSource=""), "without an ipaSource")

    def test_too_many_unverified_ipa(self):
        def mutate(m):
            for c in m["cards"][:3]:
                c["ipaVerified"] = False
        m = good_manifest()
        mutate(m)
        errors, warnings = run(m)
        self.assertEqual(errors, [])
        self.assertTrue(any("ipaVerified" in w for w in warnings))

    def test_example_count(self):
        self.assertFails(lambda m: m["cards"][0].update(examples=m["cards"][0]["examples"][:1]), "needs 2-3 examples")

    def test_example_without_translation(self):
        def mutate(m):
            m["cards"][0]["examples"][1]["vi"] = ""
        self.assertFails(mutate, "lacks text or Vietnamese")

    def test_first_example_must_contain_the_term(self):
        def mutate(m):
            m["cards"][0]["examples"][0]["text"] = "I like to eat in the evening."
        self.assertFails(mutate, "does not contain the term")

    def test_example_too_long_for_level_a(self):
        def mutate(m):
            m["cards"][0]["examples"][0]["text"] = "I eat a ripe fruita every summer when the weather is hot and sunny outside."
        self.assertFails(mutate, "words (max 12)")

    def test_meaning_en_too_long(self):
        self.assertFails(lambda m: m["cards"][0].update(meaningEn=" ".join(["word"] * 16)), "meaningEn")

    def test_image_required_when_asked(self):
        self.assertFails(lambda m: None, "image missing", require_images=True)

    def test_image_license_allowlist(self):
        def mutate(m):
            for c in m["cards"]:
                c["image"] = {"file": "a03/x.webp", "provider": "flickr", "pageUrl": "https://example.org/x", "license": "CC BY 2.0",
                              "query": "x", "sha256": "0" * 64, "reviewedAt": "2026-11-19", "width": 480, "height": 360}
        self.assertFails(mutate, "not on the allowlist", require_images=True)

    def test_image_hotlink_is_rejected(self):
        def mutate(m):
            for c in m["cards"]:
                c["image"] = {"file": "https://cdn.example.org/x.webp", "provider": "openverse", "pageUrl": "https://example.org/x", "license": "CC0",
                              "query": "x", "sha256": "0" * 64, "reviewedAt": "2026-11-19", "width": 480, "height": 360}
        self.assertFails(mutate, "never a URL", require_images=True)

    def test_words_stage_ignores_content_fields(self):
        m = good_manifest()
        for c in m["cards"]:
            for k in ("ipaUs", "ipaUk", "meaningEn", "examples", "collocations"):
                c.pop(k, None)
        self.assertEqual(run(m, stage="words")[0], [])

    def test_phrase_folder_requires_phrase_pos(self):
        m = copy.deepcopy(good_manifest(20))
        m["set"]["code"] = "C03"
        errors, _ = run(m)
        self.assertTrue(any("phrases must use" in e for e in errors))


if __name__ == "__main__":
    unittest.main()
