# WebView spike: bulk capture from Kindle Notebook

This folder is a **manual but repeatable** way to pull text out of [Kindle Notebook](https://read.amazon.com/notebook) using an embedded browser (WebView2). You stay logged in as **you**; each book’s on-screen text is appended to a file, then the repo’s scripts **dedupe**, **parse** into **`corpus.jsonl`**, and **validate** shape for Knode.

**End state:** `mvp/data/corpus.jsonl` (one JSON object per highlight) ready for **Knode** or **`mvp/query_cli`**. Full product context: [`docs/KNODE-MVP-GUIDE.md`](../../docs/KNODE-MVP-GUIDE.md) §4.

---

## At a glance

| Step | What you run | Output |
|------|----------------|--------|
| 1 | Setup venv (once) | `.venv` with `pywebview` |
| 2 | `main.py` + in-app capture | `spike_extract.txt`, `spike_extract_meta.jsonl` |
| 3 | `dedupe_spike_extract.py` | `spike_extract_deduped.txt`, `dedupe_report.txt` |
| 4 | `parse_dump` + `validate_corpus` from `mvp/` | `mvp/data/corpus.jsonl` |

**Rule:** Always capture the **book detail** view (the page that shows **YOUR KINDLE NOTES FOR:** and highlights). The library grid alone is not enough.

---

## Part A: Prerequisites

Complete **before** the first capture.

1. **Windows 10 or 11 (x64).** This spike is written for WebView2 on Windows.
2. **Python 3.10 or newer** installed. Check in PowerShell: `python --version`. If missing or too old, install the current **Windows x86-64** installer from the official site: **[python.org/downloads](https://www.python.org/downloads/)** (during setup, enable **“Add python.exe to PATH”** if offered). Examples of OK versions: 3.11, 3.12, 3.13.
3. **Microsoft Edge WebView2 Runtime** (Evergreen). Most PCs already have it. If the app window never opens or errors, install from the [WebView2 download page](https://developer.microsoft.com/microsoft-edge/webview2/).
4. **Amazon account** with the library you want in Notebook. You will sign in inside the WebView.
5. **This project’s source tree** on your PC: the **KindleNotesAgent** folder you get from **`git clone`** or from **unzipping the source/repository archive** (it must contain `spike\webview-notebook\`). This is **not** the same as the folder where **`KnodeSetup-*.exe`** installs the desktop app under **Program Files**; the spike only exists in the **developer/source** copy of the project.

---

## Part B: One-time setup (venv)

**Installing `KnodeSetup-*.exe` does not include this folder:** you need the **KindleNotesAgent source** on disk first. After **B.1**, your path should contain `spike`, `mvp`, `dotnet`, etc. Use that path everywhere you see `C:\path\to\KindleNotesAgent` below (example: `C:\Users\you\Learn\KindleNotesAgent`).

### B.1 Get the source from GitHub (pick one)

**Option A: Git clone (good for updates)**

1. Install **Git for Windows** if you do not have it: [git-scm.com/download/win](https://git-scm.com/download/win).
2. Open **PowerShell** and go to the parent folder where you want the project (example: your `Learn` folder):

   ```powershell
   cd C:\Users\you\Documents\Projects
   ```

3. Clone the repository. Replace the URL with **your** GitHub repo (maintainer publishes the real link in the project README or invite):

   ```powershell
   git clone https://github.com/YOUR_ORG_OR_USER/KindleNotesAgent.git
   cd KindleNotesAgent
   ```

4. Confirm this works: `Test-Path spike\webview-notebook\main.py` should print **`True`**.

**Option B: Download ZIP (no Git)**

1. In the browser, open the project on **GitHub**.
2. Click the green **Code** button → **Download ZIP**.
3. Extract the ZIP. GitHub often creates a folder named **`KindleNotesAgent-main`** (or **`KindleNotesAgent-master`**). Rename it to **`KindleNotesAgent`** if you like, or remember the real folder name.
4. Your path to use below is that folder (the one that contains **`spike\webview-notebook\`**).

### B.2 Create the Python venv (in `spike\webview-notebook`)

Open **PowerShell** and run **exactly** these blocks (always use the venv’s Python, not plain `python`, so you never depend on `Activate.ps1`).

```powershell
cd C:\path\to\KindleNotesAgent\spike\webview-notebook

python -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

**Success:** `pip` finishes with no error. **Failure:** `No module named 'venv'` means install Python from [python.org](https://www.python.org/) and retry.

**Optional:** Double-click **`run.bat`** later only **after** `.venv` exists; `run.bat` runs `.\.venv\Scripts\python.exe main.py`.

---

## Part C: First successful capture (full library or a first batch)

1. **Start the spike** (same folder as above):

   ```powershell
   cd C:\path\to\KindleNotesAgent\spike\webview-notebook
   .\.venv\Scripts\python.exe main.py
   ```

2. A **desktop window** opens on Amazon. Complete **sign-in** until you see your **Notebook** (your books / highlights area).

3. **For each book you want exported:**
   - In the **left** (or main) list, **click one book** so the **reading-notes** view loads.
   - Wait until you see **highlights** (and ideally the line **YOUR KINDLE NOTES FOR:** in the page).
   - Click in the WebView so that book view is focused.
   - In the **terminal**, press **Enter** (empty line).  
     You should see a line like: `Appended capture #N: … characters → spike_extract.txt`.

4. **Repeat** step 3 for every book you care about.

5. When finished, type **`q`** and press **Enter** in the terminal to quit.

**First-time checklist**

- [ ] Terminal printed **non-zero** character counts for captures you care about.
- [ ] Folder `spike\webview-notebook\` now has **`spike_extract.txt`** (not empty).
- [ ] Open `spike_extract.txt` in a text editor and search for **YOUR KINDLE NOTES FOR:**. You should see it for captured books.

If a capture shows **0 characters** or login text, that book view was not ready: click the book again, wait for highlights, then press Enter again.

---

## Part D: Sanity-check the extract (before dedupe)

Do a **quick** pass (more detail in [`QUALITY-CHECKLIST.md`](QUALITY-CHECKLIST.md)):

1. Open **`spike_extract.txt`**.
2. Confirm you see blocks starting with **`==== CAPTURE #`**.
3. If there is **ORPHAN PREAMBLE** at the top (leftover from an old run), read [`QUALITY-CHECKLIST.md`](QUALITY-CHECKLIST.md) and trim or fix before dedupe.

---

## Part E: Dedupe (required before `parse_dump`)

Duplicate Enter presses for the same book create duplicate blocks. Collapse them:

```powershell
cd C:\path\to\KindleNotesAgent\spike\webview-notebook
.\.venv\Scripts\python.exe dedupe_spike_extract.py
```

**Outputs:**

- **`spike_extract_deduped.txt`** … input for the parser.
- **`dedupe_report.txt`** … how many duplicates were dropped.

**Success:** `dedupe_report.txt` exists and `spike_extract_deduped.txt` is smaller or equal in size versus many duplicate captures.

---

## Part F: Parse and validate (`corpus.jsonl`)

From the **`mvp`** folder, using **`mvp`** venv (create it once per [`mvp/README.md`](../../mvp/README.md) if you have not):

```powershell
cd C:\path\to\KindleNotesAgent\mvp
.\.venv\Scripts\python.exe -m kindle_agent.parse_dump
.\.venv\Scripts\python.exe -m kindle_agent.validate_corpus
```

- **`parse_dump`** reads **`spike\webview-notebook\spike_extract_deduped.txt`** by default and writes **`mvp\data\corpus.jsonl`**.
- **`validate_corpus`** exits with code **0** only if every line has a non-empty **`book_title`**. If it fails, fix the extract or parser and run again (see [`QUALITY-CHECKLIST.md`](QUALITY-CHECKLIST.md) and [`docs/KNODE-MVP-GUIDE.md`](../../docs/KNODE-MVP-GUIDE.md) §4).

**Success:** `mvp\data\corpus.jsonl` exists and `validate_corpus` exits **0**. Then open **Knode** and point **Setup** at this file ([`docs/KNODE-FIRST-RUN.md`](../../docs/KNODE-FIRST-RUN.md)).

---

## Part G: Incremental updates (new books later)

You **do not** delete `spike_extract.txt` for small additions.

1. Run **`main.py`** again and sign in if needed.
2. For **each new or updated book**, open the **detail** view (with highlights), then press **Enter** in the terminal to append.
3. Run **`dedupe_spike_extract.py`** again.
4. Run **`parse_dump`** and **`validate_corpus`** again (overwrites **`corpus.jsonl`** for default paths).

**Tip:** After dedupe, see what might still be missing vs your last corpus:

```powershell
cd C:\path\to\KindleNotesAgent\spike\webview-notebook
.\.venv\Scripts\python.exe notebook_coverage.py
```

It compares the **book index** visible in your extract against **`mvp\data\corpus.jsonl`** and lists titles that still need a proper **YOUR KINDLE NOTES FOR:** capture.

---

## Troubleshooting

| Symptom | What to try |
|--------|-------------|
| `No module named 'webview'` | You used global `python`. Use `.\.venv\Scripts\python.exe` from **`spike\webview-notebook`**. |
| Window does not open | Install [WebView2 Evergreen](https://developer.microsoft.com/microsoft-edge/webview2/). |
| `evaluate_js returned None` / tiny capture | Wait for the page to finish loading; click the book again; ensure you are on the **notes** view, not only the library. |
| `parse_dump` / `validate_corpus` fails | Open `spike_extract_deduped.txt` and confirm **YOUR KINDLE NOTES FOR:** sections look normal. See [`QUALITY-CHECKLIST.md`](QUALITY-CHECKLIST.md). |
| PowerShell blocks scripts | You do not need `Activate.ps1`. Always call `.\.venv\Scripts\python.exe` directly. |

---

## Privacy

**`spike_extract*.txt`** and **`spike_extract_meta*.jsonl`** contain **your reading**. They are **gitignored**; do not commit them.

---

## Design note (why this is manual)

There is **no** public Kindle Notebook API; this spike uses **your** session in a **controlled** window. A future product step might automate more of the UI, but today the reliable path is: **one book → Enter → repeat**.
