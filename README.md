# KindleNotesAgent

**Published docs (GitHub Pages):** [Documentation hub](https://baskar-commits.github.io/KNode/) · [Design hub](https://baskar-commits.github.io/KNode/DESIGN.html) — *requires Pages enabled and a **public** repo on Free; paths are site-root (`/KNode/…`), not `docs/…`.*

## Product intent

**Knode** is a **Windows desktop** assistant for **topic Q&A** over **your Kindle highlights**, with **citations** and room to grow toward **cross-book** themes. Reading data flows through a local **`corpus.jsonl`** (you create it on your machine; it is **not** in this repo). The app **retrieves** relevant passages with **Gemini embeddings**, then **answers** with **Gemini** chat, grounded in what it retrieved.

---

## Navigate this repo

**If you only use Knode** (installed app), you do **not** need any README except this page. For the full story start with **[KNODE-MVP-GUIDE (HTML)](https://baskar-commits.github.io/KNode/KNODE-MVP-GUIDE.html)**; then use **Install the app** and **First run** below. **[§4 Corpus](https://baskar-commits.github.io/KNode/KNODE-MVP-GUIDE.html#4-corpus-install-and-first-run)** covers building **`corpus.jsonl`**.

| If you want to… | Open (HTML on Pages) |
|-----------------|------|
| **One doc for product intent, design, and corpus steps** | **[KNODE-MVP-GUIDE.html](https://baskar-commits.github.io/KNode/KNODE-MVP-GUIDE.html)** |
| **Install and First Run the Windows App** | **[KNODE-INSTALL.html](https://baskar-commits.github.io/KNode/KNODE-INSTALL.html)**, then **[KNODE-FIRST-RUN.html](https://baskar-commits.github.io/KNode/KNODE-FIRST-RUN.html)** |
| **Architecture** (HTML) · **history & security** (Markdown on GitHub) | **[KNODE-ARCHITECTURE.html](https://baskar-commits.github.io/KNode/KNODE-ARCHITECTURE.html)** · [`JOURNEY.md`](https://github.com/baskar-commits/KNode/blob/main/docs/JOURNEY.md), [`SECURITY-AND-RELEASES.md`](https://github.com/baskar-commits/KNode/blob/main/docs/SECURITY-AND-RELEASES.md) |

**If you clone the repo to work on code**, subdirectory READMEs are **developer utilities** only (capture spike, Python CLI). Ignore `README.md` files inside **`mvp/.venv/`** or other dependencies—they are not project docs.

| Working on… | README |
|-------------|--------|
| WebView capture from Kindle Notebook | **[`spike/webview-notebook/README.md`](spike/webview-notebook/README.md)** |
| Python **`parse_dump`**, Chroma, **`query_cli`** | **[`mvp/README.md`](mvp/README.md)** |

The file **[`mvp/data/README.md`](mvp/data/README.md)** is a **short git stub** (why the folder is empty in a fresh clone); you only need it when changing ignore rules or the Python output path.

---

## Install the app

End users: **[KNODE-INSTALL.html](https://baskar-commits.github.io/KNode/KNODE-INSTALL.html)** — Windows x64, .NET 8 Desktop Runtime, WebView2, SmartScreen note, GitHub Release installer.

Developers: **local build, validation, `dotnet run`, and GitHub Actions** (exact directories and PowerShell commands) live in **[`docs/SECURITY-AND-RELEASES.md`](docs/SECURITY-AND-RELEASES.md#local-build-validation-and-github-maintainers)** under *Local build, validation, and GitHub (maintainers)*. **Inno** package: [`dotnet/installer/Knode.iss`](dotnet/installer/Knode.iss), [`dotnet/build-installer.ps1`](dotnet/build-installer.ps1) → **`dotnet/dist-installer/KnodeSetup-*.exe`** (not committed). Distribution and trust: same doc.

---

## First run

**[KNODE-FIRST-RUN.html](https://baskar-commits.github.io/KNode/KNODE-FIRST-RUN.html)** — point Knode at **`corpus.jsonl`**, API key, **Build index**, then **Ask**.

Producing **`corpus.jsonl`**: **[KNODE-MVP-GUIDE §4](https://baskar-commits.github.io/KNode/KNODE-MVP-GUIDE.html#4-corpus-install-and-first-run)** (WebView spike, dedupe, `parse_dump`, `validate_corpus`).

---

## More documentation

| Topic | Link |
|--------|-----|
| **Documentation hub** (all HTML entry points) | **[baskar-commits.github.io/KNode/](https://baskar-commits.github.io/KNode/)** |
| **Design hub** | **[DESIGN.html](https://baskar-commits.github.io/KNode/DESIGN.html)** |
| **Regenerate HTML** from Markdown | `python scripts/build_doc_html.py` — see [`scripts/requirements-doc-html.txt`](scripts/requirements-doc-html.txt) |
| **Edit sources** (Markdown in repo) | [`docs/KNODE-MVP-GUIDE.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-MVP-GUIDE.md) · [`docs/KNODE-INSTALL.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-INSTALL.md) · [`docs/KNODE-FIRST-RUN.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-FIRST-RUN.md) · [`docs/KNODE-ARCHITECTURE.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-ARCHITECTURE.md) |
| Chronological decisions | [`JOURNEY.md`](https://github.com/baskar-commits/KNode/blob/main/docs/JOURNEY.md) |
| Keys, privacy, releases | [`SECURITY-AND-RELEASES.md`](https://github.com/baskar-commits/KNode/blob/main/docs/SECURITY-AND-RELEASES.md) |

**Legacy stubs**: [`KINDLE-INGESTION-DECISIONS.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KINDLE-INGESTION-DECISIONS.md), [`ADR-0001-mvp-stack.md`](https://github.com/baskar-commits/KNode/blob/main/mvp/docs/ADR-0001-mvp-stack.md). (**`MVP-SPEC.md`** is local-only / gitignored.)

---

## Repo hygiene

- **`corpus.jsonl`** and **`mvp/data/*`** (except the tracked **[`mvp/data/README.md`](mvp/data/README.md)** stub) are **gitignored**; see root **[`.gitignore`](.gitignore)**.
- **Never commit** real API keys; use **`appsettings.Local.json`** (ignored) or env vars. See **[`SECURITY-AND-RELEASES.md`](https://github.com/baskar-commits/KNode/blob/main/docs/SECURITY-AND-RELEASES.md)**.
- **UI screenshot** (served on Pages under **`images/`**): **[`knode-ask-ui.png`](https://baskar-commits.github.io/KNode/images/knode-ask-ui.png)** · [`blob` source](https://github.com/baskar-commits/KNode/blob/main/docs/images/knode-ask-ui.png).

---

## License

**MIT**: **[`LICENSE`](LICENSE)**.
