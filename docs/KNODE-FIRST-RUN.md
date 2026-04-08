# First 15 minutes with Knode

Use this after **[KNODE-INSTALL.md](KNODE-INSTALL.md)**. Goal: **corpus** → **API key** → **index** → **first answer**.

---

## 1. Create or locate `corpus.jsonl`

Knode does **not** ship reading data. You need a **JSONL** file: one JSON object per line (Kindle highlight rows). Typical path:

1. **Capture** from [Kindle Notebook](https://read.amazon.com/notebook) with the **WebView spike**: [`spike/webview-notebook/README.md`](../spike/webview-notebook/README.md).
2. **Dedupe** (if needed) → run **`parse_dump`** and **`validate_corpus`** from **`mvp/`**: [KNODE-MVP-GUIDE.md §4](KNODE-MVP-GUIDE.md#4-corpus-install-and-first-run).

**In the app:** **Help** → *How do I get corpus.jsonl?* repeats this pipeline with commands.

---

## 2. Open Knode and use **Setup · Corpus & index**

1. In the **left nav**, choose **Setup · Corpus & index** (gear).
2. **Highlight file** → **Browse…** → select your **`corpus.jsonl`**.
3. **AI agent API key** → paste a **[Google AI Studio](https://aistudio.google.com/apikey)** key (this MVP uses **Gemini** only).  
   Optional: **Remember API key** (stored with Windows **DPAPI** under `%LocalAppData%\Knode\`).
4. Click **Build index**.  
   - First time on a large library may take several minutes (embedding batches).  
   - If you already had an index for the **same** file hash and model, it may load without re-embedding.

Wait until status shows **ready** (e.g. loaded or built with highlight count).

---

## 3. Ask a question

1. Switch the left nav to **Ask**.
2. Type a question (or use a **Try a prompt** chip).
3. Click **Ask**.
4. Read the **Answer**; expand **Sources** to see retrieved passages and match strength.

---

## 4. If something fails

| Symptom | What to try |
|---------|-------------|
| **Ask** disabled | Finish **Setup**: valid corpus path, key present, **Build index** succeeded. |
| Blank Answer / Sources panes | Install **WebView2 Runtime**; restart Knode. |
| **429** / rate limit during index | Increase `Knode:EmbeddingBatchDelayMs` in `appsettings.json` next to **`Knode.exe`** (see `dotnet/Knode/appsettings.json` in source). |
| Wrong or empty retrieval | Re-run **`validate_corpus`** on the JSONL; check **Setup** path matches the file you indexed. |

More: [KNODE-MVP-GUIDE.md](KNODE-MVP-GUIDE.md) (product and architecture context), **[`spike/webview-notebook/QUALITY-CHECKLIST.md`](../spike/webview-notebook/QUALITY-CHECKLIST.md)** (capture quality).
