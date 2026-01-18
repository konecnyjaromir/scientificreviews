# Scientific Reviews
![GitHub Release](https://img.shields.io/github/v/release/konecnyjaromir/scientificreviews?link=https%3A%2F%2Fgithub.com%2Fkonecnyjaromir%2Fscientificreviews%2Freleases%2Ftag%2Fv1.0.1) ![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/konecnyjaromir/scientificreviews/latest/total) ![Static Badge](https://img.shields.io/badge/_.NET-Windows-blue) 

---

Scientific Reviews is a specialized literature management tool designed to streamline the workflow of researchers conducting systematic reviews, meta-analyses, or general bibliometric studies. It focuses on speed, data hygiene, and Journal Citation Reports (JCR) integration to ensure high-quality source management.


## 🚀 Key Features

### 📥 Import & Merge

- **BibTeX Focused**: Specialized specifically for `.bib` file structures.  
- **Bulk Loading**: Import individual BibTeX files or load entire folders containing multiple sources at once.  
- **Database Fusion**: Seamlessly fuse multiple BibTeX files into a single master project, automatically handling the merger of diverse sources.  

### 🧹 Advanced Cleaning & Quality Control

- **JCR Integration**: Built-in integration with the Journal Citation Reports database.  
- **Quality Filtering**: Automatically filter sources based on impact:  
  - Remove journals based on Quartile scores (e.g., exclude Q3 and Q4 journals).  
  - Filter by Publisher Score.  
- **Automated Hygiene**:  
  - **Deduplication**: Remove duplicate entries instantly.  
  - **Clean Garbage Data**: Remove records missing tags or DOIs.  
- **One-Key Delete**: Rapidly discard irrelevant entries with a single keystroke for high-speed screening.  

### ⚡ Search, Tags & Organization

- **Advanced Search Engine**:  
  - Fast full-text search across metadata and abstracts.  
  - Support for **logical expressions** (e.g. `term1, term2`, `term1 OR term2`) for precise filtering of large libraries.  
- **Custom Tagging**: Create and apply custom tags to categorize citations (e.g., `Include`, `Exclude`, `Review Later`).  
- **Bulk Tag Management** (new in 1.0.1):  
  - Batch edit tags across multiple records at once.  
  - Quickly normalize, add, or remove thematic labels for entire subsets of your corpus.  
- **Smart Key Generation**: Automatically recreate and standardize BibTeX entry keys in the format `<author><year>` (e.g., `Smith2023`) to ensure consistency across your library.  
- **Full Editing**: Comprehensive editor allows modification of all BibTeX fields for any record.  

### 📄 Citation & Full-Text Integration

- **Automatic Source Lookup via Google**:  
  - Help resolve incomplete or malformed entries by searching for the original source on the web.  
- **Citation–PDF Pairing**:  
  - Link BibTeX entries with local PDF files to enable one-click opening of full-text documents from within the application.  

### 📤 Export

- **Flexible Output**: Export your curated library in multiple formats:  
  - **BibTeX**: For writing and citation managers.  
  - **CSV**: For data analysis and spreadsheets.  
  - **DOI List**: For quick reference fetching or external pipelines.  

---

## 🛠️ Getting Started

1. **Project Setup**: Launch the application and go to **Project** to create a new workspace.  
2. **Load Data**: Use the **Project** menu to import your `.bib` files or folders.  
3. **Clean & Filter**:  
   - Apply JCR filters to remove low-impact journals.  
   - Run deduplication and remove records with empty DOIs or missing essential fields.  
4. **Organize & Screen**:  
   - Use the **Advanced Search** with logical expressions to narrow down relevant records.  
   - Apply tags (individually or in bulk) and use One-Key Delete for rapid screening.  
5. **Connect Full Texts**:  
   - Use source lookup to repair incomplete citations.  
   - Pair entries with local PDFs for one-click access to full-text articles.  
6. **Export**: Select your final set and export to BibTeX, CSV, or DOI list according to your downstream workflow.  

---

## 🖥️ System Requirements

- **OS**: Windows (x64)  
- **Framework**: .NET Framework 4.8 (self-contained portable executable)  
- **Input Format**: BibTeX standard  

No installation is required; Scientific Reviews is distributed as a standalone portable EXE.

---

## 📄 License

This project is licensed under the **MIT License** – see the `LICENSE` file for details.
