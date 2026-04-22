# MVP: Kindle agent (parse → RAG → query)

## What this does

1. **`parse_dump`:** Reads `spike/webview-notebook/spike_extract_deduped.txt`, outputs **`data/corpus.jsonl`** (one highlight per line: book, author, location, text, note).
2. **`ingest`:** Embeds with **sentence-transformers** (`all-MiniLM-L6-v2`) into **ChromaDB** under **`data/chroma/`**.
3. **`query_cli`:** Semantic search + prints top passages; if **`OPENAI_API_KEY`** is set, adds a short **grounded** answer.

Stack rationale and architecture: **[`docs/KNODE-MVP-GUIDE.md`](../docs/KNODE-MVP-GUIDE.md)** §3 and **[`docs/KNODE-ARCHITECTURE.md`](../docs/KNODE-ARCHITECTURE.md)**. ADR pointer: **[`mvp/docs/ADR-0001-mvp-stack.md`](docs/ADR-0001-mvp-stack.md)**.

## Setup (from `mvp/`)

**Dependencies:** Top-level pins live in **`requirements.in`**. **`requirements.txt`** is a **locked** resolve from [**pip-tools**](https://pypi.org/project/pip-tools/) (`pip-compile`). Edit **`requirements.in`**, then regenerate **`requirements.txt`**, then commit both (see **[`docs/SECURITY-AND-RELEASES.md`](../docs/SECURITY-AND-RELEASES.md)**).

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

First install pulls **PyTorch** + **sentence-transformers** (large download).

## Run pipeline

```powershell
cd path\to\KindleNotesAgent\mvp
.\.venv\Scripts\python.exe -m kindle_agent.parse_dump
.\.venv\Scripts\python.exe -m kindle_agent.validate_corpus
.\.venv\Scripts\python.exe -m kindle_agent.ingest
.\.venv\Scripts\python.exe -m kindle_agent.query_cli "What did I read about confidence?"
```

Interactive mode:

```powershell
.\.venv\Scripts\python.exe -m kindle_agent.query_cli -i
```

Optional synthesis:

```powershell
$env:OPENAI_API_KEY = "sk-..."
.\.venv\Scripts\python.exe -m kindle_agent.query_cli "How do I think about mistakes?"
```

## Data

- **`data/corpus.jsonl`** (and **`data/chroma/`**) are **never committed**; see root **`.gitignore`** (`**/corpus.jsonl`, `mvp/data/*` with only **`mvp/data/README.md`** tracked). Clone the repo, then generate **`corpus.jsonl`** here or point Knode at your file (setup walkthrough: **[KNODE-FIRST-RUN.md](../docs/KNODE-FIRST-RUN.md)**).
- Re-run **ingest** after changing the corpus or model.

## Next steps

- **`eval/GOLDEN-QUESTIONS.md`** at repo root (manual rubric).
- **Parser**: tighten heuristics if Amazon changes `innerText` layout.
- **Desktop UI**: **`../dotnet/Knode/`** (**Knode**: WPF + Gemini + Windows installer; see **[`docs/KNODE-INSTALL.md`](../docs/KNODE-INSTALL.md)**). The Python **`query_cli`** remains optional for terminal-only workflows.
