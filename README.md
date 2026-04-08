# KindleNotesAgent

## Product intent

**Knode** is a **Windows desktop** assistant for **topic Q&A** over **your Kindle highlights**, with **citations** and room to grow toward **cross-book** themes. Reading data flows through a local **`corpus.jsonl`** (you create it on your machine; it is **not** in this repo). The app **retrieves** relevant passages with **Gemini embeddings**, then **answers** with **Gemini** chat, grounded in what it retrieved.

---

## Navigate this repo

**If you only use Knode** (installed app), you do **not** need any README except this page. For the full story start with **`docs/KNODE-MVP-GUIDE.md`**; then use **Install the app** and **First run** below. **`KNODE-MVP-GUIDE`** §4 covers building **`corpus.jsonl`**.

| If you want to… | Open |
|-----------------|------|
| **One doc for product intent, design, and corpus steps** | **`docs/KNODE-MVP-GUIDE.md`** |
| **Install and First Run the Windows App** | **`docs/KNODE-INSTALL.md`**, then **`docs/KNODE-FIRST-RUN.md`** |
| **Architecture, history, security** | **`docs/KNODE-ARCHITECTURE.md`**, **`docs/JOURNEY.md`**, **`docs/SECURITY-AND-RELEASES.md`** |

**If you clone the repo to work on code**, subdirectory READMEs are **developer utilities** only (capture spike, Python CLI). Ignore `README.md` files inside **`mvp/.venv/`** or other dependencies—they are not project docs.

| Working on… | README |
|-------------|--------|
| WebView capture from Kindle Notebook | **`spike/webview-notebook/README.md`** |
| Python **`parse_dump`**, Chroma, **`query_cli`** | **`mvp/README.md`** |

The file **`mvp/data/README.md`** is a **short git stub** (why the folder is empty in a fresh clone); you only need it when changing ignore rules or the Python output path.

---

## Install the app

End users: **[`docs/KNODE-INSTALL.md`](docs/KNODE-INSTALL.md)** — Windows x64, .NET 8 Desktop Runtime, WebView2, SmartScreen note, GitHub Release installer.

Developers building the **Inno** package: `dotnet/installer/Knode.iss`, **`dotnet/build-installer.ps1`** → **`dotnet/dist-installer/KnodeSetup-*.exe`**. Distribution and trust: **[`docs/SECURITY-AND-RELEASES.md`](docs/SECURITY-AND-RELEASES.md)**.

---

## First run

**[`docs/KNODE-FIRST-RUN.md`](docs/KNODE-FIRST-RUN.md)** — point Knode at **`corpus.jsonl`**, API key, **Build index**, then **Ask**.

Producing **`corpus.jsonl`**: **[`docs/KNODE-MVP-GUIDE.md`](docs/KNODE-MVP-GUIDE.md)** §4 (WebView spike, dedupe, `parse_dump`, `validate_corpus`).

---

## More documentation (on GitHub)

| Topic | Doc |
|--------|-----|
| Product, personas, architecture summary | **[`docs/KNODE-MVP-GUIDE.md`](docs/KNODE-MVP-GUIDE.md)** |
| Detailed architecture (tiers, components, RAG loop) | **[`docs/KNODE-ARCHITECTURE.md`](docs/KNODE-ARCHITECTURE.md)** |
| Chronological decisions | **[`docs/JOURNEY.md`](docs/JOURNEY.md)** |
| Keys, privacy, releases | **[`docs/SECURITY-AND-RELEASES.md`](docs/SECURITY-AND-RELEASES.md)** |

**Legacy stubs** (short pointers into the guide): [`MVP-SPEC.md`](MVP-SPEC.md), [`docs/KINDLE-INGESTION-DECISIONS.md`](docs/KINDLE-INGESTION-DECISIONS.md), [`mvp/docs/ADR-0001-mvp-stack.md`](mvp/docs/ADR-0001-mvp-stack.md).

---

## Repo hygiene

- **`corpus.jsonl`** and **`mvp/data/*`** (except the tracked **`mvp/data/README.md`** stub) are **gitignored**; see root **`.gitignore`**.
- **Never commit** real API keys; use **`appsettings.Local.json`** (ignored) or env vars. See **`docs/SECURITY-AND-RELEASES.md`**.
- **UI screenshot** for the guide: **`docs/images/knode-ask-ui.png`**.

---

## License

**MIT**: **[`LICENSE`](LICENSE)**.
