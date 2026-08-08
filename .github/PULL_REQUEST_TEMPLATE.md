## What this changes

<!-- One or two sentences. Link the issue if there is one. -->

## Why

<!-- What problem does it solve? -->

## How it was tested

<!-- Commands run, cases covered, and anything you could not test. -->

## Checklist

- [ ] `dotnet build` and `dotnet test` both pass
- [ ] No new runtime dependency, or the PR explains why one is justified

If this touches `CipherDesk.Core`:

- [ ] `LegacyCompatibilityTests` still passes **and its golden vectors were not modified**
- [ ] Any format change bumps the version byte and keeps the old reader working
- [ ] New crypto paths have tests for round trip, wrong password, tampering and truncation

If this touches the UI:

- [ ] Checked in both light and dark themes
- [ ] Checked at 100%, 150% and 200% display scaling
- [ ] Colours come from `ThemePalette`, with no hard-coded `Color` values
- [ ] New controls have tooltips; new primary actions have keyboard shortcuts
- [ ] Screenshots attached below, both themes

<!-- Screenshots here -->
