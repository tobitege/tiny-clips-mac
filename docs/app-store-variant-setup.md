# App Store Variant Setup

This repo now includes code paths for an `APPSTORE` build variant and Mac App Store-specific files:

- `TinyClips/Info-MAS.plist`
- `TinyClips/TinyClipsMAS.entitlements`

## What is already implemented in code

When `APPSTORE` is defined at compile time:

- Update UI is hidden in menu + settings.
- Save behavior is sandbox-safe:
  - Default save folders:
    - Screenshots + GIFs: `~/Pictures/TinyClips`
    - Videos: `~/Movies/TinyClips`
  - Optional custom save folder is selected via `NSOpenPanel` and persisted using a security-scoped bookmark.

Direct/non-App-Store behavior is unchanged.

## Xcode wiring steps

1. Duplicate the `TinyClips` target and name it `TinyClipsMAS`.
2. Set a distinct bundle identifier for `TinyClipsMAS`.
  - Current MAS bundle identifier in this repo: `com.refractored.tinyclips`
3. In `TinyClipsMAS` build settings:
   - `INFOPLIST_FILE = TinyClips/Info-MAS.plist`
   - `CODE_SIGN_ENTITLEMENTS = TinyClips/TinyClipsMAS.entitlements`
   - Add `APPSTORE` to **Swift Active Compilation Conditions**.
   - Enable App Sandbox (`ENABLE_APP_SANDBOX = YES`).
4. Remove Sparkle from the `TinyClipsMAS` target:
   - No Sparkle product in the MAS target's Frameworks/Link phase.
   - Keep Sparkle only in the direct distribution target.
5. Set signing/provisioning for Mac App Store distribution on `TinyClipsMAS`.

## Validation checklist

- `TinyClips` (direct):
  - Still shows “Check for Updates…”.
  - Save directory field remains editable path behavior.
- `TinyClipsMAS`:
  - No update UI.
  - Without custom folder, screenshots/GIFs save to Pictures and videos save to Movies.
  - Custom folder selection persists across relaunch and new captures save there.

## CI App Store pipeline

For fast validation on pull requests and branch pushes, use `.github/workflows/build-mas.yml` to compile the `TinyClipsMAS` scheme unsigned.

Use `.github/workflows/app-store.yml` to archive and upload the `TinyClipsMAS` build to App Store Connect.

### Xcode Cloud note

If Xcode Cloud has automatic package resolution disabled, commit `TinyClips.xcodeproj/project.xcworkspace/xcshareddata/swiftpm/Package.resolved` to the branch being built. Without it, builds fail during dependency resolution with exit code 74.

### Required GitHub secrets

- `APPLE_TEAM_ID`
- `APPSTORE_API_KEY_ID`
- `APPSTORE_API_ISSUER_ID`
- `APPSTORE_API_PRIVATE_KEY_BASE64` (base64 of `AuthKey_XXXXXX.p8`)
- `MAS_APP_DIST_CERT_BASE64` (base64 of App Store distribution `.p12`)
- `MAS_APP_DIST_CERT_PASSWORD`
- `MAS_INSTALLER_CERT_BASE64` (base64 of installer distribution `.p12`)
- `MAS_INSTALLER_CERT_PASSWORD`
- `MAS_KEYCHAIN_PASSWORD`
- `MAS_PROVISIONING_PROFILE_BASE64` (base64 of `.provisionprofile`)
- `MAS_PROVISIONING_PROFILE_SPECIFIER`

### Running the workflow

- Trigger `App Store` workflow manually with `workflow_dispatch`.
- Provide a semantic `version` (for `CFBundleShortVersionString`).
- The workflow sets build number from `github.run_number`, archives `TinyClipsMAS`, exports with `method=app-store`, and uploads the generated `.pkg` via `xcrun altool`.
