# Changelog

All notable changes to this project will be documented in this file.

## v0.0.11 - 2026-02-15

### Added
- First-run onboarding wizard for permissions setup.
- Save notification preference in settings (default off).
- Reset all settings to defaults option for easier testing.

### Improved
- Onboarding welcome screen visuals with app icon and clearer guidance.
- Screen Recording step now includes explicit restart guidance.
- Added dedicated re-check action for Screen Recording permission status.

### Fixed
- Avoided potential QoS priority inversion in permission checking.
- Prevented duplicate popups during Screen Recording permission requests.
- Only mark onboarding complete when user explicitly finishes or dismisses.

### Maintenance
- Updated appcast for release metadata.

## v0.0.10 - 2026-02-14

### Added
- Mac App Store variant (`TinyClipsMAS`) from the same codebase.
- App Store-related documentation and project setup guidance.

### Improved
- Editor image handling and output flow refinements.
- Video trimming and timeline behavior improvements.
- Better main-thread handling around file panels and UI operations.

### Fixed
- Added `ITSAppUsesNonExemptEncryption` where required.
- Corrected plist path/signing-related project configuration issues.

### Maintenance
- Removed obsolete CI workflows and refreshed docs.

## v0.0.9 - 2026-02-14

### Added
- Countdown before Video and GIF recording.
- Release workflow step to generate changelog content.

### Improved
- Screenshot editor bottom bar layout and organization.

## v0.0.8 - 2026-02-13

### Added
- Screenshot format selection (PNG/JPEG), scale, and JPEG quality settings.
- Additional entitlement updates to support distribution/security requirements.

### Maintenance
- Updated appcast for release metadata.
