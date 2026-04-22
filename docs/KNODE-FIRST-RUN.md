# First 15 minutes with Knode

Use this after **[KNODE-INSTALL.md](KNODE-INSTALL.md)**. Goal: **Kindle `corpus.jsonl`** → **(optional) OneNote sections** → **API key** → **Build index** → **first Ask**.

---

## 1. Create or locate `corpus.jsonl`

Knode does **not** ship reading data. You need a **JSONL** file: one JSON object per line (Kindle highlight rows). Typical path:

1. **Capture** from [Kindle Notebook](https://read.amazon.com/notebook) with the **WebView spike**: [`spike/webview-notebook/README.md`](../spike/webview-notebook/README.md).
2. **Dedupe** (if needed) -> run **`parse_dump`** and **`validate_corpus`** from **`mvp/`**: [`mvp/README.md`](../mvp/README.md).

**In the app:** **Help** → *How do I get corpus.jsonl?* repeats this pipeline with commands.

---

## 2. Open Knode and use **Setup · Corpus & index**

In the **left nav**, choose **Setup · Corpus & index** (gear).

### 2a. Kindle corpus and Gemini key (required)

1. **Highlight file** → **Browse…** → select your **`corpus.jsonl`**.
2. **AI agent API key** → paste a **[Google AI Studio](https://aistudio.google.com/apikey)** key (this MVP uses **Gemini** only).  
   Optional: **Remember API key** (stored with Windows **DPAPI** under `%LocalAppData%\Knode\`).

### 2b. OneNote (optional)

Skip this block if you only want Kindle highlights.

1. Complete **[KNODE-INSTALL.md § Optional: OneNote](KNODE-INSTALL.md#optional-onenote-personal-or-work-microsoft-account)** - Entra app registration, **`http://localhost`** redirect, delegated **`Notes.Read`**, and **`Knode:OneNote:ClientId`** in **`appsettings.Local.json`** next to **`Knode.exe`**.
2. In Setup, click **Connect OneNote** and sign in with the Microsoft account that owns your notebooks.
3. **Select OneNote sections** → pick the sections whose pages you want in the index (personal notebooks you can read in OneNote).

### 2c. Build the index

1. Click **Build index**.  
   - First time on a large library may take several minutes (embedding batches).  
   - If you already had an index for the **same** corpus hash, model, and OneNote selection signature, unchanged rows may **reuse** stored vectors (see [KNODE-ARCHITECTURE.md](KNODE-ARCHITECTURE.md)).
2. Wait until status shows **ready** (e.g. loaded or built with row counts).

---

## 3. Ask a question

1. Switch the left nav to **Ask**.
2. Type a question (or use a **Try a prompt** chip).
3. Click **Ask**.
4. Read the **Answer**; expand **Sources** to see retrieved passages and match strength (Kindle books and/or OneNote pages, depending on what indexed).

---

## 4. If something fails

| Symptom | What to try |
|---------|-------------|
| **Ask** disabled | Finish **Setup**: valid corpus path, key present, **Build index** succeeded. |
| Blank Answer / Sources panes | Install **WebView2 Runtime**; restart Knode. |
| **429** / rate limit during index | Increase `Knode:EmbeddingBatchDelayMs` in `appsettings.json` next to **`Knode.exe`** (see `dotnet/Knode/appsettings.json` in source). |
| **503** / “high demand” / `UNAVAILABLE` on **Ask** or **Build index** | Usually **temporary** on Google’s side. The app **retries** (up to several times with backoff). Wait and try **Ask** again; try **off-peak** hours; confirm quota in **Google AI Studio**. You can set **`ChatModel`** to another Gemini model in `appsettings.json` if one tier is overloaded. |
| Wrong or empty retrieval | Re-run **`validate_corpus`** on the JSONL; check **Setup** path matches the file you indexed. |
| **Connect OneNote** fails / “ClientId is not set” | Ensure **`appsettings.Local.json`** sits next to **`Knode.exe`**, with **`Knode:OneNote:ClientId`** set to your app’s client ID. Confirm redirect **`http://localhost`** and Graph **`Notes.Read`** on the registration. |
| OneNote pages missing after sync | Re-open **Select OneNote sections**; confirm sections are checked; **Build index** again after changing selection. |

More: [KNODE-MVP-GUIDE.md](KNODE-MVP-GUIDE.md) (product and architecture context), **[`spike/webview-notebook/QUALITY-CHECKLIST.md`](../spike/webview-notebook/QUALITY-CHECKLIST.md)** (capture quality).
