# Scientific Reviews
![GitHub Release](https://img.shields.io/github/v/release/konecnyjaromir/scientificreviews?link=https%3A%2F%2Fgithub.com%2Fkonecnyjaromir%2Fscientificreviews%2Freleases%2Ftag%2Fv1.0.1) ![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/konecnyjaromir/scientificreviews/latest/total) ![Static Badge](https://img.shields.io/badge/_.NET-Windows-blue)

Scientific Reviews is a Windows desktop tool for researchers who work with BibTeX archives, systematic reviews, bibliometric screening, metadata cleanup, PDF pairing, and JCR enrichment. It focuses on fast batch operations over large record sets while keeping the workflow accessible through a grid-based desktop UI.

## Key Features

### Project and archive workflow

- Open a BibTeX file or a whole folder as a new archive from `Project -> Open file` / `Open folder`
- Add more BibTeX files or folders into the current archive from `Project -> Add file` / `Add folder`
- `Project -> Raw Mode` turns open/add file/folder actions into raw import without post-load preprocessing
- `Project -> Import Settings` lets you import a settings JSON from an older installation or version after an update
- `Pipelines -> Pipeline Builder` lets you create and store custom multi-step processing pipelines
- `Pipelines -> Run` lists saved custom pipelines dynamically for one-click execution
- `Pipelines` also contains the built-in `Autofix` action and `Autofix mode` selector
- Open a completely new application window from `Project -> New`
- Show the currently opened BibTeX file in the main window title
- Restore the latest autosave backup on startup when available
- Autosave the current state during long-running operations

### Fast screening and editing

- Smart search with field selectors, `AND` / `OR` / `NOT`, parentheses, and implicit `AND`
- Numeric smart-search filters such as `year:2020-2025`, `year>2025`, or `year>=2020`
- Classic full-table search across rendered BibTeX content remains available as a fallback mode
- Search mode toggle remembers the last selected `Smart search` / classic mode between application runs
- Record editing through the property grid and tag editor
- Bulk tag add/edit for selected records
- Remove selected tags from all selected records
- Rename selected tags in bulk or rename a single tag from the record panel
- Generate standardized BibTeX keys from author + year
- Duplicate, delete, and bulk-clean records directly from the grid
- Double-click a record to open its paired PDF
- Record menu and row context menu expose fast actions such as copy, duplicate, and `PDF Actions`
- Records can be flagged with `Green`, `Orange`, `Purple`, or `Red`, which stores a human-readable `flag` tag value and highlights the row in the grid
- `Record -> Flags` and the row context menu both support `No flag` plus quick flagging shortcuts `F3` to `F6`

### Search modes

The main search box supports two modes:

- `Smart search`
  - field-aware querying such as `title:"machine learning" AND author:novak`
  - boolean logic with `AND`, `OR`, `NOT`
  - grouping with parentheses, for example `(title:graph OR title:vision) AND year>=2022`
  - numeric ranges and comparisons for number-like tags, for example `year:2020-2025`, `year>2025`, `jif>=4`
- classic search
  - searches the whole rendered BibTeX record
  - comma-separated values act as `OR`

The `Smart search` checkbox next to the search box switches between these modes. Hovering over the search box shows a tooltip with example syntax for the active mode.

### Clipboard and record transfer

- `Ctrl+C`, `Ctrl+X`, `Ctrl+V` support for whole BibTeX records
- `Ctrl+Shift+V` support for raw paste without metadata fetch
- Menu actions for Copy, Cut, and Paste records
- Right-click context menu in the main grid with `Edit`, `Copy`, `Cut`, `Paste`, and `Duplicate`
- Transfer records between different running application windows using the system clipboard

### Paste Anything

The main grid supports a smart clipboard parser called `Paste Anything`.

- If the clipboard contains valid BibTeX text, the records are inserted directly
- If the clipboard contains DOI-like text, the application creates `@misc` stub records
- If the clipboard contains web links, the application creates `@online` stub records
- If the clipboard contains plain title-like text, the application creates `@misc` stub records with `title`
- DOI and arXiv links are canonicalized during paste, for example:
  - `https://doi.org/...` becomes a DOI-based record
  - `https://arxiv.org/abs/...` becomes a record with canonical arXiv DOI and `eprint`

`Ctrl+V` uses the currently configured `Paste Anything mode`. `Ctrl+Shift+V` always performs raw parsing only, without post-paste metadata fetch.

### Paste Anything modes

The feature can be configured in settings.

- `Enable Paste Anything`
  - enables or disables smart parsing of non-BibTeX text in the grid
- `Paste Anything mode`
  - `Simple`
    - parse and insert only
  - `Auto`
    - parse and then safely fetch missing metadata for the newly inserted records
  - `Deep`
    - parse and fetch metadata more aggressively, including DOI hints from supported web page metadata

The smart paste logic is context-aware:

- text fields keep normal system paste behavior
- the main grid uses record paste behavior
- BibTeX text is still accepted as the primary paste format and is not replaced by the smart parser

## Metadata Mechanism

Metadata enrichment is designed as a multi-provider pipeline with fallback behavior. The application does not depend on a single source.

- Primary providers:
  - Crossref REST API
  - Semantic Scholar Graph API
  - arXiv API
  - lightweight HTML / OpenGraph web metadata extraction
- Lookup strategy:
  - first by `doi`
  - fallback by `title`
  - final fallback by `url` for web-style `@online` records
- Supported DOI kinds:
  - standard publisher DOI
  - arXiv DOI in canonical form `10.48550/arXiv.<id>`
- Required metadata:
  - `title`
  - `author`
  - `doi`
  - `abstract`
  - `year`
- Optional metadata:
  - `eprint`
  - `journal`

### Metadata normalization rules

- `doi` is treated as the primary bibliographic identifier
- Publisher or journal DOI has higher priority than arXiv DOI
- `eprint` stores only the clean arXiv identifier, for example `2503.05231`
- `Normalize DOI`:
  - fixes DOI formatting
  - converts old raw arXiv identifiers to canonical DOI form
  - fills missing `doi` from `eprint` as `10.48550/arXiv.<eprint>`
  - never overwrites a valid publisher DOI with arXiv data

### Metadata fetch behavior

- `Database -> Fetch missing metadata` shows a warning before changing records
- The operation first runs DOI normalization, then starts metadata fetching
- URL-based metadata lookup is available for `@online` style records and other records with a usable `url`
- In `Deep` paste mode, web metadata lookup may also accept DOI hints from explicit metadata tags such as `citation_doi`
- The application can process records according to `Metadata Screen Mode`:
  - `Only missing`
  - `All`
  - `Only missing + arXive DOIs`
- `Autofix` and preprocessing use the selected built-in pipeline mode:
  - `Normal` respects the current metadata-fetch settings
  - `Deep` uses the deepest built-in metadata scope
- When an arXiv-based record is matched with a published version:
  - `doi` is upgraded to the publisher DOI
  - the arXiv identifier is preserved in `eprint`
- When a record has a publisher DOI and an arXiv version is found, the arXiv identifier is stored in `eprint`

This gives the archive a bibliographically correct DOI while still preserving arXiv information for future PDF acquisition and open-access workflows.

## Pipelines, Auto-Preprocessing, and Autofix

The application supports reusable multi-step pipelines for cleanup and enrichment workflows.

- `Pipelines -> Pipeline Builder` can create named custom pipelines from supported steps such as:
  - `Normalize DOI`
  - `Fetch metadata`
  - `Remove duplicates by title`
  - `Remove duplicates by DOI`
  - `Normalize page-tag`
  - `Create entry keys`
  - `Auto-pair PDFs`
  - `Autoupdate JCR`
- Custom pipelines are stored in settings and can be launched from `Pipelines -> Run`
- Pipeline execution uses the same task/report infrastructure as other long-running operations

Built-in preprocessing and Autofix are now backed by the same pipeline mechanism.

- `Auto-preprocessing mode` can be configured in settings:
  - `Off`
  - `Fast`
  - `Normal`
  - `Deep`
- `Autofix mode` can be configured independently:
  - `Off`
  - `Fast`
  - `Normal`
  - `Deep`
- Fast preprocessing:
  - Normalize DOI
  - Normalize page tags
  - Create entry keys
  - Auto-pair PDFs
- Normal preprocessing / Autofix:
  - Normalize DOI
  - Fetch metadata using the current metadata settings
  - Remove duplicates by title
  - Remove duplicates by DOI
  - Normalize page tags
  - Create entry keys
  - Auto-pair PDFs
  - Autoupdate JCR
- Deep preprocessing / Autofix:
  - Normalize DOI
  - Fetch metadata using the deepest built-in scope
  - Remove duplicates by title
  - Remove duplicates by DOI
  - Normalize page tags
  - Create entry keys
  - Auto-pair PDFs
  - Autoupdate JCR
- `Autoupdate JCR` runs as a parent task with visible child subtasks in the status strip

`Autofix` is a destructive workflow and shows a confirmation warning before execution because it can modify many records at once.

## PDF Pair Mechanism

Scientific Reviews can pair records with local PDFs and store the pairing inside BibTeX tags.

- Paired PDF-related tags:
  - `has_pdf`
  - `pdf_file`
  - `path_to_pdf`
- Double-click on a record opens the paired PDF
- If no PDF is paired yet, the application can prompt for manual pairing
- Manual PDF pairing updates the record in place and refreshes the grid while preserving the current sort and selection
- `Record -> Rebind PDF` lets the user pick a new PDF for the current record
- `Record -> Unbind PDF` removes `path_to_pdf` / `pdf_file` and sets `has_pdf = no`
- `Record -> PDF Actions` groups `Try autopair the PDF`, `Change PDF`, and `Unbind PDF` in one submenu
- `Try autopair the PDF` runs the automatic PDF matching logic only for the selected/current record instead of the whole archive
- The same `PDF Actions` submenu is also available from the row right-click menu

### Automatic PDF pairing

Auto-pair works against the configured `Pdf Folder` and can search recursively.

- Recursive search can be enabled or disabled in settings and is enabled by default
- Folders named like `__DELETED__` are ignored during recursive scans
- Stored `path_to_pdf` / `pdf_file` is reused when still valid
- If no stored path works, the application searches by filename match and then by similarity score

### Manual PDF attach behavior

- Manual attach and rebind use a standard file picker pointed to the most relevant known PDF folder
- Automatic opening of the selected PDF after manual attach can be controlled by the `Autoopening PDF when attach` setting
- This automatic opening is enabled by default

### PDF source match modes

The source filename matching behavior is configurable through `PDF source match mode`.

- `Title only`
  - direct and fuzzy matching use record `title`
- `Key only`
  - direct and fuzzy matching use BibTeX `key`
- `Key OR Title`
  - direct and fuzzy matching accept either `key` or `title`

Default mode is `Title only`.

### Open using DOI

- Records can be opened via DOI using [doi.org](https://doi.org)
- If DOI open is not possible, the application falls back to a Google search

## Export Features

Scientific Reviews includes multiple export workflows.

- Dedicated export form for BibTeX and CSV exports
- Separate dedicated export form for PDF export
- Save and export workflows support blocking tasks that temporarily lock the main window to protect the current archive from concurrent edits

### Export form

The export dialog lets you configure:

- Scope:
  - `Visible`
  - `Selected`
  - `All`
- Format:
  - `BibTeX (.bib)`
  - `CSV (.csv)`
- Export mode:
  - `Normal (all tags)`
  - `As columns`
  - `As standard`
- CSV separator:
  - comma
  - semicolon
  - tab
  - custom separator

`As columns` uses the `Custom columns` setting. `As standard` uses the `Standard columns` setting.
- Blocking save/export tasks are marked with `(blocking)` in the status strip while they are running

### PDF export

- Dedicated `Export PDFs` dialog
- Export all records or only selected records
- Default output directory follows the currently opened BibTeX file location
- Optional DOI and `eprint` injection into exported PDF metadata
- Optional `Pack to folder` mode creates an `export` subfolder automatically
- File naming modes:
  - `Key`
  - `Key_Title`
  - `Custom` using placeholders like `<key>_<title>_<doi>`
- Export runs asynchronously with progress bar and cancel button
- PDF export can also run as a blocking task, so the export stays asynchronous while edits in the main window are temporarily disabled
- PDF metadata injection is implemented through the open-source iText library
- Export performs source/destination validation before copying files
- Detailed export logging records skipped records, prepared jobs, successful exports, and per-file errors

## JCR and Additional Cleanup

- Update missing journals from Clarivate JCR API
- Generate JCR-derived tags such as `jif`, `jif_<year>`, and `jif_Q`
- `Update JCR` now reports which records were resolved, which still miss JCR tags, which records have no `journal`, and which records failed for another reason
- `Create extra JCR tags` now reports record-level success/failure details, including records without `journal`, unresolved journals, unusable JCR rank data, and other errors
- Remove lower-ranked records, for example Q3 and Q4
- `Remove Q3 Q4` is controlled by the `Low Quantile (Q3,Q4) Deleting Mode` setting:
  - `Only Records With Valid Jif Tags`
  - `All records`
- Remove duplicate records by title or DOI
- Remove duplicate tags while preserving the newest value
- `Database -> Clear flags` removes all `flag` tags from the current archive in one bulk action
- Remove records without DOI
- Exclude records by title pattern
- Exclude records using another BibTeX file
- Normalize page ranges through `Normalize page-tag`

## Logging and Background Operations

- Long-running tasks run through a status-strip operation manager
- The operation manager supports both regular background tasks and blocking tasks
- Blocking tasks lock the main window, suppress shortcuts, and prevent closing the main form until the operation completes, fails, or is cancelled
- `Autoupdate JCR` now exposes its internal `Update Journals Database` and `Create extra JCR tags` steps as visible subtasks in the status strip
- Process logging is written next to the executable into the `logs` folder
- Log files are text `.log` files separated by date
- Major workflows such as load, save, export, PDF auto-pair, metadata fetch, and JCR update are logged
- Parallel processing is used for expensive workflows such as BibTeX loading, JCR update, PDF auto-pair, PDF export, and metadata fetch
- Global `Threads` setting controls background worker count

## Settings

The settings dialog contains the main workflow switches and defaults, including:

- `PDF source folder`
- `Recursive PDF search`
- `Autoopening PDF when attach`
- `PDF auto-pair threshold (%)`
- `PDF source match mode`
- `Worker threads`
- `Auto-preprocessing mode`
- `Autofix mode`
- `Metadata contact email`
- `Metadata fetch scope`
- `Low Quantile (Q3,Q4) Deleting Mode`
- `JCR API key`
- `Default CSV separator`
- `Custom columns`
- `Standard columns`
- `Allow unsafe saving`
- `Allow unsafe closing`
- `Performance Optimization`
- backup settings

These settings affect both the UI and long-running background processes.

Additional clipboard and metadata-related settings include:

- `Enable Paste Anything`
- `Paste Anything mode`

### Notifications performance optimization

The notifications/report viewer uses a hybrid rendering strategy for very large reports.

- `Performance Optimization`
  - `Optimize For Performance`
    - switches large reports to async plain-text rendering at `50` estimated lines or `4000` characters
  - `Optimize For Quality / Performance ratio`
    - balanced default mode using async plain-text rendering at `150` estimated lines or `12000` characters
  - `Optimize For Quality (!)`
    - delays the optimization until `300` estimated lines or `24000` characters
  - `No optimization (not recommended)`
    - disables the async/plain-text optimization completely

When the optimization is triggered, Notifications first shows `Please wait, processing report for you...` and then loads the report as plain text to avoid UI freezes on very large change reports.

### Importing settings

- `Project -> Import Settings` can import a previously saved or copied `settings.json` file
- The import flow is designed for update scenarios where the user wants to restore settings from an older app version
- Imported settings are validated, normalized, and migrated to the current settings version before they become active
- The current active settings file is backed up automatically before replacement when one already exists
- If import validation fails, the current settings stay unchanged

## Typical Workflow

1. Open a `.bib` file or folder as a new archive.
2. Configure `PDF source folder`, matching mode, preprocessing level, smart paste mode, and metadata/JCR settings.
   After updating the application, use `Project -> Import Settings` if you want to restore settings from an older installation.
3. Import additional records by file, folder, or smart paste using `Ctrl+V` / `Ctrl+Shift+V`.
   Enable `Project -> Raw Mode` first when you want file/folder imports without automatic post-load preprocessing.
4. Run `Pipelines -> Autofix`, a custom pipeline from `Pipelines -> Run`, or individual tools such as `Normalize DOI`, `Fetch missing metadata`, `Auto-pair PDFs`, or `Update JCR`.
5. Screen, edit, tag, bind or rebind PDFs, and search records in the main grid.
   Use smart search for WoS-like field queries and numeric filters, or switch back to classic full-record search when needed.
6. Open PDFs by double-click or via DOI.
7. Export the final result as BibTeX, CSV, or matched PDFs.

## System Requirements

- OS: Windows
- Framework: .NET Framework 4.8
- Input format: BibTeX (`.bib`)

Scientific Reviews is distributed as a portable Windows application and does not require a separate installer.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
