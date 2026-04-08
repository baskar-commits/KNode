# Product journey: Agentic Kindle Learnings (living doc)

**Purpose:** Remember **why** we chose what we chose, in order, so you can brief others or onboard collaborators without re-deriving context. Update this file when major decisions land.

**Audience:** Future you, teammates, interviewers.

**New readers:** For a **single consolidated** narrative (product intent, design choices, architecture, corpus pipeline, install/run), start with **[`KNODE-MVP-GUIDE.md`](KNODE-MVP-GUIDE.md)**. This file is the **chronological diary** behind that guide.

---

## 1. Where we started

**Idea:** An **agentic** assistant over **personal reading notes**: ask topics like “confidence” or “leadership” and get **grounded** answers from what you actually read, including **connections across books**.

**Initial constraints:**

- First source: **Kindle** only; later OneNote, Google, photos, etc.
- **Distribution:** local desktop for MVP.
- **Aspirations in the spec:** OAuth to Amazon, sync from [Kindle Notebook](https://read.amazon.com/notebook), async refresh, append merge policy, cross-book narrative.

**Artifact:** [`KNODE-MVP-GUIDE.md`](KNODE-MVP-GUIDE.md) §1 (successor to **`MVP-SPEC.md`** v0.2; that filename is **local-only** if you still keep it, not in the repo).

---

## 2. Spec evolution (what changed and why)

| Version | What we locked in |
|---------|---------------------|
| **v0.1** | Persona, scenarios, rough architecture, “iterate in 48–72 hours.” |
| **v0.2** | **Notebook** as source (not ad-hoc file export as primary), **OAuth/connector** mindset for Amazon + future Microsoft/Google, **async sync**, **append + dedupe** merge, **cross-book** “connected story,” **local desktop**, **RAG** as default assumption for Q&A, **MCP** explicitly not required for MVP. |

**Lesson:** The spec moved from “maybe file export” toward **Notebook-native** product language, then **reality** (API availability) forced us to distinguish **intent** from **implementation** (see §4).

---

## 3. Discovery: official API vs wishful OAuth

**Question:** Does `read.amazon.com/notebook` expose a **documented API** so third-party apps can pull highlights with OAuth?

**What we did:** Read public Amazon developer docs:

- [Login with Amazon](https://developer.amazon.com/docs/login-with-amazon/documentation-overview.html): identity; **not** a Notebook data API by itself.
- [Amazon Data Portability](https://developer.amazon.com/docs/amazon-data-portability/overview.html) + [available scopes](https://developer.amazon.com/docs/amazon-data-portability/available-scopes.html): official portable datasets; **Kindle** scopes listed are **device/usage-style** data, **not** “export all highlights/notes text” from Notebook.

**Conclusion:** There is **no** publicly documented, supported **Kindle Notebook REST API** for highlight/notebook content the way we originally spec’d. Products like [Readwise](https://docs.readwise.io/readwise/docs/importing-highlights/kindle) use **browser/extension** patterns, not a public Amazon Notebook API.

**Artifact:** [`KNODE-MVP-GUIDE.md`](KNODE-MVP-GUIDE.md) §2–4.

**Lesson:** **Validate platform APIs before locking “OAuth + sync” in a PRD**, or keep the spec honest with a “pending validation” gate.

---

## 4. Ingestion strategy: unofficial paths, and what we picked

We mapped **six** paths (extension, WebView, automation, email, file export, third-party API). None are as clean as a first-party API; each has trade-offs (brittle DOM, ToS, UX, privacy).

**Design-phase choice for Step 1 (spike):**  
**Option B: Embedded WebView** (single desktop app, **Edge WebView2** on Windows via **pywebview**), **not** .NET for the spike (no .NET SDK on the machine at that moment).

**Why WebView first:**

- One **installer** story later; **one** codebase for shell + extraction triggers.
- Same **session-based** access model as an extension, without Chrome Web Store **yet**.
- Extension remains a **logical upgrade** if WebView login is flaky or we want Readwise-like parity.

**Artifact:** `spike/webview-notebook/` (`main.py`, `README.md`).

---

## 5. Build friction: environment, not “product risk”

| Issue | Resolution |
|-------|------------|
| PowerShell **blocked `Activate.ps1`** | Use **`.venv\Scripts\python.exe`** directly (no activation) or `run.bat`; document in README. |
| **`python main.py` vs venv** | Global `python` lacked `pywebview`; always **`.\.venv\Scripts\python.exe main.py`**. |
| **First `Enter` only** | Background thread exited after one capture; **fixed** to a **loop** (`Enter` = append, `q` = quit) with **append** to `spike_extract.txt`. |

**Lesson:** Developer ergonomics belong in the **same** story as “business” decisions; future readers will hit the same walls.

---

## 6. Extraction and data quality

**What we capture:** `document.body.innerText`: **one blob** per capture, including sidebar, UI chrome, and **usually** book title, author, and highlight text when a book is selected.

**Structured output:** `parse_dump` turns deduped spike text into **`corpus.jsonl`** (one JSON per line) with stable **`id`**, **`book_title`**, author, location, highlight text.

**Deduping:** Re-capturing the **same** book produced **identical** bodies; **`dedupe_spike_extract.py`** (hash of whitespace-normalized body). **64** captures → **61** unique in one representative run.

**Orphan preamble:** Text **before** the first `CAPTURE #1` header is labeled **ORPHAN PREAMBLE** in `spike_extract_deduped.txt` for manual review.

**Artifacts:** `spike/.../QUALITY-CHECKLIST.md`, `dedupe_spike_extract.py`, `dedupe_report.txt`.

---

## 7. What we have not done yet (honest backlog)

- **Legal / ToS** review for WebView extraction (pre‑beta).
- **Tighter structured parsing** if Amazon changes Notebook DOM or `innerText` shape (today: heuristic `parse_dump`).
- **Golden question set** rigor: expand **[`eval/GOLDEN-QUESTIONS.md`](../eval/GOLDEN-QUESTIONS.md)** and automate checks where possible.
- **Connector interface** code beyond the spike (incremental sync, append + dedupe policy).
- **`docs/AMAZON-NOTEBOOK-ACCESS.md`** formal memo (optional).
- **Notebook-parity browse UI** (full library browser in-app).

---

## 8. How to use this repo for storytelling

| If you need… | Read… |
|--------------|--------|
| **Single guide** (product, design, architecture summary, corpus, install) | [`KNODE-MVP-GUIDE.md`](KNODE-MVP-GUIDE.md) |
| **Engineering depth** (components, two vector paths, Ask sequence) | [`KNODE-ARCHITECTURE.md`](KNODE-ARCHITECTURE.md) |
| Chronological narrative (this file) | [`JOURNEY.md`](JOURNEY.md) |
| Install / first run | [`KNODE-INSTALL.md`](KNODE-INSTALL.md), [`KNODE-FIRST-RUN.md`](KNODE-FIRST-RUN.md) |
| Run the spike | [`../spike/webview-notebook/README.md`](../spike/webview-notebook/README.md) |

---

## 9. MVP pipeline (parse → Chroma → query)

**Implemented** under `mvp/`:

- **`kindle_agent.parse_dump`:** Splits `YOUR KINDLE NOTES FOR:` sections, parses `<Color> highlight | Location:`, writes **`mvp/data/corpus.jsonl`**. Dedupes **by highlight id** when the same clip appears in multiple sections.
- **`kindle_agent.ingest`:** **ChromaDB** persistent store + **sentence-transformers** `all-MiniLM-L6-v2`.
- **`kindle_agent.query_cli`:** Semantic **retrieve** + optional **OpenAI** synthesis (fails gracefully on quota/errors).

**Docs:** [`mvp/README.md`](../mvp/README.md), [`KNODE-MVP-GUIDE.md`](KNODE-MVP-GUIDE.md) §3–4, [`KNODE-INSTALL.md`](KNODE-INSTALL.md), [`KNODE-FIRST-RUN.md`](KNODE-FIRST-RUN.md), [`eval/GOLDEN-QUESTIONS.md`](../eval/GOLDEN-QUESTIONS.md).

---

## 10. Knode desktop (WPF + Gemini + installer)

**Implemented** under `dotnet/Knode/`:

- **Same `corpus.jsonl`** as the Python MVP: browse or remember path; **persistent vector index** under `%LocalAppData%\Knode\index\` (SHA-256 of corpus + embedding model).
- **AI agent API key** stored optionally with **DPAPI**; labels framed for future providers; **MVP still calls Google Gemini** (embed + chat).
- **Windows installer** via **Inno Setup** (`dotnet/installer/Knode.iss`, build script `dotnet/build-installer.ps1`).
- **Security / GitHub releases** narrative: [`docs/SECURITY-AND-RELEASES.md`](SECURITY-AND-RELEASES.md).

Source layout: **`dotnet/Knode/`** (WPF app), **`appsettings.json`** next to the binary or project root for tuning (e.g. embedding batch delay).

---

## 11. Three days of execution (snapshot)

This section is a **snapshot** of how a short burst concentrated risk: validate assumptions, ship a vertical slice, then document for others.

| Day | Focus | Outcomes |
|-----|--------|----------|
| **1** | Reality check + spike | Confirmed no public Notebook API; ran **WebView2** capture loop, dedupe, and quality notes; friction documented (venv, PowerShell). |
| **2** | Data contract + Python RAG | **`corpus.jsonl`** schema, **`parse_dump`** / **`validate_corpus`**, **Chroma** + **MiniLM** **`ingest`** and **`query_cli`**; repeatable path from raw capture to semantic search. |
| **3** | Knode + release story | **WPF** UI (Setup, Ask, sources), **Gemini** embed + chat, on-disk index, **Inno** installer scripts; **KNODE-INSTALL** / **FIRST-RUN** / **ARCHITECTURE** / **MVP-GUIDE** so a stranger can install and run; root **README** trimmed for public GitHub. |

**Lesson:** Three days does **not** finish compliance, browse UI, or sync—but it **does** prove grounded Q&A over **your** highlights with a clear path from Notebook to index.

---

## 12. Revision log

| Date | Author | Change |
|------|--------|--------|
| 2026-04-06 |  | First consolidated journey (idea → spec → API reality → WebView spike → dedupe → backlog). |
| 2026-04-06 |  | §9 MVP: parse → Chroma → query CLI. |
| 2026-04-05 |  | §10 Knode: WPF UI, Gemini RAG, DPAPI key, disk index, Inno installer, corpus help dialog. |
| 2026-04-06 |  | Docs: consolidated **KNODE-MVP-GUIDE.md**; MVP-SPEC / KINDLE-INGESTION / ADR-0001 reduced to pointers. |
| 2026-04-07 |  | Docs: **KNODE-INSTALL.md**, **KNODE-FIRST-RUN.md**, **KNODE-ARCHITECTURE.md**; root README hub; MVP guide screenshot under **docs/images**. |
| 2026-04-08 |  | MVP guide opener (agentic framing); §4 corpus + runbook order. |
| 2026-04-09 |  | §11 three-day execution snapshot; backlog refreshed (RAG/indexing done); pointer table excludes unpublished local-only docs; architecture references prose flow. |
