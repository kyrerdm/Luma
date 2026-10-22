# Microsoft Store submission preparation

Status: **candidate, not published or certified**. GitHub release: 1.0.5. Store candidate is 1.0.5.0 (x64, Windows 11 22H2 / build 22621 or newer).

## Account and identity

The owner creates and verifies the developer account at <https://storedeveloper.microsoft.com/>. Account authentication, government-ID verification and acceptance of the developer agreement are completed by the owner.

Reserve the intended product name in Partner Center. Availability of the name **Luma** is not yet confirmed. If it is unavailable, choose a new Store display name with the owner; do not reserve an unrelated name silently.

Under the product's **Product management > Product identity**, obtain the exact `Package/Identity/Name`, `Package/Identity/Publisher` and `Package/Properties/PublisherDisplayName`. Copy `packaging/StoreIdentity.example.json` to `.local/StoreIdentity.json`, fill those values and set `DisplayName` to a reserved name. These identity fields are not passwords. Never put credentials, ID documents, private signing keys or access tokens in that file or the repository.

## Build and checks

From PowerShell 7 in an STA session:

```powershell
./scripts/test-localization.ps1
./scripts/build.ps1 -Publish -SelfContained
./scripts/verify.ps1 -Published -TimeoutSeconds 240
./scripts/package-store.ps1 -IdentityFile .local/StoreIdentity.json
./scripts/test-store-package.ps1 -Package artifacts/store/Store/Luma-1.0.5.0-Store-x64.msix -Production
```

`package-store.ps1` restores a pinned Microsoft Windows SDK BuildTools NuGet package, publishes the included .NET runtime, generates scale and target-size logos from the existing vector artwork, indexes assets with MakePri and validates the MSIX with MakeAppx. Seven license/guide notices are included. It never creates a certificate, trusts a certificate, changes Developer Mode, registers a package or submits to Microsoft Store.

The unsigned production MSIX is for Store ingestion; Windows cannot normally install it by double-click. Microsoft signs the approved Store package. A separate `-Development` build has a clearly different identity and the documented Windows 11 unsigned-testing OID and **must never be uploaded**. Installing executable content with `Add-AppxPackage -AllowUnsigned` requires administrator privileges and can affect all users; the preparation script does not install it or change trust/security settings. Schema, archive and isolated policy checks do not prove installation or hardware compatibility.

Before submitting, install a properly signed candidate through a supported test deployment, run Windows App Certification Kit, and test package identity at runtime: Start/search, tray, volume/mute and audio devices, media source/transport controls, internal brightness, supported DDC/CI displays, lock indicators, startup enable/disable, sleep/resume, monitors/DPI, settings recovery, update and uninstall. Use an isolated test environment if possible. Do not disable Windows security protections to make a package install.

## Moving from the GitHub installation

Both editions use the same single-instance mutex. A second foreground launch opens the running edition's settings; a background launch does not open settings. To test the Store edition, quit the existing GitHub edition from its tray first. The Store edition copies valid legacy preferences once into its own local data folder, including language, and leaves the original primary and backup unchanged. Existing Store preferences or backups are never overwritten by migration.

The startup choice belongs to each installation channel. It is not imported automatically. After the Store edition has been verified, disable startup in the old GitHub installation or uninstall that edition, then select the desired startup setting in the Store edition. Removing the old installation before copying/exporting preferences is unnecessary.

Store settings hide EXE install/repair and GitHub update actions. The updater itself also rejects checks/downloads under package identity. The opt-in manifest startup task uses Windows StartupTask APIs and `--background`; it is disabled by default and respects Windows policy/user-disabled states.

## Listing and owner choices

Use the five language drafts in `LISTING.md`. Suggested category: Utilities & tools. Intended initial pricing: Free; no ads, in-app purchases or subscriptions. The owner confirms markets, discoverability, release scheduling and questionnaire answers in Partner Center. Do not invent an age rating; complete the actual IARC questionnaire truthfully based on the app's behavior.

Support URL: <https://github.com/kyrerdm/Luma/issues>.
Website: <https://github.com/kyrerdm/Luma>.
Privacy URL after this document is merged/published: <https://github.com/kyrerdm/Luma/blob/main/docs/PRIVACY.md>. Verify it returns the published policy before submission; a local file or missing branch is not a public privacy URL.

Use the generated 300×300 tile and 1920×1080 screenshots. Screenshots must contain fictional sample media rather than the owner's real listening activity. Clearly label sample data. Do not claim Store certification or add a Store badge before the listing is actually live.

## Notes for certification (English)

Luma is a WPF desktop utility. It needs `runFullTrust` to use Win32/CoreAudio APIs for system and per-application volume, Windows media-session APIs for playback controls, WMI/DDC/CI for supported display brightness, a keyboard hook for volume-key handling and lock-key indicators, and a notification-area icon. It runs as the signed-in standard user and does not require administrator elevation, install a driver or Windows service, or collect typed text.

Launch Luma from Start. If onboarding appears, choose appearance/position and finish. Press a volume key to display its flyout. Single-click the tray icon to open settings; right-click the tray icon to exit. Start audio in a media-session-compatible player to test media controls. With multiple exposed Windows media sessions, use the source chooser to switch between them. Internal brightness needs a compatible laptop display; DDC/CI control is opt-in and requires a supported external monitor. Unsupported features show an unavailable state. Caps Lock/Num Lock/Scroll Lock indicators are temporary. A native Windows brightness flyout may briefly flash on some systems; Luma does not modify shell binaries or system security settings to suppress it.

The Store edition has no EXE self-updater or custom uninstaller; updates and removal are managed by Windows/Microsoft Store. Startup is opt-in through a manifest StartupTask. No app account, payment, subscription, advertising or developer-operated backend is required. Media content is supplied locally by other Windows applications, not hosted or streamed by Luma.

## Remaining release gates

- [ ] Verified owner account and reserved product identity.
- [ ] Production manifest and package match Partner Center exactly.
- [ ] Actual packaged install/update/uninstall and hardware tests recorded.
- [ ] Windows App Certification Kit run and report reviewed.
- [ ] Listing, privacy URL, screenshots and questionnaires reviewed against the real build.
- [ ] Submission accepted by Microsoft; public Store URL verified.

## Official references

- <https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer>
- <https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool>
- <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options>
- <https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/screenshots-and-images>
- <https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package>
