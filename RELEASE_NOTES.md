# Release Notes

## Unreleased

This development cycle focused on turning Scientific Reviews into a faster multi-window workflow tool with stronger PDF handling, configurable preprocessing, safer save/close behavior, unified export workflows, and better background-operation control.

### Project Workflow

- Added `Project -> Open file` and `Open folder` to open a new archive after optional confirmation and clearing the current one
- Kept `Add file` and `Add folder` for incremental import into the current archive
- Added `Project -> New` to launch a separate application window
- Main window title now shows the active BibTeX file name from the current session
- Opening a new empty project no longer shows the previous file name in the window title
- Added `About` window with `Program` and `Project` tabs, including version info, GitHub link, and arXiv acknowledgement

### Save and Close Safety

- Split saving into:
  - `Save` for overwriting the currently opened BibTeX file
  - `Save As` for writing a new BibTeX file
- Added `Ctrl+S` for `Save`
- Added `Ctrl+Shift+S` for `Save As`
- `Save` now warns before overwriting unless `Allow unsafe saving` is enabled in settings
- Added `DatabaseChanged` tracking for unsaved BibTeX changes
- Closing the application now warns before losing unsaved work, with default action set to `No`
- Added `Allow unsafe closing` setting to bypass the unsaved-changes warning when desired

### Record Editing and Clipboard

- Added full-record clipboard support with `Ctrl+C`, `Ctrl+X`, and `Ctrl+V`
- Added `Ctrl+Shift+V` for raw paste without post-paste metadata fetch
- Added menu actions for `Copy`, `Cut`, and `Paste`
- Added `Ctrl+D` shortcut and menu/context action for `Duplicate`
- Added right-click context menu actions: `Edit`, `Copy`, `Cut`, `Paste`, `Duplicate`, `Rebind PDF`, and `Unbind PDF`
- Enabled record transfer between independent running application windows through the system clipboard
- Clipboard shortcuts are now context-aware:
  - text fields use normal text copy/cut/paste
  - the grid uses record copy/cut/paste
- Added `Paste Anything` smart parsing for grid paste:
  - BibTeX text is inserted as records
  - DOI-like text creates `@misc` stub records
  - web links create `@online` stub records
  - plain title-like text creates `@misc` title stubs
- Added canonicalization during smart paste for DOI and arXiv links
- Added `Enable Paste Anything` setting with default `True`
- Added `Paste Anything mode` setting:
  - `Simple`
  - `Auto`
  - `Deep`
- In `Auto` and `Deep`, pasted records can immediately trigger metadata fetch for just the newly inserted items
- Text-field paste behavior remains unchanged; smart paste only applies in the main grid context
- Added `Ctrl+E` for `Allow edit`
- Added `Ctrl+F` to focus the search box
- Added `Ctrl+R` for grid refresh
- Added `Ctrl+N` for `Project -> New`
- Added `Ctrl+O` for `Open file`
- Added `Ctrl+Shift+O` for `Open folder`

### Tags and Record Actions

- Added `Rename tag` for selected records from the `Database` menu
- Added `Rename tag` for the current record from the record panel and `Record` menu
- Rename-tag dialog now supports autocomplete and prefills the new value as `<original>_copy`
- Moved `Custom columns` management into `Settings` while keeping `Window -> Columns` as a familiar shortcut

### PDF Pairing and Full-Text Workflow

- Added `Auto-pair with PDFs`
- Added persistent PDF mapping tags:
  - `has_pdf`
  - `pdf_file`
  - `path_to_pdf`
- Added manual PDF pairing when no file is found
- Added `Rebind PDF` for the current record
- Added `Unbind PDF` for the current record, which clears the PDF mapping and sets `has_pdf = no`
- Added double-click on a record to open its paired PDF
- Unified PDF matching across open, auto-pair, and export
- Manual PDF rebind/attach now refreshes the grid while preserving selection and active sort order
- Added `Autoopening PDF when attach` setting with default `True`
- Current PDF matching rules:
  - use stored `path_to_pdf` / `pdf_file` when still valid
  - match when the PDF name contains the BibTeX `key`
  - match when the PDF name equals the normalized record title
  - match by title similarity using cosine-based scoring and keyword support
- Added configurable `PDF auto-pair threshold (%)`
- Added optional recursive PDF search
- Default recursive PDF search is now enabled
- Recursive search now ignores folders whose name starts and ends with `__`, for example `__DELETED__`

### PDF Export

- Replaced the old PDF export action with a dedicated `Export PDFs` dialog
- Added export mode for all records or only selected records
- Default export directory now follows the folder of the opened BibTeX file
- Added optional DOI and `eprint` metadata injection into exported PDFs
- Added `Pack to folder` option that creates an `export` subfolder automatically
- Added export file naming modes:
  - `Key`
  - `Key_Title`
  - `Custom` with placeholders such as `<key>_<title>_<doi>`
- Export now runs asynchronously and in parallel
- Added progress bar and cancel support to the export dialog
- Updated the `Export PDFs` dialog to match the newer export-form GUI style
- Switched PDF metadata injection to the iText library
- Added export preflight validation for source PDFs and destination paths
- Added detailed per-file PDF export logging
- Metadata injection failures no longer discard already exported PDFs
- PDF export now uses a dedicated temporary metadata workspace that is cleaned after export

### DOI and PDF Opening

- `Open using DOI` now detects DOI format automatically
- Standard DOI values such as `10.1145/3729343` open through `doi.org`
- Canonical arXiv DOI values such as `10.48550/arXiv.2310.08864` also open through `doi.org`
- Unsupported DOI-like values trigger a warning and then open through Google search

