---
name: tagNewRelease
description: Create a new git tag with release notes extracted from the changelog.
argument-hint: Optional version number to tag (e.g., v1.2.3); if not provided, uses the latest unreleased version from CHANGELOG.md
model: Claude Haiku 4.5 (copilot)
---
# Tag New Release

Create a new annotated git tag for a release based on the current project's changelog.

## Steps:
1. Check the most recent git tags to understand the versioning scheme
2. Read the CHANGELOG.md file to identify the latest unreleased version and its release notes, if they don't exist for the verison ceate them
3. Update the CHANGELOG.md file to mark the version as released
4. Verify the git working directory is clean (no uncommitted changes)
5. Create an annotated git tag with:
   - Tag name matching the version (e.g., v1.2.3)
   - Tag message containing the version and formatted release notes from the CHANGELOG
6. Confirm the tag was created successfully by showing the tag details
7. Optionally suggest pushing the tag to origin with `git push origin <tag-name>`

The release notes in the tag message should be cleanly formatted and include all sections (Added, Improved, Fixed, Changed, Deprecated, Removed, Security, etc.) from the CHANGELOG entry for that version.
