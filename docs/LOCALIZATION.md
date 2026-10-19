# Language support

New installations use **English**. The five UI choices are English (`en`), Türkçe (`tr`), Deutsch (`de`), Español (`es`) and Italiano (`it`). Existing saved choices survive updates. Legacy settings/backups without a language field keep their previous Turkish behavior. Invalid language codes fall back to English.

The embedded catalogs are in `src/Luma/Localization`. Turkish strings are the historical source keys; `en.json`, `de.json`, `es.json` and `it.json` each provide all 264 translations. This source-key convention does not change the English default.

To contribute a correction, update the matching value while keeping the exact key and every numbered placeholder, such as `{0}`. Do not translate product names, device/app/track names, file paths or license bodies. OS-provided diagnostic details may remain in the OS language. The support report uses stable technical English labels for issue diagnosis.

```powershell
.\scripts\test-localization.ps1
.\scripts\build.ps1 -Publish -SelfContained
.\scripts\verify.ps1 -Published -TimeoutSeconds 240
```

Catalog validation rejects duplicate/missing keys, empty translations and changed placeholders. UI tests cover repeated language switching, saved choices, preference backups, first-run English and legacy Turkish migration, native language names, media metadata, accessible names, installer captions and minimum-size layout. Each control retains its source key to avoid ambiguity when translations share a word. No translation service or network request is used at runtime.

Please include the selected language, section and current wording when reporting a translation issue. Wording feedback from native speakers is welcome.
