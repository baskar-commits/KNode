# Knode architecture: detailed design

**Audience:** software engineers and architects reviewing **Knode** (`dotnet/Knode`), its **vector retrieval** layer, and the optional **Python / Chroma** path. For product intent and personas, see [KNODE-MVP-GUIDE.md](KNODE-MVP-GUIDE.md).

---

## Agentic positioning vs what ships today

**Knode** is marketed as an **agentic AI** application: a companion that **grounds** reasoning in **your** reading, **selects** relevant evidence, and **acts** through **transparent** outputs (citations, scoped retrieval, optional sources panel). The **roadmap** points to richer **agentic** behavior: **multi-step** planning, **tool** use (refresh corpus, validate export), and **longer** sessions, still anchored in the private **`corpus.jsonl`** contract.

**Today’s shipped loop** is a **single** well-defined **agentic primitive**: **retrieve** (semantic Top-K over a **vector** store after optional **scope** filters) **then** **generate** (one **Gemini** `generateContent` call constrained to retrieved passages). That pattern is **RAG** (retrieval-augmented generation). Naming it **agentic** reflects **intent and UX** (your library as the action space); it does **not** imply an autonomous multi-tool planner in the v1 binary.

---

## Vector stores: two implementations (same `corpus.jsonl`)

| Path | Vector database | Embedding model | When to use |
|------|-----------------|-----------------|-------------|
| **Knode (desktop)** | **Custom on-disk index**, **not** Chroma: **`VectorIndex`** in memory at runtime + **`PersistentIndexStore`** (`vectors.bin`, `manifest.json`, `records.json` under `%LocalAppData%\Knode\index\`). Cosine similarity in C#. | **Google Gemini** `batchEmbedContents` / query embed (config default `gemini-embedding-001`). | **End-user** installs; one vendor for embed + chat. |
| **Python MVP** | **ChromaDB** persistent collection under **`mvp/data/chroma/`**. | **sentence-transformers** `all-MiniLM-L6-v2` (local). | **Dev / CLI** prototyping; [`mvp/README.md`](../mvp/README.md), [`kindle_agent.ingest`](../mvp/kindle_agent). |

Both paths consume the **same** **`corpus.jsonl`** **contract** (one JSON per line, `HighlightRecord` shape in C#). **Knode does not embed Chroma** in the shipping EXE; if you hear “vector DB” for Knode, it refers to the **Gemini-backed vector index** files above, **unless** you are explicitly on the **Python Chroma** track.

---

## System context (tiers and data flow)

**0 — Ingestion (offline or dev)**  
Kindle Notebook (web) → WebView2 spike capture → optional dedupe script → `parse_dump` (Python) → **`corpus.jsonl`**.

**1 — Presentation (WPF)**  
**Knode** `MainWindow`: **Ask**, **Setup**, **Help**; navigation and WebView-backed answer/sources.

**2 — Application (C#)**  
Orchestration: config, corpus hash, optional DPAPI for keys. **Build index:** read JSONL, batch embed via Gemini, load **`VectorIndex`**, persist via **`PersistentIndexStore`**. **Load index** from disk when corpus hash and model match manifest. **Ask:** embed question, apply **BookScope** / **YearScope**, **Top-K** cosine search, assemble RAG prompt, send **`generateContent`**, render answer and sources.

**3 — Local persistence**  
Knode **`index/`** folder (`vectors.bin`, `manifest`, `records`); optional DPAPI key blob; `user_settings.json` for paths and UI prefs.

**4 — External HTTPS**  
Google Gemini embedding API and `generateContent` (configurable base URL).

Edges in short: **`corpus.jsonl`** feeds the UI and orchestrator; orchestrator builds or loads the index; index and query embeddings use Gemini; retrieved passages plus prompt go to Gemini chat; answers return to the UI.

---

## Component reference (what each piece does)

### Ingestion chain (outside the EXE)

| Component | Location | Inputs / outputs | Notes |
|-----------|----------|------------------|--------|
| **WebView spike** | `spike/webview-notebook` | Notebook session → `spike_extract.txt` | **pywebview** + **WebView2**; raw `innerText` blobs. |
| **Dedupe** | `dedupe_spike_extract.py` | Duplicates removed → `spike_extract_deduped.txt` | Hash-normalized bodies; **report** in `dedupe_report.txt`. |
| **parse_dump** | `mvp/kindle_agent/parse_dump.py` | Deduped text → **`corpus.jsonl`** | Heuristic parser; stable **`id`** per highlight; dedupes **ids** across sections. |
| **validate_corpus** | `mvp/kindle_agent/validate_corpus.py` | **`corpus.jsonl`** → exit code | **Exit 0** iff every row has non-empty **`book_title`**. |

### Presentation (WPF)

| Component | Location | Responsibility |
|-----------|----------|----------------|
| **MainWindow** | `dotnet/Knode/MainWindow.xaml(.cs)` | **Ask** (question, chips, banner, answer WebView, collapsible sources), **Setup** (corpus path, key, build), **Help**; left nav; `MainNav` selection. |
| **CorpusHelpWindow** | `CorpusHelpWindow.xaml` | Modal copy for **corpus.jsonl** pipeline. |
| **MarkdownToHtml** + **WebView2** | `Services/MarkdownToHtml.cs` | Markdown → sanitized HTML for Answer / Sources. |

### Application: orchestration and RAG

| Component | Location | Responsibility |
|-----------|----------|----------------|
| **KnodeRagService** | `Services/KnodeRagService.cs` | **`BuildIndexAsync`**: read JSONL, filter lines, batch **Gemini** embed, **`VectorIndex.Load`**, **`PersistentIndexStore.SaveAsync`**. **`AskAsync`**: scope → embed question → **`VectorIndex.Search`** → build context string → **`GeminiClient.GenerateContentAsync`**. |
| **GeminiClient** | `Services/GeminiClient.cs` | HTTP to Google Generative Language API: **embed** + **chat**. |
| **VectorIndex** | `Services/VectorIndex.cs` | In-memory **float[]** vectors + **`HighlightRecord`** list; **cosine** Top-K search. |
| **PersistentIndexStore** | `Services/PersistentIndexStore.cs` | **SHA-256** of corpus file + embedding model id in **manifest**; load/save **vectors** + **records** for skip-rebuild when unchanged. |
| **BookScopeResolver** / **BookScopeResult** | `Services/BookScope*.cs` | Restrict candidate highlight indices when question matches **`book_title`** substrings (configurable min length). |
| **YearScopeResolver** | `Services/YearScopeResolver.cs` | Restrict by **`last_accessed`** calendar year when question mentions **20xx**. |
| **HighlightRecord** / **HighlightRecordJson** | `Services/HighlightRecord*.cs` | JSONL row shape; deserialize with tolerant extra keys; **EmbedText** / **CitationLine** for model + UI. |
| **RagQueryLogger** | `Services/RagQueryLogger.cs` | Optional append-only **`rag-YYYYMMDD.log`** under `%LocalAppData%\Knode\logs\`. |

### Local artifacts (Knode, not Chroma)

| Artifact | Role |
|----------|------|
| **`corpus.jsonl`** | User-supplied source of truth for highlights (path in Setup). |
| **`index/`** folder | **Cached** embeddings + metadata; safe to delete to force full **re-embed**. |
| **`user_settings.json`** | Last corpus path, UI prefs (not secrets). |
| **DPAPI blob** | Optional saved API key (see security doc). |

---

## Ask sequence (step by step)

1. User submits a question (and Top-K, scopes) in **MainWindow**.
2. **MainWindow** calls **KnodeRagService.AskAsync**.
3. **KnodeRagService** asks **BookScope** / **YearScope** for candidate index subsets (or full corpus).
4. **KnodeRagService** calls **GeminiClient** to **embed the question**.
5. **VectorIndex.Search** runs **Top-K** cosine similarity over vectors, respecting scope filters.
6. Service **builds the prompt** from retrieved passages only (citations, no extra web context).
7. **GeminiClient.GenerateContentAsync** returns the answer text.
8. **UI** shows the answer and optional **Sources** expander with retrieved excerpts and scores.

---

## Optional Python stack (ChromaDB)

Same **`corpus.jsonl`** after **`ingest`**:

- **Vector DB:** **Chroma** collection on disk.
- **Embed:** **sentence-transformers** (no Gemini required for this path).
- **Query:** **`query_cli`**; optional **OpenAI** for synthesis.

Use this to **compare** retrieval behavior or work **offline** from Google embed APIs; it is **not** bundled in **`KnodeSetup.exe`**.

---

## Interfaces and dependencies (summary)

- **Inbound:** filesystem **`corpus.jsonl`**, user question string, **`appsettings.json`** + optional env **`GEMINI_API_KEY`** / **`AGENT_API_KEY`**, Windows **DPAPI** for saved key.
- **Outbound HTTPS:** **Google Gemini** embedding + generate endpoints (configurable **BaseUrl**).
- **No** inbound network service in MVP; **no** Chroma process in Knode EXE.

---

## Revision log

| Date | Change |
|------|--------|
| 2026-04-07 | Expanded for engineers: Chroma vs Knode on-disk vector index, per-component tables, tiered system context, step-by-step Ask sequence, agentic product framing vs shipped RAG loop. |
| 2026-04-09 | Replaced diagram-only sections with prose flow (public doc set). |
