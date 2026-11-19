"""Look up IPA for words in the Free Dictionary API (data from Wiktionary), with a local cache.

Only the pronunciation strings are used (a fact, not creative text); definitions and examples from the
API are never read. Rate limit: 2 requests per second. The cache lives in the ignored
`.appdata/local/content-build/ipa-cache.json`.

Usage:
  py tools/content/ipa_check.py lookup mango goat
  py tools/content/ipa_check.py fill <manifest.json> [--overwrite]
      fills ipaUs / ipaUk / ipaSource / ipaVerified for single-word cards; phrases are skipped.
"""
from __future__ import annotations

import argparse
import re
import sys
import time
import urllib.error
import urllib.parse
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import BUILD, http_json, read_json, write_json  # noqa: E402

CACHE = BUILD / "ipa-cache.json"
API = "https://api.dictionaryapi.dev/api/v2/entries/en/"
IPA = re.compile(r"^/[^/\s][^/]*/$")
BRACKET = re.compile(r"^\[[^\[\]\s][^\[\]]*\]$")


def load_cache() -> dict:
    return read_json(CACHE) if CACHE.exists() else {}


US_TAGS = re.compile(r"\b(ga|us|general american|american)\b", re.I)
UK_TAGS = re.compile(r"\b(rp|uk|received pronunciation|british)\b", re.I)
WIKI = "https://en.wiktionary.org/w/api.php?action=query&prop=revisions&rvprop=content&rvslots=main&format=json&formatversion=2&titles="


def from_dictionaryapi(word: str, result: dict) -> None:
    url = API + urllib.parse.quote(word)
    try:
        data = http_json(url, "dictionaryapi", per_second=2, retries=2)
    except urllib.error.HTTPError as exc:
        if exc.code != 404:
            raise
        return
    for entry in data if isinstance(data, list) else []:
        for ph in entry.get("phonetics") or []:
            text = (ph.get("text") or "").strip()
            audio = (ph.get("audio") or "").lower()
            if not IPA.match(text) or "-au" in audio or "-ca" in audio:
                continue
            if "-us" in audio:
                result["us"] = result["us"] or text
                result["usSource"] = result["usSource"] or url
            elif "-uk" in audio:
                result["uk"] = result["uk"] or text
                result["ukSource"] = result["ukSource"] or url


def from_wiktionary(word: str, result: dict) -> None:
    url = WIKI + urllib.parse.quote(word)
    page_url = "https://en.wiktionary.org/wiki/" + urllib.parse.quote(word)
    data = http_json(url, "wiktionary", per_second=2, retries=2)
    pages = (data.get("query") or {}).get("pages") or []
    if not pages or "missing" in pages[0]:
        return
    text = pages[0]["revisions"][0]["slots"]["main"]["content"]
    m = re.search(r"==English==(.*?)(?=\n==[^=]|\Z)", text, re.S)
    if not m:
        return
    for line in m.group(1).splitlines():
        if "IPA|en" not in line:
            continue
        for tpl in re.findall(r"\{\{IPA\|en\|([^}]*)\}\}", line):
            parts = tpl.split("|")
            ipas = [x.strip() for x in parts if IPA.match(x.strip())]
            if not ipas:  # narrow transcription [..]: keep the symbols, show them as /../
                ipas = ["/" + x.strip()[1:-1] + "/" for x in parts if BRACKET.match(x.strip())]
            if not ipas:
                continue
            tags = " ".join(re.findall(r"\{\{(?:a|accent|qualifier)\|([^}]*)\}\}", line)) + " " + " ".join(x for x in parts if x.startswith("a="))
            us_tag, uk_tag = US_TAGS.search(tags), UK_TAGS.search(tags)
            if us_tag:
                result["us"] = result["us"] or ipas[0]
                result["usSource"] = result["usSource"] or page_url
            if uk_tag:
                result["uk"] = result["uk"] or ipas[0]
                result["ukSource"] = result["ukSource"] or page_url
            if not tags.strip():  # untagged: shared by both accents
                for k in ("us", "uk"):
                    result[k] = result[k] or ipas[0]
                    result[k + "Source"] = result[k + "Source"] or page_url


def lookup(word: str, cache: dict) -> dict:
    key = word.lower().strip()
    if key in cache and "usSource" in cache[key]:
        return cache[key]
    result: dict = {"us": None, "uk": None, "usSource": None, "ukSource": None}
    failed = False
    # Wiktionary first (explicit US/UK tags, tolerant rate limit); the Free Dictionary API fills the gaps.
    for finder, retries_note in ((from_wiktionary, "wiktionary"), (from_dictionaryapi, "dictionaryapi")):
        if result["us"] and result["uk"]:
            break
        try:
            finder(key, result)
        except urllib.error.HTTPError as exc:
            failed = True
            print(f"  {retries_note} HTTP {exc.code} for {key}", file=sys.stderr)
            if exc.code == 429:
                time.sleep(30)
        except Exception as exc:  # noqa: BLE001 - one word must not stop the run
            failed = True
            print(f"  {retries_note} {type(exc).__name__} for {key}", file=sys.stderr)
    if not failed:  # never cache an answer that came from a failed request
        cache[key] = result
        write_json(CACHE, cache)
    return result


def cmd_fill(path: Path, overwrite: bool) -> None:
    manifest = read_json(path)
    cache = load_cache()
    filled = missing = 0
    for card in manifest["cards"]:
        term = card["term"]
        if " " in term.strip():
            continue
        if card.get("ipaVerified") and not overwrite:
            continue
        r = lookup(term, cache)
        if not (r["us"] and r["uk"]):
            missing += 1
            print(f"  incomplete IPA: {term} us={r['us']} uk={r['uk']}")
            if r["us"] or r["uk"]:  # keep the half we have, leave the card unverified
                card["ipaUs"], card["ipaUk"] = r["us"] or r["uk"], r["uk"] or r["us"]
                card["ipaVerified"] = False
            continue
        card["ipaUs"], card["ipaUk"] = r["us"], r["uk"]
        card["ipaSource"] = r["usSource"] if r["usSource"] == r["ukSource"] else f"{r['usSource']} ; {r['ukSource']}"
        card["ipaVerified"] = True
        filled += 1
        write_json(path, manifest)
    write_json(path, manifest)
    print(f"{manifest['set']['code']}: {filled} filled, {missing} without a source")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    a1 = sub.add_parser("lookup")
    a1.add_argument("words", nargs="+")
    a2 = sub.add_parser("fill")
    a2.add_argument("manifest", type=Path)
    a2.add_argument("--overwrite", action="store_true")
    a = ap.parse_args()
    if a.cmd == "lookup":
        cache = load_cache()
        for w in a.words:
            r = lookup(w, cache)
            print(f"{w}: us={r['us']} uk={r['uk']}")
    else:
        cmd_fill(a.manifest, a.overwrite)


if __name__ == "__main__":
    main()
