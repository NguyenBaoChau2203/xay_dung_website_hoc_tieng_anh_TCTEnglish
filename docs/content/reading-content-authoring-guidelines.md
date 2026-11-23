# TCT English — Reading Content Authoring Guidelines (A1–C2)

Canonical standard for humans and AI authoring Reading passages for TCT English.
Language of this document: **English** (AI/prompts). Learner UI copy stays Vietnamese where required.

Related: Excel import contract → [reading-excel-authoring-design](../superpowers/specs/2026-11-11-reading-excel-authoring-design.md); product rules → [SPEC.md](../../SPEC.md) §C.5.

These three layers stay separate so assessment or Excel changes do not rewrite CEFR language rules.

---

## Layer 1 — CEFR Content Standard

### Word count (Passage body only)

Count **words in Passage paragraphs only**. Do **not** count Title, Questions, Answers, or Vocabulary.

| CEFR | Target | Allowed range | Suggested paragraphs |
|------|--------|---------------|----------------------|
| A1 | 100 | 80–120 | 2–3 |
| A2 | 150 | 120–180 | 2–3 |
| B1 | 250 | 200–300 | 3–4 |
| B2 | 350 | 300–450 | 3–5 |
| C1 | 500 | 450–600 | 4–5 |
| C2 | 650 | 550–750 | 4–6 |

Do not pad text only to hit a number. Prefer natural flow; paragraph lengths may vary.

### Difficulty ≠ length

Do not treat a longer A2 as “harder than A1”. Difficulty is judged together by vocabulary, grammar, text structure, comprehension demand, and question complexity.

### Six control criteria

| Criterion | What to control |
|-----------|-----------------|
| Vocabulary | Frequency, abstractness, academic load |
| Grammar | Clause count, complexity |
| Sentence Length | Information density per sentence |
| Text Structure | Paragraph organisation and cohesion |
| Comprehension | Literal → synthesis → inference |
| Question Complexity | Task demand and distractors |

A C1/C2 text may use familiar words yet remain hard through abstract argument and implication.

### CEFR profiles (authoring direction)

#### A1 — Beginner

- **Goal:** Simple, familiar daily information.
- **Topics:** Family, friends, school, hobbies, food, routines.
- **Vocabulary:** Concrete, high-frequency.
- **Grammar:** Present simple, *be*, short affirmatives/negatives/questions.
- **Sentences:** Short; few clauses.
- **Comprehension:** Names, times, places, activities (literal).
- **Inference:** Almost none.

#### A2 — Elementary

- **Goal:** Familiar topics with simple linked information.
- **Topics:** Travel, shopping, health, work, city life.
- **Vocabulary:** Common words; basic synonyms/phrases.
- **Grammar:** Past simple, future, comparatives, modals, basic connectors.
- **Sentences:** Expanded simple; some compound; limited complex.
- **Comprehension:** Main idea, detail, sequence.
- **Inference:** Simple contextual meaning.

#### B1 — Intermediate

- **Goal:** Familiar social, study, and work texts.
- **Topics:** Education, technology, environment, careers, culture.
- **Vocabulary:** Light academic, collocations, phrasal verbs, paraphrase.
- **Grammar:** Common tenses, passive, conditionals, relative clauses.
- **Sentences:** Mix of simple, compound, complex.
- **Comprehension:** Paragraph main ideas, cause–effect, author purpose (basic).
- **Inference:** Moderate implied meaning.

#### B2 — Upper intermediate

- **Goal:** Fairly complex specialised or argumentative text.
- **Topics:** Economy, science, psychology, technology, social policy.
- **Vocabulary:** Broader academic, idioms, nuance.
- **Grammar:** Multi-clause complex, inversion, reduced clauses, emphasis.
- **Sentences:** Longer; denser logic.
- **Comprehension:** Argument structure, stance, purpose, attitude.
- **Inference:** Information not stated directly.

#### C1 — Advanced

- **Goal:** Long, complex texts; tone and implication.
- **Topics:** Philosophy, academia, culture, public policy, research.
- **Vocabulary:** Specialist and abstract; fine shades of meaning.
- **Grammar:** Flexible multi-layer structures.
- **Sentences:** May be long with supporting detail.
- **Comprehension:** Evaluate argument, tone, attitude, implication.
- **Inference:** Cross-paragraph links.

#### C2 — Proficiency

