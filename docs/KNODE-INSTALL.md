# Install Knode (Windows)

**Who this is for:** people installing the shipped **Inno Setup** installer (`KnodeSetup-*.exe`), for example from a **GitHub Release**.

**Developers** building from source: see [KNODE-MVP-GUIDE.md](KNODE-MVP-GUIDE.md) §4; open `dotnet/Knode/Knode.csproj` in Visual Studio or run `dotnet build` / `dotnet run` from `dotnet/Knode/`.

---

## Supported environment

| Item | Detail |
|------|--------|
| **OS** | **Windows 10** or **Windows 11**, **64-bit (x64)** |
| **Not supported** | macOS, Linux, Windows ARM32, 32-bit Windows (this build targets **win-x64**) |

---

## Prerequisites (install these before or with Knode)

1. **[Microsoft .NET 8 **Desktop** Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)**, **x64**.  
   The published installer uses **framework-dependent** deployment (`--self-contained false`). Without this runtime, Knode will not start.
2. **[WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)**: Usually already present on Windows 10/11. If answer/source panes stay blank, install the Evergreen runtime.

---

<h2 id="optional-onenote-personal-or-work-microsoft-account">Optional: OneNote (personal or work Microsoft account)</h2>

Knode can **merge selected OneNote sections** into the same local index as your Kindle **`corpus.jsonl`**. This is **off until you configure it**.

1. **Register an app** in the [Microsoft Entra admin center](https://entra.microsoft.com/) (or [Azure Portal](https://portal.azure.com/) → **Microsoft Entra ID** → **App registrations** → **New registration**).
   - **Supported account types:** include **personal Microsoft accounts** (Knode uses the “common” / work-or-personal audience).
   - **Redirect URI:** **Public client / native** → **`http://localhost`** (must match what the app uses).
   - **API permissions:** add **Microsoft Graph** delegated **`Notes.Read`** (and allow the sign-in flow to include **`offline_access`** for refresh).
2. Copy the application **(client) ID** and put it in **`appsettings.Local.json`** next to **`Knode.exe`** (same folder as the installed app, or next to the project when developing). Use the **`Knode:OneNote:ClientId`** setting — see tracked **`dotnet/Knode/appsettings.json`** for the shape. **`appsettings.Local.json` is gitignored**; do not paste the client ID into files you commit.
3. In the app, open **Setup · Corpus & index**, use **Connect OneNote** / **Select OneNote sections**, then **Build index** so Graph sync runs.

Local snapshot files, token cache, and keys: **[SECURITY-AND-RELEASES.md](SECURITY-AND-RELEASES.md)** (OneNote section).

---

## Get the installer

- **Recommended:** Open **[GitHub Releases](https://github.com/baskar-commits/KNode/releases)** for this repo, choose the **Latest** release, and download **`KnodeSetup-x.y.z.exe`** from **Assets** (the version in the filename matches the release tag). The artifact is built with [Inno Setup](https://jrsoftware.org/isinfo.php) via `dotnet/build-installer.ps1`).
- **Trust:** Install only from **official** links you expect (maintainer’s repo or Release). This project does **not** ship through the Microsoft Store in the MVP.

---

## Run the installer

1. Double-click **`KnodeSetup-*.exe`**.
2. Follow the wizard (install location, shortcuts as offered).
3. Launch **Knode** from the Start menu or desktop shortcut.

### Windows SmartScreen and code signing

Release builds may be **unsigned** or **not** use a **publicly trusted** Authenticode certificate (common for small OSS projects). Windows **SmartScreen** may show **“Windows protected your PC”** or similar.

- **This is normal** for many community builds, not a guarantee the app is harmful.
- If you choose **More info → Run anyway**, you are choosing to trust **this publisher and this download channel**. Prefer downloads **only** from the official Release you verified.
- **Code signing** (paid cert) reduces SmartScreen friction but is optional for MVP; the project makes **no** claim of formal third-party security audit. See [SECURITY-AND-RELEASES.md](SECURITY-AND-RELEASES.md).

---

## After install

Follow **[KNODE-FIRST-RUN.md](KNODE-FIRST-RUN.md)** (Kindle **`corpus.jsonl`**, optional OneNote setup, API key, **Build index**, first **Ask**).

---

## Building the installer yourself (maintainers)

On a machine with **.NET 8 SDK** and **Inno Setup 6** (`ISCC.exe` on PATH):

```powershell
powershell -ExecutionPolicy Bypass -File dotnet/build-installer.ps1
```

Output: **`dotnet/dist-installer/KnodeSetup-*.exe`** (version from `Knode.csproj` / `Knode.iss`).  
Published app binaries: **`dotnet/publish/`** (before Inno wraps them).

---

## Privacy and network (short)

Knode sends **your question** and **retrieved passage text** (Kindle and/or OneNote rows from the local index) to **Google Gemini** when you click **Ask**, and calls Gemini for **embeddings** during **Build index**. If you use OneNote, **Microsoft Graph** is called during **Connect** / section selection / **Build index** to sync pages you selected; routine **Ask** uses the **local index** only. Your **corpus file** and **local index** stay on your PC. Details: [SECURITY-AND-RELEASES.md](SECURITY-AND-RELEASES.md).
