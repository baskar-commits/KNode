# Security and releases: Knode / KindleNotesAgent

This document is the **maintainer’s map** for going from **local changes** to a **GitHub Release** with a downloadable installer, and for **security** expectations around keys, binaries, and automation.

**Repository root** means the folder that contains **`dotnet\`**, **`docs\`**, **`scripts\`**, and **`.github\`**. Commands assume **Windows PowerShell** and `cd` to that root unless noted.

---

## End-to-end: from project update to Release

Follow this path when you are ready to ship a new **Knode** version.

| Phase | What you do | Outcome |
|--------|----------------|--------|
| **1. Audit** | Run **`scripts\security-audit.ps1`** (NuGet + Python). Fix issues. | Tree is dependency-clean enough to release. |
| **2. Version** | Run **`scripts\Bump-KnodeVersion.ps1`** (**minor** = patch +1; **major** = middle +1, patch 0), or edit **`dotnet\Knode\Knode.csproj`** (`<Version>`) and **`dotnet\installer\Knode.iss`** (`MyAppVersion`) so they **match** (`Major.Minor.Patch`). | Next semver is fixed in source. |
| **3. Build & package** | **`dotnet build .\dotnet\Knode.sln -c Release`** and optionally **`dotnet\build-installer.ps1`**, **or** one script: **`scripts\Build-KnodeRelease.ps1`** (optional bump + build + Inno). | **`dotnet\publish\`**, local **`KnodeSetup-x.y.z.exe`** in **`dotnet\dist-installer\`** when Inno is installed. |
| **4. Smoke test** | **`dotnet run --project .\dotnet\Knode\Knode.csproj -c Release`** or install the local **`KnodeSetup-*.exe`**. | Confirms the build runs before you tag. |
| **5. Push source** | **`git add`**, **`git commit`**, **`git push origin main`**. | **main** updates; **Security audit** and **Knode installer** workflows run on push. |
| **6. Tag → Release** | **`scripts\Push-KnodeReleaseTag.ps1`** (try **`-WhatIf`** first). | Script **reads `<Version>` from `Knode.csproj`**, checks **`Knode.iss`**, creates **`vX.Y.Z`**, pushes it. **Actions → Knode installer** builds; **Releases** gets **`KnodeSetup-X.Y.Z.exe`**. |

**You do not type the version again at tag time.** `Push-KnodeReleaseTag.ps1` takes the version from the **already committed** `.csproj` (and verifies `.iss`). Step 2 is the only place you set the semver for that release.

**All-in-one local script:** **`scripts\Build-KnodeRelease.ps1`** — optional interactive bump, then **`dotnet build`** and **`dotnet\build-installer.ps1`**. Flags: **`-SkipVersionBump`** (rebuild only), **`-BumpVersion`** (always run bump), **`-InnoPath "..."`** if **`ISCC.exe`** is not on `PATH`. Plain **`dotnet build`** and **CI** do **not** run this script (no prompts in automation).

---

## Where end users get the installer

**Not** a folder in the repo browser (`docs`, `dotnet`, etc.). **No** checked-in **`KnodeSetup-*.exe`**.

Users open **[GitHub Releases](https://github.com/baskar-commits/KNode/releases)** for this repo → download **`KnodeSetup-x.y.z.exe`** from **Assets** on the **Latest** (or chosen) release. (Forks: replace `OWNER/REPO` in that URL if you publish releases elsewhere.)

Product copy also points here: **[`KNODE-INSTALL.md`](KNODE-INSTALL.md)** (and the published HTML from the README).

**Actions → workflow artifact** without a tag is useful for debugging; the **supported** install path for outsiders is the **Release asset**.

---

## API keys and GitHub

- **Never commit** real API keys. The repo ships **`appsettings.json`** with **empty** `Knode:Agent:ApiKey` and `Knode:Gemini:ApiKey`.
- Use **`appsettings.Local.json`** (see root `.gitignore`) or **`AGENT_API_KEY`**, **`GEMINI_API_KEY`**, **`GOOGLE_API_KEY`** for local dev.
- Knode can store a key with **Windows DPAPI** under `%LocalAppData%\Knode\`. That is **not** a substitute for full-disk encryption or an enterprise vault.

## Source vs installer

- **`git clone`** is **source** only; no production secrets in tree if you follow the rules above.
- **Release** **`KnodeSetup-x.y.z.exe`** contains **compiled** bits and the same default empty keys. Users still add their own keys at runtime.
- Shipping **only** the installer does **not** hide that the app uses Google APIs; it hides **source**. Treat API keys like passwords.

## Corpus data

- **`corpus.jsonl`** is **private** reading data; it is **not** in the repo. See **[initial setup](KNODE-MVP-GUIDE.md#4-corpus-install-and-first-run)**.
- **`.gitignore`** ignores **`**/corpus.jsonl`** broadly; **`mvp/data/README.md`** is the tracked stub.

---

## Commands reference (same story, more detail)

### Dependency audit (before UI work or a release)

```powershell
cd C:\path\to\KindleNotesAgent
python -m pip install pip-audit
powershell -ExecutionPolicy Bypass -File .\scripts\security-audit.ps1
```

Restores **`dotnet\Knode.sln`**, runs **`dotnet list … --vulnerable`**, **`pip-audit`** on **`mvp\requirements.txt`** and **`spike\webview-notebook\requirements.txt`**. See **Deeper checks** below for manual **`dotnet list`**, **pip-tools**, and the **transformers** advisory.

### Compile only

```powershell
dotnet build .\dotnet\Knode.sln -c Release
```

### Installer locally (parity with CI)

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1
# or, if ISCC.exe is not on PATH:
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Produces **`dotnet\publish\`** and **`dotnet\dist-installer\KnodeSetup-x.y.z.exe`** when Inno is available; otherwise publish only plus a warning.

### Run the UI from source

```powershell
dotnet run --project .\dotnet\Knode\Knode.csproj -c Release
```

Omit **`-c Release`** for Debug. For **Build index** / **Ask**, configure **`appsettings.Local.json`** or env vars with a **Gemini** key.

### Commit, push **main**, then tag

After **`<Version>`** and **`MyAppVersion`** match and you are satisfied:

```powershell
git status
git add .\dotnet\Knode\Knode.csproj .\dotnet\installer\Knode.iss
git add .
git commit -m "Release 0.2.3: bump app and installer version"
git push origin main
```

Then:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1 -WhatIf
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1
```

