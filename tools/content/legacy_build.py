"""Build the legacy-image manifests (data/content/legacy-images/*.json) from wwwroot/data/system-vocabulary.json.

D7: the 9 legacy JSON sets point at image files that do not exist (61 paths), have no image (30) or use a remote
shop URL (1). Each card gets a `target` (the existing path, or a new /images/<topic>/<slug>.jpg) and either a photo
query or a `generate` spec (original artwork). The normal pipeline then picks/generates the image, and
legacy_export.py writes JPEG 480x360 files to the target and fills `ImageUrl` for cards that had none.

Usage: py tools/content/legacy_build.py
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import read_json, slug, write_json  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "TCTEnglish" / "wwwroot" / "data" / "system-vocabulary.json"
OUT = ROOT / "data" / "content" / "legacy-images"

PHOTO = {  # term -> search query (photos; Openverse CC0 / public domain)
    "Dog": "dog", "Cat": "cat", "Bird": "bird", "Fish": "fish swimming", "Horse": "horse", "Elephant": "elephant",
    "Lion": "lion", "Tiger": "tiger", "Monkey": "monkey", "Bear": "bear",
    "Rice": "bowl of rice", "Bread": "bread loaf", "Chicken": "roast chicken", "Beef": "beef steak", "Vegetable": "fresh vegetables",
    "Fruit": "fresh fruit", "Water": "glass of water", "Coffee": "cup of coffee", "Tea": "cup of tea", "Milk": "glass of milk", "Juice": "orange juice glass",
    "Airport": "airport terminal", "Hotel": "hotel building", "Beach": "beach", "Mountain": "mountain", "Restaurant": "restaurant interior", "Museum": "museum",
    "School": "school building", "Book": "open book", "Pen": "ballpoint pen", "Desk": "school desk",
    "Artist": "artist painting", "Musician": "musician playing guitar",
}
GENERATE = {  # (set name, term) or term -> generate spec; value defaults to the lower-case term
    "Mother": ("family", "mother"), "Father": ("family", "father"), "Brother": ("family", "brother"), "Sister": ("family", "sister"),
    "Grandmother": ("family", "grandmother"), "Grandfather": ("family", "grandfather"), "Son": ("family", "son"), "Daughter": ("family", "daughter"),
    "Parents": ("family", "parents"), "Family": ("family", "family"),
    "Teacher": ("people", "teacher"), "Student": ("people", "student"),
    "Education": ("edu", "graduation"), "Homework": ("edu", "assignment"), "Exam": ("edu", "quiz"), "Lesson": ("edu", "tutor"),
    "Doctor": ("job", "doctor"), "Engineer": ("job", "engineer"), "Nurse": ("job", "nurse"), "Police officer": ("job", "police officer"),
    "Chef": ("job", "chef"), "Driver": ("job", "driver"), "Farmer": ("job", "farmer"),
    "Red": ("color", "red"), "Blue": ("color", "blue"), "Yellow": ("color", "yellow"), "Green": ("color", "green"), "Black": ("color", "black"),
    "White": ("color", "white"), "Orange": ("color", "orange"), "Purple": ("color", "purple"), "Pink": ("color", "pink"), "Brown": ("color", "brown"),
    "Passport": ("travel", "passport"), "Ticket": ("travel", "ticket"), "Luggage": ("travel", "luggage"), "Tourist": ("travel", "tourist"),
}
PHRASES = {  # greeting-type cards -> conversation scene
    "Hello", "Goodbye", "Good morning", "Good night", "See you later", "Thank you", "Please", "Sorry", "Excuse me", "You're welcome",
    "How are you?", "What's your name?", "Where are you from?", "I don't understand", "Can you repeat?", "Speak slowly", "Nice to meet you", "Have a nice day",
}
FOLDER_OF = {  # topic folder for new files when the card had no path
    "1000 từ tiếng Anh thông dụng": "greetings", "Từ vựng tiếng Anh giao tiếp": "greetings", "3000 từ vựng Oxford A1": "family",
    "3000 từ vựng Oxford A2": "education", "Đồ ăn & Thức uống": "food", "Du lịch & Địa điểm": "travel", "Nghề nghiệp": "jobs", "Màu sắc": "colors",
}


def main() -> None:
    data = read_json(SRC)
    OUT.mkdir(parents=True, exist_ok=True)
    n = 0
    for folder in data:
        for set_ in folder["Sets"]:
            code = "L" + str(n + 1).zfill(2)
            n += 1
            cards = []
            for c in set_["Cards"]:
                term = c["Term"]
                url = c.get("ImageUrl") or ""
                local = url if url.startswith("/images/") else None
                target = local or f"/images/{FOLDER_OF[set_['SetName']]}/{slug(term)}.jpg"
                # Dog in "1000 từ" points at a remote shop image: it gets a local file and a new path.
                card = {"term": term, "target": target, "hadImage": bool(local)}
                if term in PHRASES:
                    card["generate"] = {"kind": "scene", "value": "phrase"}
                    card["imageQuery"] = term
                elif term in GENERATE:
                    kind, value = GENERATE[term]
                    card["generate"] = {"kind": kind, "value": value}
                elif term in PHOTO:
                    card["imageQuery"] = PHOTO[term]
                else:
                    raise SystemExit(f"no image plan for {set_['SetName']} / {term}")
                cards.append(card)
            manifest = {"schemaVersion": 1, "folder": folder["FolderName"],
                        "set": {"code": code, "name": set_["SetName"], "description": set_.get("Description", ""), "topic": set_["SetName"]},
                        "cards": cards}
            write_json(OUT / f"{code.lower()}-{slug(set_['SetName'])}.json", manifest)
            print(code, set_["SetName"], len(cards))


if __name__ == "__main__":
    main()
