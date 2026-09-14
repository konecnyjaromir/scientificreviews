# Scientific Reviews
![GitHub Release](https://img.shields.io/github/v/release/konecnyjaromir/scientificreviews?link=https%3A%2F%2Fgithub.com%2Fkonecnyjaromir%2Fscientificreviews%2Freleases%2Ftag%2Fv1.0.1) ![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/konecnyjaromir/scientificreviews/latest/total) ![Static Badge](https://img.shields.io/badge/_.NET-Windows-blue)

Scientific Reviews is a Windows desktop tool for researchers who work with BibTeX archives, systematic reviews, bibliometric screening, and Journal Citation Reports (JCR) enrichment. It is designed for fast record cleanup, bulk operations, PDF pairing, and high-throughput screening directly from a grid-based GUI.

## Key Features

### Project and archive workflow

- Open a BibTeX file or a whole folder as a new archive from `Project -> Open file` / `Open folder`
- Add more BibTeX files or folders into the current archive from `Project -> Add file` / `Add folder`
- Open a completely new application window from `Project -> New`
- Show the currently opened BibTeX file in the main window title
- Restore the latest autosave backup on startup when available

### Fast screening and editing

- Full-table search across rendered BibTeX content
- Multi-term filtering using comma-separated search terms
- Record editing through property grid and tag editor
- Bulk tag add/edit for selected records
- Remove a single tag or multiple tags from a record
- Generate standardized BibTeX keys from author + year
- Duplicate, delete, and bulk-clean records directly from the grid

### Clipboard and record transfer

- `Ctrl+C`, `Ctrl+X`, `Ctrl+V` support for whole BibTeX records
- Menu actions for Copy, Cut, and Paste records
- Right-click context menu in the main grid with `Edit`, `Copy`, `Cut`, `Paste`, and `Duplicate`
- Transfer records between different running application windows using the system clipboard

### PDF pairing and full-text workflow

- Open source PDF by double-clicking a record
- If no PDF is paired, prompt for manual pairing using a PDF file picker
- Store paired PDF information inside record tags:
  - `has_pdf`
  - `pdf_file`
  - `path_to_pdf`
- Automatic PDF pairing from `Pdf Folder` using these rules:
  - stored `path_to_pdf` / `pdf_file` if still valid
  - PDF file name contains BibTeX `key`
  - exact match between record title and PDF file name
  - similarity matching between standardized title and PDF name
- Configurable similarity threshold for automatic pairing
- Optional recursive PDF search
- Recursive search ignores folders whose name starts and ends with `__`, for example `__DELETED__`

### PDF export

- Dedicated `Export PDFs` dialog
- Export all records or only selected records
- Default output directory follows the currently opened BibTeX file location
- Optional DOI injection into exported PDF metadata
- Optional `Pack to folder` mode creates an `export` subfolder automatically
- File naming modes:
  - `Key`
  - `Key_Title`
  - `Custom` using placeholders like `<key>_<title>_<doi>`
- Export runs asynchronously with progress bar and cancel button

### JCR and metadata enrichment

- Update missing journals from Clarivate JCR API
- Generate JCR-derived tags such as `jif`, `jif_<year>`, and `jif_Q`
- Remove lower-ranked records, for example Q3 and Q4
- Remove duplicate tags while preserving the newest value

### Background operations and performance

- Status-strip operation manager for long-running tasks
- Parallel processing for expensive workflows such as:
  - BibTeX loading
  - JCR update
  - PDF auto-pair
  - PDF export
- Configurable global `Threads` setting, default `4`
- Indeterminate progress is used where total completion cannot be estimated in advance
- Automatic follow-up operations after open/add:
  - PDF auto-pair when `Pdf Folder` is configured
  - JCR update when API key is configured

### Data cleanup and export

- Remove duplicate records by title or DOI
- Remove records without DOI
- Exclude records by title pattern
- Exclude records using another BibTeX file
- Update page tag formatting
- Export as:
  - BibTeX
  - CSV
  - DOI list

## Settings

The application includes a settings dialog with the most important workflow parameters:

- `Pdf Folder`
- `Recursive PDF search`
- `PDF auto-pair threshold (%)`
- `Threads`
- `JCR Api key`
- backup settings

These settings influence both UI behavior and background processing.

## Typical Workflow

1. Open a `.bib` file or folder as a new archive.
2. Let the application automatically run PDF pairing and JCR update when configured.
3. Search, screen, tag, duplicate, or remove records in the main grid.
4. Open PDFs by double-click, or manually pair missing files.
5. Run cleanup tools for duplicates, missing DOI records, page normalization, and tag cleanup.
6. Export the final archive as BibTeX, CSV, DOI list, or export matched PDFs with the export dialog.

## System Requirements

- OS: Windows
- Framework: .NET Framework 4.8
- Input format: BibTeX (`.bib`)

Scientific Reviews is distributed as a portable Windows application and does not require a separate installer.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
