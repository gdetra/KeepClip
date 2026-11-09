# Contributing to KeepClip

Thank you for helping improve KeepClip. For a larger feature or architectural change, open an issue before starting so the maintainer can agree on its scope.

## Branches and pull requests

- Start from the latest `main` branch.
- Use a short working branch such as `feat/image-search`, `fix/paste-focus`, `refactor/settings`, `docs/build-guide`, `test/clipboard`, or `ci/checks`. Maintainer automation may use `codex/<description>`.
- If you do not have repository write access, fork the repository and push your working branch to your fork.
- Open a pull request against `main`. Contributors must not push directly to `main`.
- Keep each PR focused on one change. Explain the problem, the resulting behavior, and how you verified it. Link the relevant issue when one exists.
- Request review when the change is ready. Address review comments and rerun the relevant checks after making changes.

The maintainer, [@gdetra](https://github.com/gdetra), may bypass the PR requirement. Other contributions require maintainer approval and passing CI before they are merged.

## Code conventions

- Follow `.editorconfig` and run the repository formatters before submitting.
- Put each top-level class, interface, record, or enum in its own file. Keep files and methods focused.
- Use four spaces for indentation, explicit access modifiers, and `_camelCase` for private fields. Indent parameters in long multiline signatures by four additional spaces.
- Pass service dependencies through constructors. Keep container resolution in the composition root.
- Keep application behavior in services and view models; use code-behind for view-specific interactions.
- Use `async Task` for asynchronous work, reserving `async void` for event handlers. Preserve cancellation and existing clipboard/session guards.
- Handle expected operational failures with actionable UI feedback. Propagate unexpected failures rather than reporting success.

## Validation

Build and test using the commands in the [README](README.md). Run the checks relevant to your change:

```powershell
./scripts/Format-Code.ps1 -Check
./scripts/Format-Markup.ps1 -Check
dotnet test tests/KeepClip.Tests -c Release --arch x64 --no-restore
dotnet test tests/KeepClip.Windows.Tests -c Release --arch x64 --no-restore
```

For changes affecting native interop or architecture-dependent behavior, also run the x86 tests when the matching runtime is available. CI runs both architectures.

Add or update tests for changed behavior. Prefer regression cases and observable outcomes over tests that repeat the implementation or framework behavior. Avoid duplicate tests for the same failure boundary.

For UI changes, include screenshots or reproducible manual verification steps. Use sample clipboard content in screenshots and bug reports.

For changes to packaging or release validation, run the relevant scripts:

```powershell
./scripts/Test-Versioning.ps1
./scripts/Test-StoreVersion.ps1
./scripts/Test-Release.ps1
```

## Commits and review

Use focused Conventional Commits, including the PR title when squash merging:

- `fix:` or `perf:` requests a patch increment, for example `1.0.0` to `1.0.1`.
- `feat:` requests a minor increment, for example `1.0.0` to `1.1.0`.
- A `!` after the type/scope, or a `BREAKING CHANGE:` footer, requests a major increment, for example `1.0.0` to `2.0.0`.
- Other types (`refactor:`, `style:`, `test:`, `docs:`, `ci:`, `build:`, `chore:`) use the default patch increment.

GitVersion calculates the next version from history and release tags. Multiple commits before a release use the highest requested increment; each development commit advances the build counter. Release tags fix the stable version. Keep the same conventions in merge and squash messages so the intent survives integration.

All review conversations must be resolved before merging. New changes may require another approval. Do not include generated build outputs, certificates, credentials, or unrelated edits.

Repository administrators can use the separate [branch protection configuration](.github/BRANCH_PROTECTION.md) to enforce this policy on GitHub.
