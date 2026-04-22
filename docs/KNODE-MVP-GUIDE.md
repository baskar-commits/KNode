# Knode: Grounded answers across your personal knowledge

**Canonical MVP guide.**

Picture the highlights from books you have read on **Kindle**, the travel scraps your family keeps in **OneNote**, a photo of a menu you loved, or a voice memo after a hard day. **Knode** is built around a simple idea: ask in normal language, and get answers that **pull from your own saves** and **point back to them**, so themes connect across reading, notes, and plans instead of sitting in separate apps.

The **current version** proves that on **Windows** you can ask over a local **Kindle corpus** (`corpus.jsonl`) and optional **OneNote selected sections**, then index and **Ask** with sources using **Google Gemini**. **Down the road**, the same pattern extends to more of **your** repositories and formats (documents, **images**, **audio**, **video**, and transcripts), still grounded in what you chose to keep.

![Knode Ask UI: question, coaching, answer, and collapsible sources](images/knode-ask-ui.png)

---

## 1. Product: intent, personas, scenarios, requirements, success

### Elevator pitch

**Knode** is a **Windows desktop** assistant that answers **natural-language questions** about your own sources: **Kindle highlights/notes** in local **`corpus.jsonl`** and optional **OneNote sections** you choose in Setup. Answers are **retrieval-grounded**: the model sees **your** passages first, then synthesizes with **citations** (book/section, location/page, quote). The wedge is **personal, citation-backed recall**, not generic web chat.

**Repository policy:** The **GitHub repo ships source and docs only**: **no `corpus.jsonl`**, no sample highlights. **`**/corpus.jsonl`** is gitignored at repo root. After clone or install, every user provides their own corpus and optional connector configuration.

### Problem

