# Luma

<img src="docs/images/luma-logo.png" width="72" height="72" alt="Luma blue and purple spark logo" />

Clean, animated volume, media, brightness and lock-key flyouts for Windows 11.

**[Download for Windows](https://github.com/kyrerdm/Luma/releases/latest)** · [Türkçe](docs/README.tr.md) · [Deutsch / Español / Italiano quick start](docs/QUICKSTART.md)

**English is the default for new installations.** Türkçe, Deutsch, Español and Italiano are available under **General → Language**. Updating keeps your existing language choice.

![Luma volume bar and media card](docs/images/luma-motion.gif)

*Track names, artwork and player information in the examples are fictional.*

## Install

Requires **Windows 11 x64**. The required .NET Desktop Runtime is included; no separate .NET or Visual Studio installation is needed.

1. Open the [latest release](https://github.com/kyrerdm/Luma/releases/latest).
2. Download **`Luma-1.0.0-Setup.exe`** from **Assets** and run it.
3. Choose **Install → Open Luma**. Administrator access is not required.
4. Search for **Luma** in the Windows Start menu. Click its system tray icon once to open Settings.

Setup installs to `%LOCALAPPDATA%\Programs\Luma\1.0.0`, adds a Start menu shortcut and registers an entry in Windows Installed apps. The desktop shortcut is optional. **Start with Windows** is a separate preference under General.

For portable use, download **`Luma-1.0.0-win-x64.zip`**, extract it to a permanent folder and run `Luma.exe`. GitHub's **Source code** archives do not contain the runnable application.

## Update or uninstall

In an installed copy, choose **General → Check for updates → Download and update**, or run the new Setup. Downloads start only when requested. Luma checks the release asset's size and GitHub SHA-256 digest before running it. A failed startup restores the validated previous installation when available.

For portable updates, exit Luma from its tray menu, extract the new ZIP into a separate folder and open the new executable. An already-enabled startup entry follows the new path; a disabled one stays disabled.

Preferences live in `%LOCALAPPDATA%\LumaFlyouts\settings.json`. A successful save maintains `settings.json.bak`; a damaged primary can recover from the last valid copy. Recovery information is shown in Settings. Loading preferences does not rewrite them.

Uninstall through **Windows Settings → Apps → Installed apps → Luma**. The uninstaller removes Luma's installation and shortcuts while keeping personal preferences.

## Features

| Feature | What it does |
| --- | --- |
| Volume bar | Volume, mute and mouse-wheel adjustment in a compact animated strip. |
| Audio output and app mixer | Choose an active output and adjust the apps using that output separately. The arrow toggles the panel open or closed. |
| Media card | Track, artist, artwork, playback controls and seeking when supported by the player. Optional blurred artwork background. |
| Media source selection | Switch between the separate media sessions Windows exposes. Controls follow the selected source. |
| Brightness | Supported internal laptop displays, with optional DDC/CI controls for external monitors. |
| Lock-key indicators | Temporary Caps Lock, Num Lock and Scroll Lock notifications. |
| Placement | Four corners, top/bottom center, selectable monitor or follow-cursor placement. Scale from 75% to 140%. |
| Appearance | Dark, light or Windows theme; 12 accents, custom RGB color, opacity and animation preferences. |
| Live settings preview | Current volume and selected media. Read-only preview meters; the playback clock stops while hidden or minimized. |
| Languages | English, Türkçe, Deutsch, Español and Italiano, with live switching and saved preferences. |
| Daily use | Optional startup, settings import/export, local support reports and manual updates. |
| About | Developer credit, public project links and a selectable offline license reader. |

Settings sections support **Ctrl+1–6**. Use **Tab / Shift+Tab** to navigate. Closing Settings hides it to the tray; use the tray menu to exit Luma.

## Scope and known limitations

**1.0.0 is the first stable GitHub release for Windows 11 x64.** It is currently unsigned. There is no Microsoft Store or native ARM64 package yet. Physical hardware compatibility, long sessions and assistive-technology checks continue; the release label does not certify every device.

- Brightness depends on the laptop driver or external monitor's DDC/CI support. Some Windows/vendor brightness panels can still flash briefly before being hidden.
- Media information and commands depend on what each player exposes to Windows. Ordinary Chrome tabs may share one system media session and cannot then be selected independently. Separate Chrome/Spotify sessions can be selected.
- Apps appear in the mixer for the selected audio output. An app routed to another output may be absent. Multiple sessions belonging to the same app are adjusted together.
- Bluetooth/USB hotplug, sleep/resume, different laptop Fn controllers, external monitor writes, mixed DPI, Narrator and contrast themes require further real-device coverage.

Luma records no keystrokes and sends no telemetry. Everyday operation is offline. Only requested update checks/downloads contact GitHub. Support reports are saved locally and are never sent automatically.

## Build and verify

The SDK is pinned in `global.json`. From the repository root, with PowerShell and that .NET SDK installed:

```powershell
.\scripts\test-localization.ps1
.\scripts\build.ps1 -Publish -SelfContained
.\scripts\verify.ps1 -Published -TimeoutSeconds 240
.\scripts\package.ps1
.\scripts\test-package.ps1
```

The output is `artifacts\Luma-<project version>`. Setup and portable ZIP are placed in `artifacts`, alongside their `.sha256` files. Packaging verifies runtime notices against the restored runtime packages and includes the same licenses and guide as the installer.

`verify.ps1` runs Windows/WPF, audio, media, keyboard, settings recovery, update, localization and layout checks. Mouse-input fixtures target Luma's isolated test windows. These checks require an interactive Windows desktop; the GitHub workflow runs catalog validation, compilation and package integrity checks separately.

For isolated development preferences:

```powershell
.\artifacts\Luma-1.0.1\Luma.exe --data-dir .\.local\development-preferences
```

See [1.0 release notes](docs/releases/v1.0.0.md), [translation guidance](docs/LOCALIZATION.md), [multilingual quick start](docs/QUICKSTART.md) and [ongoing validation](docs/KARARLI_SURUM.md).

## License and credit

Microsoft Store preparation is tracked separately in the [submission guide](docs/store/SUBMISSION.md), with [listing drafts in five languages](docs/store/LISTING.md). The 1.0.1 MSIX candidate is not yet published or Store-certified; the public GitHub stable release remains 1.0.0. See the [privacy policy](docs/PRIVACY.md) for local data, optional update connections and support reports.

Designed and developed by **kyrerdm**. Luma's original code is licensed under [MIT](LICENSE); bundled components retain their own notices. License texts are available offline in **About** and in the distribution.

Luma is an independent project inspired by ModernFlyouts. It contains no ModernFlyouts code or artwork. The handwritten developer credit is separate from executable code signing.
