# Dev README — Auto Release (.NET x.y.z.b)

This repository uses a GitHub Actions workflow that **bumps the .NET 4-part version** (`x.y.z.b`) directly in:

- `src/Properties/AssemblyInfo.cs`

…and **creates a GitHub Release only when an explicit commit flag is present**.

---

## Versioning model

The workflow maintains a **4-part .NET version**:

- **`x.y.z.b`**
  - `x` = major
  - `y` = minor
  - `z` = patch
  - `b` = build (always increases when a release is triggered)

### Rules

When the workflow is triggered by a flagged commit:

1. **Only the flagged component is incremented** (`major` OR `minor` OR `patch` OR `build`).
2. **Build number `b` increments every time** a flag is present (including `(build)`).
3. If **no flag** is present, the workflow **skips bump + build + tag + release**.

> Note: the workflow also accepts the common typo `(path)` as `(patch)`.

---

## How to trigger a release

A release is triggered **only** if the latest commit message contains one of the flags below (case-insensitive):

- `(major)`
- `(minor)`
- `(patch)` 
- `(build)`

### Examples

| Commit message | Result |
|---|---|
| `(major) Breaking API update` | `x++`, `b++` |
| `(minor) Add new feature` | `y++`, `b++` |
| `(patch) Fix null reference` | `z++`, `b++` |
| `(build) CI pipeline adjustments` | `b++` only |
| `Refactor code` | **no release** |

---

## What the workflow does (high level)

If a release flag is found:

1. Reads the latest commit message and detects the bump flag.
2. Loads the current `AssemblyVersion("x.y.z.b")` from `AssemblyInfo.cs` (defaults to `1.0.0.0` if missing).
3. Computes the new version based on rules above.
4. Updates these attributes in `src/Properties/AssemblyInfo.cs`:
   - `AssemblyVersion`
   - `AssemblyFileVersion`
   - `AssemblyInformationalVersion`
5. Commits the bump as:
   - `chore: bump version to x.y.z.b`
6. Creates a git tag:
   - `v{x.y.z.b}`
7. Builds the project and zips output into `ScientificReviews.zip`.
8. Packs the build with Velopack (`vpk pack`, delta against the previous release when available).
9. Publishes a GitHub Release for the tag with the Velopack assets (`vpk upload github`), then sets the release notes and uploads `ScientificReviews.zip`.

If **no flag** is found:

- the workflow exits early and does **not** change anything.

---

## Output artifacts

- **Tag**: `v{x.y.z.b}` (pushed by the workflow on the version bump commit)
- **Release assets**:
  - `ScientificReviews-win-Setup.exe` - Velopack installer
  - `ScientificReviews-{x.y.b}-full.nupkg` (+ `-delta.nupkg` from the second Velopack release on), `releases.win.json`, `RELEASES`, `assets.win.json` - update feed read by the installed app
  - `ScientificReviews-win-Portable.zip` - Velopack portable (no installer, `Update.exe` + `current\`, updates itself, data in `%LocalAppData%` like the installed app)
  - `ScientificReviews.zip` - standalone build output, no automatic updates, data next to the executable

---

## Automatic updates (Velopack)

The installed application updates itself from GitHub Releases using [Velopack](https://velopack.io) (same setup as Betotech_Sampledroid).

- `src/Program.cs` - `VelopackApp.Build().Run()` must stay the **first** line of `Main` (install/update/uninstall hooks, the process may exit there). After `Application.Run` returns, a pending update confirmed by the user is applied and the app restarts.
- `src/Helpers/AppUpdater.cs` - `UpdateManager` + `GithubSource` for the public repository, no token, stable releases only.
- `src/Forms/MainForm.Updates.cs` - background check on startup (setting `Check for updates`, default on) and manual check from `About -> Check for updates`. After download the main window is closed through its normal path (unsaved-changes prompt, autosave, settings save), then `Program.Main` applies the update. If the close is cancelled, the update is installed on the next close; a downloaded package is also applied automatically on the next start.
- **Data folder**: the installed app keeps `settings.json`, `journals.json`, `logs` and the default `backup` folder in `%LocalAppData%\VSB-TUO\ScientificReviews` (`Program.GlobalPath`), because Velopack replaces the application folder on every update. The standalone zip and build output keep the data next to the executable.

### Installer version vs. assembly version

Velopack requires SemVer2 (3 parts), so the package version is **`x.y.b`** (major.minor.build), e.g. assembly `1.1.3.12` -> installer `1.1.12`. The build number `b` increases with every release and never resets, so update ordering is always correct (including `(build)`-only releases). The update dialog shows this installer version; `About` shows the full assembly version.

### Version of the Velopack tool

`VPK_VERSION` in `.github/workflows/release.yml` must match the `Velopack` package version in `src/packages.config`.

### Testing updates locally

Updates only work in the installed app (`UpdateManager.IsInstalled`); running from `bin\` or from the zip skips the check. To test: install an older release with Setup.exe, then publish a newer release (or run the installed app against a newer release that already exists).

---

## Release notes

If `RELEASE_NOTES.md` exists at repository root:

- the workflow uses its content as release body (`body_path`).

If the file is missing:

- it falls back to a short placeholder text and still enables `generate_release_notes: true`.

---

## Important notes & gotchas

### 1) Release triggers on the latest commit only
The workflow reads **only the most recent commit message** (`git log -1 --pretty=%B`).  
If you push multiple commits at once, make sure the **last** one contains the flag.

### 2) The workflow pushes back to the same branch
On a flagged commit, the workflow creates a new bump commit and pushes it back to:
- `origin HEAD:${{ github.ref_name }}`

This means:
- your branch will have an extra commit created by GitHub Actions
- tags are pushed automatically

### 3) Avoid infinite loops
Because the workflow pushes commits, it could re-trigger itself. It is designed to **avoid releasing again** because the bump commit does **not** include flags by default.

If you modify the bump commit message, keep it **without** `(major)/(minor)/(patch)/(build)`.

### 4) AssemblyInfo.cs must exist
The workflow fails if:
- a flag is found, but `src/Properties/AssemblyInfo.cs` is missing.

### 5) Version attribute pattern
The workflow expects this format inside `AssemblyInfo.cs`:

```csharp
[assembly: AssemblyVersion("1.2.3.4")]
```

If missing, it will append missing attributes at the end of the file.

---

## Quick checklist before releasing

- [ ] Make sure the **last commit** includes one flag: `(major)` / `(minor)` / `(patch)` / `(build)`
- [ ] Ensure `src/Properties/AssemblyInfo.cs` exists and contains valid version attributes
- [ ] (Optional) Update `RELEASE_NOTES.md` if you want curated release notes

---

## FAQ

**Q: Can I bump major and minor in one commit?**  
No. The workflow applies **exactly one** bump flag, with precedence: `major > minor > patch > build`.

**Q: Does `(build)` also increment build?**  
Yes. `(build)` increments **only** build, and build increments **every time a flag is present**.

**Q: What if both `(minor)` and `(patch)` appear?**  
The workflow applies the first match by precedence order: `major`, then `minor`, then `patch`, then `build`.

