# Local corpus output (not in GitHub)

This folder is where the Python pipeline writes **`corpus.jsonl`** by default.

- **Nothing here is committed.** The repo does not ship anyone’s Kindle highlights. After `git clone`, you still run the ingestion pipeline (see **[`docs/KNODE-MVP-GUIDE.md`](../../docs/KNODE-MVP-GUIDE.md)** §4) or point Knode at a `corpus.jsonl` you created elsewhere.
- **`corpus.jsonl`** is listed in the **root `.gitignore`** so it cannot be published accidentally, even if moved outside `mvp/data/`.

```powershell
# From repo mvp/: produces ./data/corpus.jsonl (ignored by git)
.\.venv\Scripts\python.exe -m kindle_agent.parse_dump
# Exit 0 only when every row has book_title (run before Knode Build index)
.\.venv\Scripts\python.exe -m kindle_agent.validate_corpus
```

Upstream: **WebView capture** → optional **`dedupe_spike_extract.py`** in `spike/webview-notebook/` → **`parse_dump`** (also dedupes by stable `id` across sections) → **`validate_corpus`**.

Then **Knode** → **Setup · Corpus & index** → **Browse** → select that file → **Build index**.
