# OmsiStudio Epic Backlog

This document outlines the roadmap, priority groups, and status of OmsiStudio tasks.

---

## ✅ Completed Tasks

### Governance & Hygiene
*   **GOV-001 - Remove Non-Existent check.sh Requirement From Governance Docs**
    *   *Purpose*: Replaced references to missing `check.sh` with `dotnet build` / `dotnet test` validation.
    *   *Area*: `docs/AI_Governance/`
*   **GOV-003 - Initialize Git Repository and Add .gitignore**
    *   *Purpose*: Initialized Git locally and added a standard `.gitignore` to keep workspace clean.
    *   *Area*: Workspace root

### OS-001 - Scenery Object Browser Baseline
*   **OS-001-TASK-001 - Create Domain Models**
    *   *Purpose*: Formulated the core `OmsiAsset`, `OmsiAssetType`, `OmsiModelReference`, and `OmsiScanResult` models.
    *   *Area*: `OmsiStudio.Core`
*   **OS-001-FIX-001 - Align Task 1 Domain Models With Governance Review**
    *   *Purpose*: Cleaned up models (Unknown=0 default enum, property alignment) and documented inside `DOMAIN_MODEL.md`.
    *   *Area*: `OmsiStudio.Core`, `docs/domain/`
*   **OS-001-TASK-002 - Create OMSI Directory Scanner Service**
    *   *Purpose*: Extracted file discovery logic to a reusable filesystem directory scanner.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`
*   **OS-001-TASK-003 - Create SCO Parser Returning ScoFile Model**
    *   *Purpose*: Created a format-level `.sco` parser and mapped it defensively to handle comments and texture/mesh links.
    *   *Area*: `OmsiStudio.OmsiFormat`
*   **OS-001-TASK-004 - Wire Scanner Into MainWindowViewModel**
    *   *Purpose*: Wired the parser/scanner pipeline to the Avalonia UI using dependency injection and abstracted folder picking.
    *   *Area*: `OmsiStudio.App`

---

## 📋 Planned Backlog

### P0 - Temel Browser MVP
*   **OS-001-TASK-005 - Main UI Binding Cleanup**
    *   *Purpose*: Tidy up UI/XAML bindings and ensure responsiveness when displaying asset list filters.
    *   *Area*: `OmsiStudio.App` (Views/ViewModels)
    *   *Scope Creep Note*: Restrict purely to wiring cleanups. Avoid redesigning or restyling the UI layout.
*   **OS-001-TASK-006 - Scan Result Reporting**
    *   *Purpose*: Map parser warnings/errors into the `OmsiScanResult` structure and surface them in the UI status panel.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.App`
    *   *Scope Creep Note*: Surface counts and warning lists only; do not add automated error-fixing actions.
*   **OS-001-TASK-007 - Asset Grouping**
    *   *Purpose*: Implement grouping view of scenery objects by relative directories or parsed category names.
    *   *Area*: `OmsiStudio.App` (ViewModels/XAML)
    *   *Scope Creep Note*: Strictly focus on directory grouping lists; avoid complex tree view animations.
*   **OS-001-TASK-008 - Persist Last OMSI Root**
    *   *Purpose*: Save the last successfully scanned OMSI root path to local settings and reload it on startup.
    *   *Area*: `OmsiStudio.App`
    *   *Scope Creep Note*: Store path in a simple JSON or text config file; do not set up a database/registry configuration store.

### P1 - Parser Kalitesi
*   **OS-002-TASK-001 - Expand SCO Metadata Parsing**
    *   *Purpose*: Parse additional `.sco` tags (such as display names, localizations, or properties) recursively.
    *   *Area*: `OmsiStudio.OmsiFormat` (ScoParser)
    *   *Scope Creep Note*: Do not parse complex physics/script tokens. Keep to basic metadata strings.
*   **OS-002-TASK-002 - Encoding Support**
    *   *Purpose*: Support encodings (like system ANSI, windows-1252/1254) for correct character display in localized files.
    *   *Area*: `OmsiStudio.OmsiFormat` (ScoParser)
    *   *Scope Creep Note*: Only handle file loading encoding mappings; do not implement file rewriting.
*   **OS-002-TASK-003 - Parser Fixtures**
    *   *Purpose*: Add testing fixtures with pre-constructed `.sco` files representing different OMSI versions to avoid regression.
    *   *Area*: `OmsiStudio.OmsiFormat.Tests`
    *   *Scope Creep Note*: Keep to read-only resource-embedded files or simple strings.

### P1 - App Kullanılabilirliği
*   **OS-003-TASK-001 - Search and Filter Improvements**
    *   *Purpose*: Enhance search capability to match nested categories, asset tags, or multiple keywords.
    *   *Area*: `OmsiStudio.App` (ViewModels)
    *   *Scope Creep Note*: Avoid adding external indexing libraries; stick to standard LINQ filtering.
*   **OS-003-TASK-002 - Asset Detail Improvements**
    *   *Purpose*: Render detailed sections for categories, mesh paths, and textures in a tabbed panel.
    *   *Area*: `OmsiStudio.App` (Views/XAML)
    *   *Scope Creep Note*: Document texture names only; do not attempt to render or verify files on disk.
*   **OS-003-TASK-003 - Cancellation and Progress**
    *   *Purpose*: Allow users to cancel active directory scans and show a progress percentage in the UI.
    *   *Area*: `OmsiStudio.App`, `OmsiStudio.OmsiFormat` (Scanner)
    *   *Scope Creep Note*: Restrict to CancellationToken triggers and simple progress bars.

### P2 - Conversion Hazırlığı
*   **OS-004-TASK-001 - Conversion Domain Contracts**
    *   *Purpose*: Define contracts and models for file conversion operations (e.g. exporting scenery objects).
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.Conversion`
    *   *Scope Creep Note*: Define abstractions only; do not write actual conversion implementations.
*   **OS-004-TASK-002 - Export Manifest Prototype**
    *   *Purpose*: Draft a prototype manifest creator to track exported assets list.
    *   *Area*: `OmsiStudio.Conversion`
    *   *Scope Creep Note*: Prototype simple text/JSON manifest formats; do not connect to external packager utilities.

### P2 - 3D/O3D Ön Hazırlık
*   **OS-005-SPIKE-001 - O3D Format Research**
    *   *Purpose*: Perform format research regarding the binary `.o3d` structures to prepare for future reading.
    *   *Area*: `docs/spikes/`
    *   *Scope Creep Note*: Spiking only (writing technical logs/documentation); no parser code changes allowed.
*   **OS-005-TASK-001 - Model Reference Resolution**
    *   *Purpose*: Match referenced `.o3d` mesh names in `.sco` files against existing model subdirectories.
    *   *Area*: `OmsiStudio.OmsiFormat`
    *   *Scope Creep Note*: Resolve paths on disk; do not parse file contents or perform validation of binary mesh vertices.

### P3 - Governance / Project Hygiene
*   **GOV-002 - Split Test Projects**
    *   *Purpose*: Separate `OmsiStudio.OmsiFormat.Tests` into dedicated, separate test projects for Core, App, and Format layers.
    *   *Area*: Solution structure
    *   *Scope Creep Note*: Limit changes to moving test files and updating project configurations.