- **Goal:** Near all text types, including abstract/specialised debate.
- **Topics:** Academic debate, cultural criticism, deep research, social theory.
- **Vocabulary:** Precise, polysemous, rhetorical, technical.
- **Grammar:** Flexible, dense, multi-layered.
- **Sentences:** Tight argumentation; may be concise yet deep.
- **Comprehension:** Analyse, synthesise, evaluate, contrast views.
- **Inference:** Assumptions, limits of argument, what is left unsaid.

### Passage structure (reference, not mandatory count)

1. Introduction — topic / context / problem  
2. Development — explanation / evidence  
3. Analysis — deeper view / alternatives  
4. Conclusion — evaluation / extension  

Use as many paragraphs as the CEFR table suggests; do not force exactly four.

---

## Layer 2 — Reading Assessment Standard

### Question types (every reading)

Each imported reading includes **all five** system types:

| Type | Skill |
|------|--------|
| MultipleChoice | Select the best answer |
| TrueFalse | Verify a statement |
| GapFill | Context / wording |
| Matching | Link pairs (e.g. word–meaning) |
| ShortAnswer | Locate / summarise briefly |

### Question difficulty by CEFR

- **A1–A2:** Prefer literal retrieval from the passage.
- **B1–B2:** Increase paraphrase, main idea, and inference.
- **C1–C2:** Increase argument, attitude, implication, assumption, author stance.

Same topic (technology) — progressive stems:

| CEFR | Example stem |
|------|----------------|
| A1 | What device does Anna use? |
| A2 | Why does Anna use her phone every day? |
| B1 | What is the main advantage of using technology at work? |
| B2 | What can be inferred about the impact of technology on employees? |
| C1 | How does the author qualify the benefits of technological development? |
| C2 | Which underlying assumption does the author's argument appear to challenge? |

### Answer quality rules

1. One determinate correct answer (or clear scoring criterion for ShortAnswer).
2. Evidence must map to Passage (`Evidence_Paragraph_ID` = `ParagraphNo`).
3. Prefer paraphrase over copying full sentences into stems.
4. Distractors plausible but not relying on outside-passage facts.
5. No trick questions.

### Suggested duration (read + answer)

Map to Excel `Duration` / `EstimatedTimeMinutes` using the **midpoint**:

| CEFR | Suggested range | Duration value |
|------|-----------------|----------------|
| A1 | 5–7 min | 6 |
| A2 | 7–10 min | 8 |
| B1 | 10–15 min | 12 |
| B2 | 15–20 min | 18 |
| C1 | 20–25 min | 22 |
| C2 | 25–30 min | 28 |

Actual time also depends on question load and learner speed.

---

## Layer 3 — Content Import Standard

### Excel identity

- Workbook sheets: `Guide`, `Reading_Info`, `Passage`, `Questions`, `Answers`, `Vocabulary`.
- Excel `ReadingCode` = **temporary join key** only (unique in file, e.g. `L1`, `L2`).
- Import **always** assigns `ExternalReadingKey = RD-{CEFR}-NNN` (next free per CEFR).
- Import creates **Draft** only; learners see content after Admin Approve → Publish.
- Optional media ZIP: `{file ReadingCode}.jpg|png|webp|gif` at ZIP root.

### Pre-import checklist

- [ ] CEFR valid (A1–C2); Duration matches midpoint table  
- [ ] Passage word count inside allowed range for that CEFR  
- [ ] Paragraph count within suggested band; natural flow  
- [ ] Five question types present; Question_ID unique in workbook  
- [ ] Evidence_Paragraph_ID matches Passage ParagraphNo  
- [ ] Vocabulary Meaning = Vietnamese when vocab rows exist  
- [ ] Title / topic / questions / vocab stay on one theme  
- [ ] Cover image (if any) is a **real photograph**, not AI-generated  

### Soft vs hard rules

Word-count and duration bands are **authoring standards**. The import parser does **not** hard-fail on word count (unless a future reviewed change adds that). Reviewers still reject out-of-band content.

---

## Editorial rules (all layers)

1. Stay on level — avoid flooding the text with above-level lexis/structures.  
2. Sound natural — not a string of artificial “question bait” sentences.  
3. Stay consistent — title, passage, vocab, and items share one topic.  
4. Stay clear — every item has a determinate answer or scoring rule.  
5. Stay evidenced — answers checkable against the passage (except justified inference).  
6. Paraphrase reasonably in stems.  
7. Raise difficulty via language + thinking, not word count alone.  
8. Fair distractors — no outside-passage traps.

---

## Cover images

Prefer real photographer stock (e.g. Pexels / Unsplash CDN). Store under `/images/reading/`. Do **not** use AI-generated covers or generic icon placeholders when a topic photo is expected.