Highlights in [Kindle Notebook](https://read.amazon.com/notebook) are organized **by book**, but **themes span books and notes**. Finding "what did I read about confidence?" across Kindle and OneNote is slow; keyword search misses concepts. Users want **their** material connected by topic without fabricated quotes.

### Primary persona (MVP)

**Alex, the returning reader**

- Reads on Kindle, keeps additional context in OneNote, and revisits both before decisions.
- Wants: "What have I read or noted about *X*?" before journaling, prep, or decisions.
- Expects citations tied to source context: book/location for Kindle and section/page for OneNote.
- Accepts a practical ingestion path for Kindle today (capture/export to structured file), while OneNote can be synced from selected sections in Setup.

### Scenarios (as shipped + near term)

| # | Scenario | Today (MVP) | Intent (later) |
|---|----------|-------------|----------------|
| 1 | **Get sources into the app** | Kindle: produce **`corpus.jsonl`** via capture + `parse_dump`. OneNote: connect account, pick sections, sync pages during Build index. | Less manual Kindle ingestion and broader connector coverage. |
| 2 | **Topic query** | User asks in Knode; retrieve top passages from local index; Gemini `generateContent` with citations. | Richer cross-source synthesis and stronger thematic grouping. |
| 3 | **Connect Kindle and notes** | Retrieval can return Kindle and OneNote passages in one answer flow with citations. | Better UI affordances for browsing links between passages. |
| 4 | **Refresh data** | Kindle: rerun capture + parse when highlights change. OneNote: re-sync selected sections on Build index. | More incremental refresh and lower-latency updates. |

### Functional requirements (MVP: implemented vs gap)

| ID | Requirement | Status |
|----|-------------|--------|
| F1 | **Structured corpus** (`corpus.jsonl`, one highlight per line) | **Done** (`parse_dump`). |
| F2 | **Semantic search** over the user’s corpus | **Done** (Knode: Gemini embeddings + cosine; Python path: Chroma + MiniLM). |
| F2b | **Optional OneNote source** with section selection | **Done** (Graph sync in Setup; page-level records merged into local index). |
| F3 | **Citation-first** answers | **Done** (prompt + sources panel). |
| F4 | **Local-first** data; clear **privacy** story for corpus/connectors | **Done** (local index + OneNote local artifacts); see security doc for API traffic and local files. |
| F5 | **Amazon "connector"** via documented Notebook API | **Not available** publicly; MVP relies on user-mediated Kindle export/capture to JSONL. |
| F6 | **Installer / desktop** distribution | **Done** (Inno Setup + `build-installer.ps1`). |

### Non-goals (unchanged)

- Live Microsoft / Google / WhatsApp connectors in v1.
- Social, shared, or public corpora.
- Agents that act outside **read → reason → answer** (no calendar/email actions).

### Success criteria (MVP)

| Metric | Target |
|--------|--------|
| **Grounding** | For ~25 [golden questions](../eval/GOLDEN-QUESTIONS.md), ≥90% of factual claims supported by retrieved clips (human review). |
| **Citation usefulness** | Book + location + excerpt visible for non-obvious claims. |
| **Activation** | Median time from **having `corpus.jsonl`** to **first good answer** \< 15 minutes (install + index + ask). |

---

## 2. Design: options, choice, reasoning (succinct)

### What we wanted (official)

Programmatic, documented access to the same user-owned material from Kindle and OneNote, with clear auth and predictable sync boundaries.

### What we verified

- No published **Kindle Notebook API** for third-party highlight export.
- [Login with Amazon](https://developer.amazon.com/docs/login-with-amazon/documentation-overview.html) is identity, not Notebook payload access.
- [Amazon Data Portability](https://developer.amazon.com/docs/amazon-data-portability/overview.html) Kindle-related scopes are not "export all Notebook highlight text" as of our review.
- OneNote is feasible via Microsoft Graph with delegated scope **`Notes.Read`**, account sign-in through MSAL, and user-selected section sync.

**Conclusion:** MVP uses two ingestion designs: Kindle through user-mediated export/capture to JSONL, and OneNote through Graph sync of selected sections.

### Options considered (abbreviated)

| Source | Option | Decision |
|--------|--------|----------|
| Kindle | Browser/WebView/session capture + parse to JSONL | Chosen for MVP because no public Notebook API exists. |
| Kindle | Fully documented API connector | Not available now. |
| OneNote | MSAL auth + Graph Notes.Read + section picker | Chosen and shipped in Setup flow. |
| OneNote | Pull everything by default | Rejected; user must pick sections to keep control and scope. |

### Chosen path (MVP)

1. **Kindle path:** capture/export + dedupe + `parse_dump` to produce `corpus.jsonl`.
2. **OneNote path:** sign in with MSAL, choose sections, sync page text via Graph during Build index.
3. **Unified index:** merge both source rows into the same local vector index and answer through one Ask flow.

**Reasoning:** this keeps user control explicit, keeps Ask local-first, and avoids live connector calls per question.

---

## 3. Architecture: layers, data flow, technologies

**Deep dive (tiers, components, vector stores, Ask sequence):** [`KNODE-ARCHITECTURE.md`](KNODE-ARCHITECTURE.md)

### Layered view (static summary)

| Layer | Responsibility | Technologies |
|-------|----------------|-------------|
| **Presentation** | Setup corpus path, connect OneNote, pick sections, build index, ask, show answer + sources | WPF, .NET 8 |
| **Application** | Build/sync orchestration, embed batching, Top-K retrieval, RAG prompt assembly, cancellation | C# in `dotnet/Knode` |
| **Local data** | `corpus.jsonl`, OneNote snapshots, persisted embeddings (`vectors.bin`, `manifest.json`, `records.json`), optional DPAPI key blob | File I/O, local app data |
| **External APIs** | Embeddings/chat for answer flow; OneNote sync during setup/index | Google Gemini, Microsoft Graph via MSAL |

**Boundary reminder:** OneNote Graph calls happen at connect/section-pick/build-index time. Routine Ask reads the local index only.

---

## 4. Vision and future sources

This section is the **public** north star. It stays high level so contributors and readers share the same direction without publishing private details.

### What we are building toward

The through-line is **your** material in, **cited** answers out: retrieval and reasoning stay anchored to snippets, files, or media you ingested, not the open web. Experiences stay **goal-directed** (scoped questions, cross-source synthesis) rather than an open-ended agent running your machine.

### Scenario themes (illustrative, not a commitment list)

- **Reading and big moments:** Connect highlights across books before a career or life decision; surface themes and tensions you already captured.
- **Family and travel notes:** Patterns across itineraries, preferences, pace, and places (e.g. from notes or planners you control), with answers tied to those entries.
- **Voice and text together:** Questions over transcripts, memos, and typed notes in one personal corpus, once ingestion supports them.
- **Video or talks plus reading:** Relate something you saved from a clip or lecture to a passage you highlighted, with pointers to both.
- **Cross-corpus bridges:** Link ideas between unrelated repositories you own (for example, a theme in reading and a pattern in trip notes).
- **Work knowledge:** Recall across meeting notes, specs, or exports you choose to ingest, with provenance on each claim.

Technical building blocks for engineers stay in [§3](#3-architecture-layers-data-flow-technologies) and [`KNODE-ARCHITECTURE.md`](KNODE-ARCHITECTURE.md).

### Private scenario notes (local only, not for GitHub)

Rich vignettes (real names, employers, trips, or exploratory copy) belong in a **local-only** markdown file, for example **`docs/VISION-SCENARIOS.local.md`**, and should **not** be committed. Filenames ending in **`.local.md`** are **gitignored** at repo root so you can iterate privately; keep the **canonical guide** to sanitized bullets here in §4.

---

## Revision log

| Date | Change |
|------|--------|
| 2026-04-22 | Updated persona/scenarios/design text to explicitly include shipped OneNote connector and cross-source retrieval; removed corpus runbook section from this guide and kept detailed setup in install/first-run docs. |
| 2026-04-21 | Updated product framing from Kindle-only to Kindle + optional OneNote sections; added functional requirement row for OneNote source and privacy wording update. |
| 2026-04-09 | Story-style intro; §5 vision + public scenario themes; private **`*.local.md`** scenario notes + gitignore. |
| 2026-04-08 | Agentic product framing up front; §4 is corpus summary + INSTALL → FIRST-RUN order; long install/corpus runbooks moved to dedicated docs; em dash cleanup in this file. |
| 2026-04-07 | Hero screenshot; pointers to **KNODE-INSTALL** + **KNODE-FIRST-RUN** + **KNODE-ARCHITECTURE**; `validate_corpus` wording aligned with exit rules. |
| 2026-04-06 | Consolidated MVP-SPEC + ingestion decisions + ADR + runbooks into this file. |
