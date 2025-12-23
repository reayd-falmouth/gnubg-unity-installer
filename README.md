# GNUBG Unity Installer (UPM)
Unity Package Manager installer that downloads GNUBG binaries (Windows/macOS/Linux) from GitHub Releases and unpacks them into Assets/StreamingAssets/gnubg.
You don’t *have* to, but it’s the cleanest setup:

This repository contains a Unity Package Manager (UPM) package that installs GNU Backgammon (GNUBG) runtime binaries into a Unity project.

On install (or via menu), the package downloads prebuilt GNUBG bundles for **Windows / macOS / Linux** from a separate GitHub Releases repository, then unzips them into:

```

Assets/StreamingAssets/gnubg/
windows/
macos/
linux/

```

This layout is designed to support a Unity bridge that launches `gnubg-cli` from `StreamingAssets`.

---

## What this package does

- Fetches the **latest GitHub Release** metadata from a configured builds repo
- Downloads these assets:
  - `gnubg-windows.zip`
  - `gnubg-macos.zip`
  - `gnubg-linux.zip`
- Extracts each zip to `Assets/StreamingAssets/gnubg/<platform>/`
- Normalises the executable name to:
  - Windows: `gnubg-cli.exe`
  - macOS/Linux: `gnubg-cli`
- Ensures executable permissions on macOS/Linux (Editor-side)

---

## Requirements

- Unity 2021.3+ recommended
- Internet access during installation/update (Editor)
- A builds repository that publishes the GNUBG zip assets via GitHub Releases

---

## Install (Unity Package Manager)

Unity **Package Manager → Add package from git URL…**

Use:

```

[https://github.com/](https://github.com/)<OWNER>/<REPO>.git?path=/Packages/com.stonesanddice.gnubg.installer#v0.1.0

```

Replace `<OWNER>` and `<REPO>` with this repository.

---

## Configure the builds repository

The installer needs to know where to download GNUBG binaries from.

Edit:

```

Packages/com.stonesanddice.gnubg.installer/Editor/GnubgInstaller.cs

```

Set:

- `GitHubOwner`
- `GitHubRepo`

The builds repo must publish the release assets:

- `gnubg-windows.zip`
- `gnubg-macos.zip`
- `gnubg-linux.zip`

---

## Install / Update binaries

Menu:

**Tools → GNUBG → Install or Update Binaries**

This will download and unpack all platforms into `Assets/StreamingAssets/gnubg/`.

---

## Artefact layout expected inside each zip

Each zip should contain a single top-level folder named by platform, or just the contents directly. The installer can flatten one extra platform folder level if needed.

Recommended zip contents:

```

windows/
gnubg-cli.exe
*.dll
share/gnubg/...
lib/python3.12/...   (if embedding python)
macos/
gnubg-cli
share/gnubg/...
linux/
gnubg-cli
share/gnubg/...

```

---

## Notes for embedded Python (Windows)

If your GNUBG build embeds Python, ensure the Windows zip includes:

- `lib/python3.12/` (including `encodings/`)
- `lib/python312.zip` (if required by your build)

If your runtime sets `PYTHONHOME`/`PYTHONPATH`, do so relative to the extracted folder.