**Other sync options** (not required today): generate Inno version from MSBuild; **MinVer** / **GitVersion** from tags only.

---

## CI: **Knode installer** workflow

**`.github/workflows/knode-installer.yml`** runs on **push to `main`**, **`workflow_dispatch`**, and **tag `v*`**. It publishes **`dotnet/Knode`**, runs **Inno** on **`Knode.iss`**, uploads **`KnodeSetup-*`** as an **artifact**, and on **`v*`** tags **creates/updates a GitHub Release** and attaches the **`.exe`**.

---

## Windows trust: SmartScreen, Defender, signing

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

## Deeper checks (maintainers)

Automated scans find **known-bad library versions**; they do **not** replace code review or a pentest.

### Manual .NET (optional)

```powershell
cd dotnet
dotnet restore Knode.sln
dotnet list Knode.sln package --vulnerable --include-transitive
dotnet list Knode.sln package --outdated
```

Prefer **.NET 8** patch lines for **`Microsoft.Extensions.*`** / **`System.*`** unless you intentionally retarget.

### Python lockfile (`mvp/`)

Edit **`mvp/requirements.in`**, then:

```powershell
cd mvp
python -m pip install pip-tools
pip-compile requirements.in -o requirements.txt
```

Commit **both** **`requirements.in`** and **`requirements.txt`**.

### `transformers` / `sentence-transformers` (CVE context)

**`pip-audit`** may report **CVE-2026-1839** on **`transformers`** (affected builds are before **5.0.0rc3**) via **`sentence-transformers`**. This repo’s CLI is **inference-oriented**; do not point **untrusted** checkpoint paths at training APIs. **`sentence-transformers` 3.x** may pin **`transformers`** below **5.x** until upstream relaxes; mitigations include **`torch>=2.6`** in **`requirements.in`** and re-auditing in a clean venv. The **Knode Windows app** does not ship the Python **transformers** stack.

### GitHub Actions: security audit

**`.github/workflows/security-audit.yml`** runs on **push** and **pull_request** (**.NET** vulnerable packages + **`pip-audit`** on the same lockfiles). It may **ignore** a specific CVE on **`pip-audit`** until **`transformers` 5+** is compatible — see the workflow file; remove the ignore when fixed.

### What this repo does **not** automate

- **BinSkim** / full **SDL** binary analysis.
- **Dependabot** / **GitHub Advanced Security** (enable per org policy).
- **SBOM** for the installer.
- Uploading every build to third-party multi-scanners (privacy vs coverage tradeoff).

---

## Public repo and license

**Public** GitHub + **MIT** (`LICENSE`) does **not** leak keys if you never commit them. Release tags must match **`<Version>`** / **`MyAppVersion`** on that commit so the **Knode installer** workflow and **`Push-KnodeReleaseTag.ps1`** stay coherent.
