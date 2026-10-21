# Luma

<img src="docs/images/luma-logo.png" width="72" height="72" alt="Luma logo" />

Free, open-source volume, media and brightness flyouts for Windows 11.

**[Download for Windows](https://github.com/kyrerdm/Luma/releases/latest)** · [Türkçe](docs/README.tr.md) · [Deutsch / Español / Italiano](docs/QUICKSTART.md)

![Luma volume, media and settings](docs/store/images/01-overview.png)

*Screenshots use fictional media and device information.*

## Features

- System volume, mute, audio output selection and per-application volume controls.
- Media artwork, playback controls, seeking and source selection when supported by the player.
- Brightness controls for supported laptop displays and optional DDC/CI external monitors.
- Temporary Caps Lock, Num Lock and Scroll Lock indicators.
- Six positions, adjustable size and monitor selection.
- Light, dark and system themes, 12 accent colors and a custom color option.
- Live settings preview, preference backup and optional startup with Windows.
- Quiet update indicators and optional automatic release checks in the GitHub edition.
- English, Turkish, German, Spanish and Italian. New installations start in English; updates keep your language choice.

## Install

Requires **Windows 11 x64**. The .NET runtime is included.

1. Open the [latest release](https://github.com/kyrerdm/Luma/releases/latest) and download the file ending in **`-Setup.exe`** under **Assets**.
2. Run it and choose **Install → Open Luma**. Administrator access is not required.
3. Search for **Luma** in Start. Click its tray icon once to open Settings.

For portable use, download the **`-win-x64.zip`** asset, extract it to a permanent folder and open `Luma.exe`. The **Source code** archives contain source, not the runnable app. GitHub builds are currently unsigned.

## Use, update and uninstall

Press a volume key to display Luma. Standard keyboard play/pause, next, previous and stop keys also show the media card when enabled and a player exposes a Windows media session; the keys keep their normal playback action. The arrow on the volume bar opens and closes the audio output/app mixer panel; clicking outside also closes it. The media source arrow switches between the sessions Windows exposes. The pin keeps the volume/media card visible; lock indicators remain temporary.

Settings close to the tray. Right-click the tray icon and choose **Exit** to quit. Change language under **General → Language** and startup under **General**.

The GitHub edition checks for updates shortly after startup and every six hours by default. A small green dot on the tray icon and an **Update available** badge in Settings lead to the update section. Turn off automatic checks under **General**, or use **Check for updates** manually. **Download and update** starts the download and installation; neither starts automatically.

You can also run the latest Setup. Quit an old copy before opening a different installation or portable version. Uninstall through **Windows Settings → Apps → Installed apps**. GitHub preferences remain in `%LOCALAPPDATA%\LumaFlyouts` for later use; preference import/export is available in Settings.

## Compatibility

- Brightness depends on the display and drivers. The native Windows brightness indicator can briefly appear on some devices.
- Media support depends on the player. Browser tabs may share one Windows media session and cannot then be selected separately.
- The mixer shows audio sessions on the selected output; sessions belonging to one app are adjusted together.
- Native ARM64 packages are not available. Broader hardware, sleep/resume and accessibility validation is tracked in the [validation notes](docs/KARARLI_SURUM.md).

Audio, media and brightness features operate locally. Automatic/manual release checks and requested update downloads use GitHub. Luma records no typed text or telemetry, and support reports are saved locally. [Privacy policy](docs/PRIVACY.md).

## Microsoft Store

Store packaging, listing text and screenshots are prepared. **Luma is not yet published on Microsoft Store.** The owner must connect the real publisher identity and complete packaged installation and Windows certification checks before submission. Start with the [Store publishing guide](docs/store/BASLA-BURADAN.md).

## Development and license

[Build and verify](docs/DEVELOPMENT.md) · [Translations](docs/LOCALIZATION.md) · [Release notes](https://github.com/kyrerdm/Luma/releases) · [Report an issue](https://github.com/kyrerdm/Luma/issues)

Designed and developed by **kyrerdm**. Licensed under [MIT](LICENSE); bundled components retain their own notices, available offline in About. Luma is an independent project inspired by ModernFlyouts and contains no ModernFlyouts code or artwork.