### Database Cleanup, Normalization, and Autofix

- Added `Normalize DOI`
- Added `Normalize page-tag`
- `Normalize page-tag` now reports results to the status label the same way as `Normalize DOI`
- Added `Create entry keys` with shortcut `Ctrl+Shift+K`
- Added `Fetch missing metadata` shortcut `Ctrl+Shift+M`
- Added `Auto-pair with PDFs` shortcut `Ctrl+Shift+P`
- Added manual `Autofix` with shortcut `Ctrl+Shift+A`
- `Autofix` now runs the deep cleanup pipeline:
  - Normalize DOI
  - Fetch missing metadata
  - Normalize page-tag
  - Create entry keys
  - Auto-pair PDFs
  - Update JCR
- `Normalize DOI` now preserves publisher DOI priority while also filling `eprint` when arXiv information is available

### Auto-Preprocessing

- Added `Auto-preprocessing mode` setting with:
  - `Off`
  - `Fast`
  - `Deep`
- Default auto-preprocessing mode is now `Fast`
- `Fast` preprocessing runs:
  - Normalize DOI
  - Normalize page-tag
  - Create entry keys
  - Auto-pair PDFs
- `Deep` preprocessing runs:
  - Normalize DOI
  - Fetch missing metadata
  - Normalize page-tag
  - Create entry keys
  - Auto-pair PDFs
  - Update JCR
- Automatic preprocessing now runs after opening BibTeX archives according to the selected mode

### Metadata Enrichment

- Metadata fetch now supports type-aware completeness rules:
  - scholarly records require article metadata such as `title`, `author`, `doi`, `abstract`, and `year`
  - `@online` records use web-oriented fields such as `title`, `url`, `urldate`, and `note`
- Added lightweight web metadata extraction from HTML/OpenGraph metadata as a final fallback provider
- URL lookup is now available after DOI/title matching when a record contains a usable `url`
- `Deep` smart-paste mode may accept DOI hints from explicit web metadata tags such as `citation_doi`
- Metadata fetch can now be targeted at a selected subset of records, which is used by smart paste enrichment

### Export and Output Workflows

- Unified database export into a single `Project -> Export` dialog
- Replaced separate visible/CSV/BibTeX export paths with one configurable export form
- Added export scope options:
  - `Visible`
  - `Selected`
  - `All`
- Added export format options:
  - `BibTeX (.bib)`
  - `CSV (.csv)`
- Added export mode options:
  - `Normal`
  - `As columns`
  - `As standard`
- Added configurable CSV separator options:
  - `,`
  - `;`
  - `TAB`
  - custom
- Added `Standard columns` setting for `As standard` export mode
- Added `Default CSV separator` setting
- Added `LastExportSettings` persistence so export choices are reused between openings
- Added `Ctrl+Shift+E` shortcut for database export
- Database export now runs modally from the export form with progress and cancel support
- Successful database export now closes the export form automatically

### Background Operations and Performance

- Added a status-strip operation manager for long-running tasks
- Added support for multiple parallel background operations in the main window
- Added shared `Threads` setting with default value `4`
- Multi-threaded operations now use the configured `Threads` value
- `Auto-pair with PDFs` now runs asynchronously and in parallel
- PDF export now runs asynchronously and in parallel
- Open/add operations now use continuous progress when completion percentage cannot be estimated reliably
- Added live status dialog when clicking a running background operation in the status strip
- Added `Stop` support for cancellable subtasks and their child processes from the status dialog
- Metadata fetch, JCR update, PDF auto-pair, archive loading, and preprocessing/autofix can now be cancelled cooperatively
- After open/add, the app can automatically trigger the configured preprocessing pipeline

### JCR and Database Cleanup

- JCR tag generation now updates existing tags instead of duplicating them
- Added `Remove duplicate tags` database action
- Duplicate-tag cleanup preserves the newest value for the same tag key
- Added `Normalize page-tag` into the `Autofix` pipeline
- Added database refresh usage across lightweight edit actions instead of unnecessary full reloads

### Refactoring

- Split `MainForm` into focused partial classes:
  - `MainForm.ArchiveActions.cs`
  - `MainForm.DatabaseActions.cs`
  - `MainForm.RecordInteractions.cs`
  - `MainForm.TagActions.cs`
  - `MainForm.PdfActions.cs`
- Extracted shared logic into helper services:
  - `BibtexLoadService`
  - `DatabaseExportService`
  - `JcrUpdateService`
  - `MetadataFetchService`
  - `PdfMatchingService`
  - `PdfExportService`
  - `BibtexTagService`
  - `StatusStripOperationManager`
- Removed empty event handlers and unused designer event hookups from `MainForm`
- Simplified duplicated logic around PDF handling, export, archive loading, and JCR updates
- Added shared preprocessing pipeline logic reused by both automatic preprocessing and manual `Autofix`

### Fixes and UX Improvements

- Fixed context menu positioning in the main grid so it opens at the actual click location
- Fixed `pdf_file` to store only the file name instead of a relative folder path
- Fixed menu typo `Add folfer` to `Add folder`
- Improved consistency between PDF pairing, PDF export, and manual PDF selection behavior
- Fixed record panel selection after light grid refresh so the current record stays focused
- Fixed text paste into search and editors so `Ctrl+V` no longer incorrectly pastes BibTeX records there
- Improved settings organization with grouped categories, unified naming, and descriptions
- Renamed `Update page tag format` to `Normalize page-tag`
- Fixed legacy `Recursive PDF search` defaults in persisted settings through settings migration
