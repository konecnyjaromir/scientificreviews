# Release Notes

## Unreleased

This development cycle focused on turning Scientific Reviews into a faster multi-window workflow tool with stronger PDF handling, better background processing, and a much cleaner internal architecture.

### Project Workflow

- Added `Project -> Open file` and `Open folder` to open a new archive after optional confirmation and clearing the current one
- Kept `Add file` and `Add folder` for incremental import into the current archive
- Added `Project -> New` to launch a separate application window
- Main window title now shows the active BibTeX file name
- Added `LastBibTex` tracking for default export behavior and UI context

### Record Editing and Clipboard

- Added full-record clipboard support with `Ctrl+C`, `Ctrl+X`, and `Ctrl+V`
- Added menu actions for `Copy`, `Cut`, and `Paste`
- Added right-click context menu actions: `Edit`, `Copy`, `Cut`, `Paste`, and `Duplicate`
- Enabled record transfer between independent running application windows through the system clipboard

### PDF Pairing and Full-Text Workflow

- Added `Auto-pair with PDFs`
- Added persistent PDF mapping tags:
  - `has_pdf`
  - `pdf_file`
  - `path_to_pdf`
- Added manual PDF pairing when no file is found
- Added double-click on a record to open its paired PDF
- Unified PDF matching across open, auto-pair, and export
- Current PDF matching rules:
  - use stored `path_to_pdf` / `pdf_file` when still valid
  - match when the PDF name contains the BibTeX `key`
  - match when the PDF name equals the normalized record title
  - match by title similarity using cosine-based scoring and keyword support
- Added configurable `PDF auto-pair threshold (%)`
- Added optional recursive PDF search
- Recursive search now ignores folders whose name starts and ends with `__`, for example `__DELETED__`

### PDF Export

- Replaced the old PDF export action with a dedicated `Export PDFs` dialog
- Added export mode for all records or only selected records
- Default export directory now follows the folder of the opened BibTeX file
- Added optional DOI metadata injection into exported PDFs
- Added `Pack to folder` option that creates an `export` subfolder automatically
- Added export file naming modes:
  - `Key`
  - `Key_Title`
  - `Custom` with placeholders such as `<key>_<title>_<doi>`
- Export now runs asynchronously and in parallel
- Added progress bar and cancel support to the export dialog

### DOI and PDF Opening

- `Open using DOI` now detects DOI format automatically
- Standard DOI values such as `10.1145/3729343` open through `doi.org`
- arXiv identifiers such as `2310.08864` open through `arxiv.org/pdf/...`
- Unsupported DOI-like values trigger a warning and then open through Google search

### Background Operations and Performance

- Added a status-strip operation manager for long-running tasks
- Added support for multiple parallel background operations in the main window
- Added shared `Threads` setting with default value `4`
- Multi-threaded operations now use the configured `Threads` value
- `Auto-pair with PDFs` now runs asynchronously and in parallel
- PDF export now runs asynchronously and in parallel
- Open/add operations now use continuous progress when completion percentage cannot be estimated reliably
- After open/add, the app can automatically trigger:
  - PDF auto-pair
  - JCR update when API key is configured

### JCR and Database Cleanup

- JCR tag generation now updates existing tags instead of duplicating them
- Added `Remove duplicate tags` database action
- Duplicate-tag cleanup preserves the newest value for the same tag key

### Refactoring

- Split `MainForm` into focused partial classes:
  - `MainForm.ArchiveActions.cs`
  - `MainForm.DatabaseActions.cs`
  - `MainForm.RecordInteractions.cs`
  - `MainForm.TagActions.cs`
  - `MainForm.PdfActions.cs`
- Extracted shared logic into helper services:
  - `BibtexLoadService`
  - `JcrUpdateService`
  - `PdfMatchingService`
  - `PdfExportService`
  - `BibtexTagService`
  - `StatusStripOperationManager`
- Removed empty event handlers and unused designer event hookups from `MainForm`
- Simplified duplicated logic around PDF handling, export, archive loading, and JCR updates

### Fixes and UX Improvements

- Fixed context menu positioning in the main grid so it opens at the actual click location
- Fixed `pdf_file` to store only the file name instead of a relative folder path
- Fixed menu typo `Add folfer` to `Add folder`
- Improved consistency between PDF pairing, PDF export, and manual PDF selection behavior
