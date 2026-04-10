# Security and releases: Knode / KindleNotesAgent

## API keys and GitHub

- **Never commit** real API keys. The repository ships **`appsettings.json`** with **empty** `Knode:Agent:ApiKey` and `Knode:Gemini:ApiKey`.
- Use **`appsettings.Local.json`** (see root `.gitignore`) or environment variables **`AGENT_API_KEY`**, **`GEMINI_API_KEY`**, or **`GOOGLE_API_KEY`** for local development.
- Knode can **remember** a key on the PC using **Windows DPAPI** (`%LocalAppData%\Knode\agent_api_key.protected`). That protects against casual file snooping; it is **not** a substitute for device encryption or an enterprise secrets vault.

## Source vs installer

- **`git clone`** gives **source code** only. Anyone can read C# and configs; there are **no embedded production secrets** in the repo.
- **Release artifacts** (e.g. `KnodeSetup-x.y.z.exe` from Inno Setup) contain **compiled binaries** and a default `appsettings.json` with **empty** keys, same as the repo. Users still supply their own AI agent key at runtime.
- Shipping **only the `.exe` / installer** does **not** hide that the app calls Google’s APIs; it only hides implementation details. Treat the **key** like a password either way.

## Corpus data

- **`corpus.jsonl` is private reading data.** The GitHub repository **does not** include it; there is no bundled corpus to “update”; every install **creates or selects** their own file during **[initial setup](KNODE-MVP-GUIDE.md#4-corpus-install-and-first-run)** (Python pipeline and/or **Knode → Setup**).
- Root **`.gitignore`** ignores **`**/corpus.jsonl` everywhere** in the repo, not only under `mvp/data/`, so renamed paths still cannot be committed by mistake.
- Tracked stub only: **`mvp/data/README.md`** explains the folder; generated **`mvp/data/*`** stays local.

## GitHub Releases

- **Repository:** **public** source (code + docs under the **MIT** license in root `LICENSE`). That does **not** expose API keys if you never commit them.
- **Release asset:** **`KnodeSetup-x.y.z.exe`** from **Inno Setup** (same output as local **`dotnet/build-installer.ps1`**). End users install from the Release page; they do not need to clone or build.
- **Tag:** e.g. **`v0.2.1`** must match **`<Version>`** in **`dotnet/Knode/Knode.csproj`** and **`#define MyAppVersion`** in **`dotnet/installer/Knode.iss`** on the tagged commit.

**CI (step-by-step in Actions):** **`.github/workflows/knode-installer.yml`** runs on every push to **`main`**, on **`workflow_dispatch`**, and on tag pushes **`v*`**. It publishes **`dotnet/Knode`**, installs **Inno Setup** on the runner, compiles **`Knode.iss`**, uploads **`KnodeSetup-*`** as a **workflow artifact**, and on **`v*`** tags also creates a **GitHub Release** and attaches the **`.exe`**.

**Maintainer “single command” release** (after version bump is **committed** and pushed to **`main`**):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Push-KnodeReleaseTag.ps1
```

That creates and pushes **`vX.Y.Z`** from **`Knode.csproj`**; watch **Actions → Knode installer** for per-step logs. You can also run **`dotnet/build-installer.ps1`** locally for parity.

Packaging for Windows stays **Inno Setup** unless you later add another format.

### SmartScreen, signing, and trust

Community builds are often **unsigned** or not signed with a **widely trusted** Authenticode certificate. **Windows SmartScreen** may block or warn. This does **not** by itself mean the binary is malicious; users should download **only** from the **official** Release they expect and read **[`KNODE-INSTALL.md`](KNODE-INSTALL.md)**. The project does **not** claim a formal third-party security audit; reviewers may inspect **source** and build the installer themselves.

---

## Local build, validation, and GitHub (maintainers)

Use this section as the **single checklist** before you open the UI, before a release, and when pushing **GitHub Actions** builds. Commands below are written for **Windows PowerShell** and assume paths that match this repository layout.

**Repository root** is the folder that contains **`dotnet\`**, **`docs\`**, **`scripts\`**, and **`.github\`** (on your machine that may be `...\KindleNotesAgent`). Unless a step says otherwise, **open PowerShell, `cd` to that folder, then run the command**.

### Order of operations (recommended)

1. **Validate dependencies** (NuGet + Python lockfiles) from repo root.  
2. **Compile** the solution (`dotnet build`).  
3. **Optionally** build the **Inno installer** (needs Inno Setup locally).  
4. **Run the app** (`dotnet run`) or open the solution in Visual Studio.  
5. When ready to ship: **commit, push `main`**, then **push tag** so **Knode installer** on Actions can attach a Release.

**All-in-one (optional version bump + compile + installer):** from the repo root, run **`scripts\Build-KnodeRelease.ps1`**. It asks whether to run **`Bump-KnodeVersion.ps1`** first (then **minor** / **major** inside that script), then runs **`dotnet build`** on **`dotnet\Knode.sln`** and **`dotnet\build-installer.ps1`**. Use **`-SkipVersionBump`** when you only want to rebuild without changing the version. **GitHub Actions** and plain **`dotnet build`** do **not** run this script or prompt — that keeps CI and Visual Studio builds non-interactive.

### 1. Dependency checks (before UI or release)

**Directory: repository root.**

```powershell
cd C:\path\to\KindleNotesAgent
python -m pip install pip-audit
powershell -ExecutionPolicy Bypass -File .\scripts\security-audit.ps1
```

`security-audit.ps1` restores **`dotnet\Knode.sln`** and runs **`dotnet list … --vulnerable`**, then **`pip-audit`** on **`mvp\requirements.txt`** and **`spike\webview-notebook\requirements.txt`**. Fix any reported issues before treating the tree as release-ready.

### 2. Compile Knode (no need to open the UI yet)

**Directory: repository root** (preferred):

```powershell
cd C:\path\to\KindleNotesAgent
dotnet build .\dotnet\Knode.sln -c Release
```

**Directory: `dotnet`** (equivalent):

```powershell
cd C:\path\to\KindleNotesAgent\dotnet
dotnet build Knode.sln -c Release
```

A successful build confirms the **C# / WPF** project compiles. This matches the kind of compile CI exercises before packaging.

### 3. Optional: publish folder + `KnodeSetup-*.exe` (parity with CI)

**Directory: repository root.** Produces **`dotnet\publish\`** and, when Inno is available, **`dotnet\dist-installer\KnodeSetup-<version>.exe`**.

```powershell
cd C:\path\to\KindleNotesAgent
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1
```

If **`ISCC.exe`** is not on your **`PATH`** but Inno Setup 6 is installed:

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet\build-installer.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

If Inno is missing, the script still **publish**es to **`dotnet\publish\`** and warns; you can run **`Knode.exe`** from there for a **local** install-free test.

### 4. Run the Knode UI from the command line

**Directory: repository root.**

```powershell
cd C:\path\to\KindleNotesAgent
dotnet run --project .\dotnet\Knode\Knode.csproj -c Release
```

For the default (often Debug) configuration, omit `-c Release`. Ensure **`appsettings.Local.json`** or env vars supply a **Gemini** key if you will use **Build index** / **Ask**.

### 5. Push source to GitHub, then invoke **Knode installer** Actions

**Directory: repository root** (your git checkout).

1. Set the next **`<Version>`** and **`MyAppVersion`** (must match). The project uses **three-part** versions **Major.Minor.Patch** (e.g. **0.2.1**). Either run **`scripts\Bump-KnodeVersion.ps1`** — **minor** bumps the **patch** (`0.2.1` → `0.2.2`); **major** bumps the **middle** segment and sets patch to **0** (`0.2.1` → `0.3.0`) — or edit **`dotnet\Knode\Knode.csproj`** and **`dotnet\installer\Knode.iss`** by hand.

**Other ways to keep versions in sync:** (a) always use **`Bump-KnodeVersion.ps1`**; (b) single source of truth: only **`<Version>`** and generate the Inno line in a build step; (c) **MinVer** / **GitVersion** from git tags.

2. Commit and push **`main`** (this triggers **Security audit**, **Knode installer**, and other workflows on `push`):

```powershell
cd C:\path\to\KindleNotesAgent
git status
git add .\dotnet\Knode\Knode.csproj .\dotnet\installer\Knode.iss
git add .
git commit -m "Release x.y.z: bump app and installer version"
git push origin main
```

3. To create a **GitHub Release** and attach **`KnodeSetup-<version>.exe`**, push an annotated **tag** that matches `<Version>` (see **`scripts\Push-KnodeReleaseTag.ps1`**). **Directory: repository root.**

```powershell
cd C:\path\to\KindleNotesAgent
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1 -WhatIf
powershell -ExecutionPolicy Bypass -File .\scripts\Push-KnodeReleaseTag.ps1
```

The second command **creates and pushes** `v` + semver from the csproj. On GitHub: **Actions → Knode installer** for step-by-step logs; **Releases** for the downloadable **`.exe`**. You can also run **Knode installer** manually from the Actions tab (**Run workflow**) without a tag; that build uploads an **artifact** but does not create a Release.

---

## Dependency and release checks (maintainers)

Automated checks catch **known** vulnerable **library versions**. They do **not** prove the app has no logic bugs and are **not** a substitute for code review or a professional pentest.

### 1. Script (recommended before a release)

From the repo root (Windows PowerShell):

```powershell
python -m pip install pip-audit   # once per machine / venv
powershell -ExecutionPolicy Bypass -File scripts/security-audit.ps1
```

The script runs:

- **`dotnet list package --vulnerable --include-transitive`** on **`dotnet/Knode.sln`** (NuGet advisory data).
- **`pip-audit`** on **`mvp/requirements.txt`** and **`spike/webview-notebook/requirements.txt`** (PyPA/OSV).

Review output; fix by **upgrading packages** (or document accepted risk and mitigations).

### 2. Manual .NET commands

```powershell
cd dotnet
dotnet restore Knode.sln
dotnet list Knode.sln package --vulnerable --include-transitive
dotnet list Knode.sln package --outdated
```

Stay on the **same major** line as **`TargetFramework`** (e.g. **.NET 8** projects: prefer **8.x** patch updates for `Microsoft.Extensions.*` / `System.*` unless you intentionally retarget).

### 2b. Python lockfile (`mvp/`)

**`mvp/requirements.txt`** is **generated** from **`mvp/requirements.in`** using [**pip-tools**](https://pypi.org/project/pip-tools/). Edit only **`requirements.in`**, then regenerate:

```powershell
cd mvp
python -m pip install pip-tools
pip-compile requirements.in -o requirements.txt
```

Commit **both** **`requirements.in`** and **`requirements.txt`** so installs and **`pip-audit`** use the **same** resolved versions. (Optional: `--generate-hashes` on `pip-compile` for stricter supply-chain pinning when you need it.)

### 3. Malware scanning on the installer / EXE

Dependency scanning (**section 1**) answers **“are our libraries known-bad versions?”** It does **not** prove the **built binary** is free of malware or tampering. For **installer trust**, combine layers:

| Approach | What it does |
|----------|----------------|
| **Microsoft Defender (local)** | Right-click **`KnodeSetup-*.exe`** → **Scan with Microsoft Defender**, or run a **full** / **custom folder** scan on **`dotnet\dist-installer\`** and **`dotnet\publish\`**. This is your baseline on Windows. |
| **Defender CLI (automation)** | From an elevated or normal PowerShell: `"C:\Program Files\Windows Defender\MpCmdRun.exe" -Scan -ScanType 3 -File "C:\path\to\KnodeSetup-x.y.z.exe"` (`ScanType 3` = custom file). Exit codes indicate detections; see Microsoft docs for your Windows build. |
| **[VirusTotal](https://www.virustotal.com/)** (or similar) | Upload the **installer** (or published **folder** zipped). Multiple AV engines increase **breadth**; **cons:** you upload **your** build to a third party (sensitive if the binary is unreleased), and **new / unsigned** apps often get **false positives**. Use for **signal**, not a legal “clean bill of health.” |
| **Hash published with the release** | On the machine that **built** the artifact: `Get-FileHash -Algorithm SHA256 .\KnodeSetup-x.y.z.exe`. Publish that hex next to the download so others can **verify integrity** (same bits you intended), not authenticity (signing does that better). |
| **Code signing (Authenticode)** | A **trusted** certificate reduces SmartScreen warnings and ties the file to a **publisher identity**; it is **not** a malware scan but improves **user trust** and **tamper detection** (invalid signature after modification). |

**Reality check:** A **100% malware-free** guarantee requires **dedicated security operations** (and sometimes formal audit). For an MVP, **build only from your repo**, **scan with Defender**, **optionally VirusTotal**, **publish hashes**, and **sign** when you are ready to invest.

### 4. Python path (`mvp/`) and HuggingFace `transformers`

**`pip-audit`** may report **CVE-2026-1839** on **`transformers`** (&lt; **5.0.0rc3**), pulled in by **`sentence-transformers`**. That issue concerns **unsafe checkpoint loading** in **`Trainer`**. This repo’s CLI usage is **inference-oriented** (`ingest`, `query_cli`); **do not** point training or checkpoint-restore flows at **untrusted** model directories.

**`sentence-transformers` 3.x** currently requires **`transformers` &lt; 5**, so the advisory may remain **reported** until upstream relaxes that bound. Mitigations:

- Require **`torch>=2.6`** in **`mvp/requirements.in`** (compiled into **`requirements.txt`**; tighter **`torch.load`** behavior per vendor guidance for that class of issue).
- Re-run **`pip-audit`** after **`pip install -r requirements.txt`** in a clean venv before releases.
- Prefer the **Knode desktop** path for most testers (**no** Python **`transformers`** stack in the shipped Windows app).

### 5. GitHub Actions

Workflow **`.github/workflows/security-audit.yml`** runs on **push** and **pull_request**: **.NET** vulnerable-package list and **`pip-audit`** on **`mvp/requirements.txt`** and **`spike/webview-notebook/requirements.txt`**. It uses **`windows-latest`** so **`net8.0-windows`** restores. **`pip-audit`** on the MVP lockfile may temporarily ignore **CVE-2026-1839** until **`transformers` 5+** is compatible with **`sentence-transformers`** (see **`pip-audit --ignore-vuln`** in the workflow; **remove** that flag when fixed).

### 6. What this repo does **not** automate here

- **BinSkim** / **SDL**-style binary analysis (optional for larger release bars).
- **Dependabot** / **GitHub Advanced Security** (enable on the GitHub repo when you use it).
- **SBOM** generation for the installer (add when your distribution policy requires it).
- **Uploading every nightly build** to third-party multi-scanners (balance **privacy** vs coverage).
