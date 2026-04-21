# Security and releases: Knode / KindleNotesAgent

This document is the **maintainer path** from local changes to a **GitHub Release** asset (`KnodeSetup-x.y.z.exe`), plus the **minimum security rules** you must not violate.

**How to read it**

- Follow **Phases 1–6** in order.
- Each phase ends with **“Details”** pointing to an appendix with copy-paste commands and footguns.
- If you only need commands, jump to **[Appendix A — Commands (copy/paste)](#appendix-a-commands)**.

**Repository root** is the folder that contains **`dotnet\`**, **`docs\`**, **`scripts\`**, and **`.github\`**. Unless a command says otherwise, open PowerShell **here**.

---

<h2 id="release-checklist-overview">Release checklist (overview)</h2>

| Phase | Goal | Where to read |
|------:|------|----------------|
| 1 | Prove dependencies are clean enough to ship | [Phase 1](#phase-1-dependency-audit) · [Appendix A](#appendix-a-commands) |
| 2 | Pick the next semver and make **source files agree** | [Phase 2](#phase-2-version-bump--sync-checkpoint) · [Appendix A](#appendix-a-commands) |
| 3 | Build Release binaries and (optionally) the installer | [Phase 3](#phase-3-build--package) · [Appendix A](#appendix-a-commands) |
| 4 | Smoke test locally | [Phase 4](#phase-4-smoke-test) · [Appendix A](#appendix-a-commands) |
| 5 | Commit + push `main` | [Phase 5](#phase-5-commit--push-main) · [Appendix A9](#appendix-a9-docs) · [Appendix A7](#appendix-a7-commit-push) · [Appendix D](#appendix-d-secrets) |
| 6 | Tag → GitHub Release asset | [Phase 6](#phase-6-tag--github-release) · [Appendix A8](#appendix-a8-tag) · [Appendix B](#appendix-b-github-ci) |

---

<h2 id="phase-1-dependency-audit">Phase 1 — Dependency audit</h2>

**Do**

- Run `scripts\security-audit.ps1`.
- If it reports actionable issues, fix them before continuing.

**Done when**

- NuGet vulnerability scan is clean (or you have a documented exception you accept for this release).
- Python lockfile audits are clean (this repo documents one known ignore path for `mvp/` — see [Appendix C](#appendix-c-python-cve)).

**Details:** [Appendix A — Commands](#appendix-a-commands) · [Appendix C](#appendix-c-python-cve)

---

<h2 id="phase-2-version-bump--sync-checkpoint">Phase 2 — Version bump + sync checkpoint</h2>

**Source of truth**

- `dotnet\Knode\Knode.csproj` → `<Version>X.Y.Z</Version>`
- `dotnet\installer\Knode.iss` → `#define MyAppVersion "X.Y.Z"`

**Hard gate (before Phase 5)**

1. Set `X.Y.Z` in **both** files.
2. Confirm both read the **same** `X.Y.Z`.
3. Only after Phase 5 push does GitHub “know” the new version — **local builds do not update GitHub by themselves**.

**Details:** [Appendix A — Commands](#appendix-a-commands)

---

<h2 id="phase-3-build--package">Phase 3 — Build + package</h2>

**Do**

- `dotnet build .\dotnet\Knode.sln -c Release`
- Optional installer: run **`dotnet\build-installer.ps1`** (publish + Inno). Requires **Inno Setup** (`ISCC.exe`). If the compiler is installed but **not on `PATH`**, pass **`-InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"`** (copy/paste under **A.4** in [Appendix A](#appendix-a-commands)).

**Done when**

- Release build succeeds.
- If packaging succeeded: `dotnet\dist-installer\KnodeSetup-X.Y.Z.exe` exists.

**Details:** [Appendix A — Commands](#appendix-a-commands) (includes **repo root vs `dotnet\` folder** path rules)

---

<h2 id="phase-4-smoke-test">Phase 4 — Smoke test</h2>

**Do**

- Run the app from Release bits **or** install the local `KnodeSetup-*.exe`.
- Click through: Setup → Build index → Ask (and any connectors you ship in that release, e.g. OneNote).

**Done when**

- No startup crash, key/index flows work, and you are willing to put your name on the release.

**Details:** [Appendix A — Commands](#appendix-a-commands)

---

<h2 id="phase-5-commit--push-main">Phase 5 — Commit + push `main`</h2>

**Do**

- **Documentation (if this release touched docs):** Regenerate **`docs/KNODE-*.html`** from Markdown when you changed the matching **`docs/KNODE-*.md`** (see [Appendix A9](#appendix-a9-docs)). Commit **`.md` + generated `.html` together** so **GitHub Pages** (`/docs` on `main`) matches the repo. Hand-maintained pages such as **`docs/index.html`** and **`docs/DESIGN.html`** ship as-is—include them in the same commit when you update them.
- `git status` / `git diff` — confirm **no secrets** and **no build outputs** are staged (see [Appendix A9](#appendix-a9-docs) for suggested commands).
- Commit with a message that states what shipped (features + version bump + doc changes if any).
- `git push origin main`

**Never commit**

- Real API keys, tokens, or private corpora (`corpus.jsonl`).
- Connector secrets in tracked files when they should live in `appsettings.Local.json` (gitignored).

**Details:** [Appendix A9 — Documentation sync](#appendix-a9-docs) · [Appendix A7 — Commit + push](#appendix-a7-commit-push) · [Appendix D — Secret hygiene + local data](#appendix-d-secrets)

---

<h2 id="phase-6-tag--github-release">Phase 6 — Tag → GitHub Release</h2>

**Do**

- Run `scripts\Push-KnodeReleaseTag.ps1` with `-WhatIf` first, then for real.
- Confirm the Release asset appears on GitHub and matches the intended version.

**Important**

- `Push-KnodeReleaseTag.ps1` reads `<Version>` from the **already pushed** `Knode.csproj` and checks `Knode.iss` matches.

**Details:** [Appendix A8 — Tag](#appendix-a8-tag) · [Appendix B — CI + download location](#appendix-b-github-ci)

---

<h2 id="appendix-a-commands">Appendix A — Commands (copy/paste)</h2>

### A.1 Dependency audit

```powershell
cd C:\path\to\KindleNotesAgent
python -m pip install pip-audit
powershell -ExecutionPolicy Bypass -File .\scripts\security-audit.ps1
```

### A.2 Version bump (optional helper)

```powershell
cd C:\path\to\KindleNotesAgent
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-KnodeVersion.ps1
```

If you bump manually, edit **`dotnet\Knode\Knode.csproj`** and **`dotnet\installer\Knode.iss`** together.

### A.3 Build (Release)

```powershell
cd C:\path\to\KindleNotesAgent
dotnet build .\dotnet\Knode.sln -c Release
```

### A.4 Installer (Inno)

Run the block that matches your **current directory**.

**From repo root** (`...\KindleNotesAgent`):

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1
# or, if ISCC.exe is not on PATH:
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

**From `dotnet` folder** (`...\KindleNotesAgent\dotnet`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
# or:
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Common mistake: if you are already in `dotnet`, do **not** prefix `.\dotnet\...` (that resolves to `dotnet\dotnet\...`).

### A.5 Smoke test: run UI from source (Release)

**From repo root**:

```powershell
dotnet run --project .\dotnet\Knode\Knode.csproj -c Release
```

**From `dotnet` folder**:

```powershell
dotnet run --project .\Knode\Knode.csproj -c Release
```

### A.6 Optional all-in-one local release helper

```powershell
cd C:\path\to\KindleNotesAgent
powershell -ExecutionPolicy Bypass -File .\scripts\Build-KnodeRelease.ps1
```

<h3 id="appendix-a7-commit-push">A.7 Commit + push `main`</h3>

```powershell
cd C:\path\to\KindleNotesAgent
git status
git add .
git commit -m "Release X.Y.Z: <short summary>"
git push origin main
```

<h3 id="appendix-a8-tag">A.8 Tag (after push)</h3>

```powershell
cd C:\path\to\KindleNotesAgent
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1 -WhatIf
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1
```

<h3 id="appendix-a9-docs">A.9 Documentation sync (Markdown + GitHub Pages HTML)</h3>

**When:** You changed **`docs/KNODE-MVP-GUIDE.md`**, **`KNODE-INSTALL.md`**, **`KNODE-FIRST-RUN.md`**, or **`KNODE-ARCHITECTURE.md`**, or you want published **`KNODE-*.html`** on Pages to match **`main`**.

**Step 1 — Review (no writes)**

```powershell
cd C:\path\to\KindleNotesAgent
git status
git diff --stat
git diff
```

Confirm **`appsettings.Local.json`**, **`corpus.jsonl`**, **`dotnet/**/bin/`**, **`dotnet/**/obj/`**, **`dotnet/dist-installer/`**, and other ignored artifacts are **not** listed under “Changes to be committed”. If `git add .` would pick them up, fix **`.gitignore`** or use a **narrow `git add`** (see A.7).

**Regenerate HTML from Markdown** (from repo root; one-time deps: `python -m pip install -r scripts/requirements-doc-html.txt`):

```powershell
cd C:\path\to\KindleNotesAgent
python scripts/build_doc_html.py
git status
git diff --stat docs/
```

**Step 2 — Commit + push** (example: doc-only follow-up after a feature release):

```powershell
cd C:\path\to\KindleNotesAgent
git add README.md docs/
git commit -m "Docs: OneNote install/first-run, hub, journey; SECURITY Phase 5 + A.9"
git push origin main
```

Adjust paths if you only changed a subset; **`git add docs/`** is usual for doc waves that include **`docs/index.html`** and **`docs/DESIGN.html`**.

---

<h2 id="appendix-b-github-ci">Appendix B — GitHub CI + where users download</h2>

### CI: **Knode installer** workflow

**`.github/workflows/knode-installer.yml`** runs on **push to `main`**, **`workflow_dispatch`**, and **tag `v*`**. It publishes **`dotnet/Knode`**, runs **Inno** on **`Knode.iss`**, uploads **`KnodeSetup-*`** as an **artifact**, and on **`v*`** tags **creates/updates a GitHub Release** and attaches the **`.exe`**.

### Where end users should download

**Not** from random folders in the repo browser. **No** checked-in **`KnodeSetup-*.exe`**.

Users should download from **[GitHub Releases](https://github.com/baskar-commits/KNode/releases)** → **Assets** on the release you intend them to use.

Product copy also points here: **[`KNODE-INSTALL.md`](KNODE-INSTALL.md)**.

**Workflow artifacts without a tag** are for debugging; the **supported** external install path is the **Release asset**.

---

<h2 id="appendix-c-python-cve">Appendix C — Python CVE context (maintainers)</h2>

### `transformers` / `sentence-transformers` (CVE context)

**`pip-audit`** may report **CVE-2026-1839** on **`transformers`** (affected builds are before **5.0.0rc3**) via **`sentence-transformers`**. This repo’s CLI is **inference-oriented**; do not point **untrusted** checkpoint paths at training APIs. **`sentence-transformers` 3.x** may pin **`transformers`** below **5.x** until upstream relaxes; mitigations include **`torch>=2.6`** in **`mvp/requirements.in`** and re-auditing in a clean venv. The **Knode Windows app** does not ship the Python **transformers** stack.

### GitHub Actions: security audit

**`.github/workflows/security-audit.yml`** runs on **push** and **pull_request** (**.NET** vulnerable packages + **`pip-audit`** on the same lockfiles). It may **ignore** a specific CVE on **`pip-audit`** until **`transformers` 5+** is compatible — see the workflow file; remove the ignore when fixed.

### Python lockfile (`mvp/`)

Edit **`mvp/requirements.in`**, then:

```powershell
cd mvp
python -m pip install pip-tools
pip-compile requirements.in -o requirements.txt
```

Commit **both** **`requirements.in`** and **`requirements.txt`**.

### Manual .NET (optional)

```powershell
cd dotnet
dotnet restore Knode.sln
dotnet list Knode.sln package --vulnerable --include-transitive
dotnet list Knode.sln package --outdated
```

---

<h2 id="appendix-d-secrets">Appendix D — Secret hygiene + local data</h2>

### API keys and GitHub

- **Never commit** real API keys. The repo ships **`appsettings.json`** with **empty** `Knode:Agent:ApiKey` and `Knode:Gemini:ApiKey`.
- Prefer **`appsettings.Local.json`** (gitignored) for machine-local values like **`Knode:OneNote:ClientId`**.
- Use env vars **`AGENT_API_KEY`**, **`GEMINI_API_KEY`**, **`GOOGLE_API_KEY`** for local dev.
- Knode can store a key with **Windows DPAPI** under `%LocalAppData%\Knode\`. That is **not** a substitute for full-disk encryption or an enterprise vault.

### Source vs installer

- **`git clone`** is **source** only; no production secrets in tree if you follow the rules above.
- **Release** **`KnodeSetup-x.y.z.exe`** contains **compiled** bits and the same default empty keys. Users still add their own keys at runtime.

### Corpus data

- **`corpus.jsonl`** is **private** reading data; it is **not** in the repo. See **[initial setup](KNODE-MVP-GUIDE.md#4-corpus-install-and-first-run)**.
- **`.gitignore`** ignores **`**/corpus.jsonl`** broadly; **`mvp/data/README.md`** is the tracked stub.

### OneNote connector data and local storage

- OneNote content used for retrieval is synced to local artifacts under **`%LocalAppData%\Knode\`** during Build index.
- Key files:
  - **`onenote_settings.json`**: selected section ids/labels, sync watermark (`LastSyncUtc`), build signature metadata.
  - **`onenote_records.json`**: local snapshot rows of synced OneNote page text/metadata.
  - **`index\records.json`** + **`index\vectors.bin`**: combined retrieval index rows/vectors (Kindle + selected OneNote).
  - **MSAL token cache** file (local app data): supports silent auth reuse between Connect / Select sections / Build index.
- At normal **Ask** time, Knode retrieves from local index; it does **not** require live Graph reads for each question.
- If you disable OneNote or change selected sections, rebuild index to align persisted retrieval state.

---

<h2 id="appendix-e-windows-trust">Appendix E — Windows trust + signing</h2>

Community builds are often **unsigned**. Expect:

- **SmartScreen** (“Unknown publisher”, “don’t run this often”) — users can use **More info → Run anyway** after trusting the **official Release** only. Details: **[`KNODE-INSTALL.md`](KNODE-INSTALL.md)**.
- **Edge / Defender** blocking download as **“virus detected”** — often **false positives** on new, unsigned EXEs. Check **Windows Security → Protection history**; optional **Microsoft false-positive submission**; publish **SHA256** on the release for integrity checks.

**Code signing (Authenticode)** improves trust and SmartScreen behavior; it is **not** a malware guarantee. **OSS projects** sometimes use **[SignPath Foundation](https://signpath.org/)** (free tier with eligibility rules) or **paid** certs.

| Layer | Role |
|--------|------|
| **Defender** (local scan) | Right-click **`KnodeSetup-*.exe`** → **Scan with Microsoft Defender**, or CLI: `"C:\Program Files\Windows Defender\MpCmdRun.exe" -Scan -ScanType 3 -File "C:\path\to\KnodeSetup-x.y.z.exe"`. |
| **VirusTotal** | Broad signal; **unsigned** builds often get noisy FP; use with care for unreleased bits. |
| **SHA256 in release notes** | `Get-FileHash -Algorithm SHA256`; publish hex for **integrity** (same bits); not authenticity. |
| **Signing** | Publisher identity + fewer warnings; still scan. |

---

## What this repo does **not** automate

- **BinSkim** / full **SDL** binary analysis.
- **Dependabot** / **GitHub Advanced Security** (enable per org policy).
- **SBOM** for the installer.
- Uploading every build to third-party multi-scanners (privacy vs coverage tradeoff).

---

## Public repo and license

**Public** GitHub + **MIT** (`LICENSE`) does **not** leak keys if you never commit them. Release tags must match **`<Version>`** / **`MyAppVersion`** on that commit so the **Knode installer** workflow and **`Push-KnodeReleaseTag.ps1`** stay coherent.
