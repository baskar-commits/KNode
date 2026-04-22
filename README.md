# KindleNotesAgent

**Published docs (GitHub Pages):** [Documentation hub](https://baskar-commits.github.io/KNode/) - requires Pages enabled and a **public** repo on Free; paths are site-root (`/KNode/...`), not `docs/...`.

## Product intent

**Knode** is a **Windows desktop** assistant for **topic Q&A** over **your own reading notes** with **citations**. Today that means a local Kindle **`corpus.jsonl`** plus optional OneNote sections you choose in Setup. The app retrieves passages with Gemini embeddings, then answers with Gemini chat grounded in those passages.

---

## Navigate this repo

Start here if you use the app:

| If you want to... | Open (HTML on Pages) |
|-------------------|----------------------|
| Product intent and scope | **[KNODE-MVP-GUIDE.html](https://baskar-commits.github.io/KNode/KNODE-MVP-GUIDE.html)** |
| Install and first run | **[KNODE-INSTALL.html](https://baskar-commits.github.io/KNode/KNODE-INSTALL.html)**, then **[KNODE-FIRST-RUN.html](https://baskar-commits.github.io/KNode/KNODE-FIRST-RUN.html)** |
| Architecture and release/security | **[KNODE-ARCHITECTURE.html](https://baskar-commits.github.io/KNode/KNODE-ARCHITECTURE.html)**, **[`SECURITY-AND-RELEASES.md`](https://github.com/baskar-commits/KNode/blob/main/docs/SECURITY-AND-RELEASES.md)** |

If you clone the repo for code work, subdirectory READMEs are developer utilities:

| Working on... | README |
|---------------|--------|
| WebView capture from Kindle Notebook | **[`spike/webview-notebook/README.md`](spike/webview-notebook/README.md)** |
| Python `parse_dump`, Chroma, `query_cli` | **[`mvp/README.md`](mvp/README.md)** |

---

## Install the app

End users: **[KNODE-INSTALL.html](https://baskar-commits.github.io/KNode/KNODE-INSTALL.html)** - Windows x64, .NET 8 Desktop Runtime, WebView2, SmartScreen note. Download **`KnodeSetup-*.exe`** from **[GitHub Releases](https://github.com/baskar-commits/KNode/releases)**.

Developers: local build, validation, `dotnet run`, and release steps are in **[`docs/SECURITY-AND-RELEASES.md`](docs/SECURITY-AND-RELEASES.md#release-checklist-overview)**.

---

## First run

**[KNODE-FIRST-RUN.html](https://baskar-commits.github.io/KNode/KNODE-FIRST-RUN.html)** covers corpus, optional OneNote setup, API key, Build index, and Ask.

---

## More documentation

| Topic | Link |
|-------|------|
| Documentation hub | **[baskar-commits.github.io/KNode/](https://baskar-commits.github.io/KNode/)** |
| Regenerate HTML from Markdown | `python scripts/build_doc_html.py` (see [`scripts/requirements-doc-html.txt`](scripts/requirements-doc-html.txt)) |
| Edit sources (Markdown in repo) | [`docs/KNODE-MVP-GUIDE.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-MVP-GUIDE.md), [`docs/KNODE-INSTALL.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-INSTALL.md), [`docs/KNODE-FIRST-RUN.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-FIRST-RUN.md), [`docs/KNODE-ARCHITECTURE.md`](https://github.com/baskar-commits/KNode/blob/main/docs/KNODE-ARCHITECTURE.md) |
| Keys, privacy, releases | [`SECURITY-AND-RELEASES.md`](https://github.com/baskar-commits/KNode/blob/main/docs/SECURITY-AND-RELEASES.md) |

---

## Repo hygiene

- `corpus.jsonl` and `mvp/data/*` (except tracked `mvp/data/README.md`) are gitignored.
- Never commit real API keys; use `appsettings.Local.json` (ignored) or environment variables.

---

## License

**MIT**: **[`LICENSE`](LICENSE)**.
