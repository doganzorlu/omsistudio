# OmsiStudio Epic Backlog

This document outlines the roadmap, priority groups, and status of OmsiStudio tasks.

---

## ✅ Completed Tasks

### Governance & Hygiene
*   **GOV-001 - Remove Non-Existent check.sh Requirement From Governance Docs**
    *   *Purpose*: Replaced references to missing `check.sh` with `dotnet build` / `dotnet test` validation.
    *   *Area*: `docs/AI_Governance/`
*   **GOV-002 - Split Test Projects**
    *   *Purpose*: Separated `OmsiStudio.OmsiFormat.Tests` into dedicated, separate test projects for App and Conversion layers to prevent dependency pollution.
    *   *Area*: Solution structure
*   **GOV-003 - Initialize Git Repository and Add .gitignore**
    *   *Purpose*: Initialized Git locally and added a standard `.gitignore` to keep workspace clean.
    *   *Area*: Workspace root
*   **GOV-004 - Create Project README and Developer Runbook**
    *   *Purpose*: Created a comprehensive root `README.md` defining project purpose, architecture layers, build/test/run commands, test structure, manifest export details, and known governance.
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
*   **OS-001-TASK-005 - Main UI Binding Cleanup**
    *   *Purpose*: Tidied up UI/XAML bindings and ensured responsiveness when displaying asset list filters.
    *   *Area*: `OmsiStudio.App` (Views/ViewModels)
*   **OS-001-TASK-006 - Scan Result Reporting**
    *   *Purpose*: Mapped parser warnings/errors into the `OmsiScanResult` structure and surfaced them in the UI status panel.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.App`
*   **OS-001-TASK-007 - Asset Grouping**
    *   *Purpose*: Implemented grouping view of scenery objects by relative directories or parsed category names.
    *   *Area*: `OmsiStudio.App` (ViewModels/XAML)
*   **OS-001-TASK-008 - Persist Last OMSI Root**
    *   *Purpose*: Saved the last successfully scanned OMSI root path to local settings and reloaded it on startup.
    *   *Area*: `OmsiStudio.App`

### OS-002 - SCO Parser Encoding & Fixtures
*   **OS-002-TASK-001 - Expand SCO Metadata Parsing**
    *   *Purpose*: Parsed additional `.sco` tags (such as sound, script, collision mesh references, etc.) recursively.
    *   *Area*: `OmsiStudio.OmsiFormat` (ScoParser)
*   **OS-002-TASK-002 - Encoding Support**
    *   *Purpose*: Supported encodings (like system ANSI, windows-1252/1254) for correct character display in localized files.
    *   *Area*: `OmsiStudio.OmsiFormat` (ScoParser)
*   **OS-002-TASK-003 - Parser Fixtures**
    *   *Purpose*: Added testing fixtures with pre-constructed `.sco` files representing different OMSI versions to avoid regression.
    *   *Area*: `OmsiStudio.OmsiFormat.Tests`

### App Usability, Localization & Error Handling
*   **OS-003-TASK-001 - Search and Filter Improvements**
    *   *Purpose*: Enhanced search capability to match nested categories, asset tags, description, path, or mesh references with multi-token support.
    *   *Area*: `OmsiStudio.App`
*   **OS-003-TASK-004 - Add Turkish/English Localization Support**
    *   *Purpose*: Added localization service for dynamic language switching (TR/EN) in XAML/ViewModel.
    *   *Area*: `OmsiStudio.App`
*   **OS-003-TASK-002B - Complete Asset Detail Metadata Display**
    *   *Purpose*: Extracted and displayed texture references in the scenery object detail panel.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`, `OmsiStudio.App`
*   **OS-003-FIX-003 - Finish Asset Detail Command Error Propagation**
    *   *Purpose*: Resolved launcher service exceptions swallowing and wired meaningful error reporting back to the UI.
    *   *Area*: `OmsiStudio.App`
*   **OS-003-TASK-003 - Cancellation and Progress**
    *   *Purpose*: Allowed users to cancel active directory scans and show dynamic progress info in the UI.
    *   *Area*: `OmsiStudio.App`, `OmsiStudio.OmsiFormat`

### OS-004 - Conversion Domain & Manifest Preparation
*   **OS-004-TASK-001 - Conversion Domain Contracts**
    *   *Purpose*: Formulated target formats, request, and result contracts and implemented a placeholder conversion service.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.Conversion`
*   **OS-004-FIX-001 - Validate Conversion Output Directory Contract**
    *   *Purpose*: Implemented validation rules to require target output directory to be non-empty and a fully qualified absolute path.
    *   *Area*: `OmsiStudio.Conversion`
*   **OS-004-TASK-002 - Create Export Manifest Prototype**
    *   *Purpose*: Drafted a prototype manifest creator models, contracts, and serializers to track exported assets list.
    *   *Area*: `OmsiStudio.Core`, `OmsiStudio.Conversion`

### OS-005 - 3D/O3D Format Research & Reference Resolution
*   **OS-005-SPIKE-001 - O3D Format Research**
    *   *Purpose*: Performed format research regarding the binary `.o3d` structures to prepare for future reading and documented findings.
    *   *Area*: `docs/spikes/`
*   **OS-005-TASK-001 - Model Reference Resolution**
    *   *Purpose*: Match referenced `.o3d` mesh names in `.sco` files against existing model subdirectories and track missing/invalid file resolution.
    *   *Area*: `OmsiStudio.OmsiFormat`, `OmsiStudio.Core`

### OS-006 - OMSI Asset Converter MVP
*   **OS-006-TASK-001 - Add Manifest-Only Export for Selected Asset**
    *   *Purpose*: Implement folder picking, manifest conversion, JSON serialization, and deterministic file saving to support manifest-only exports.
    *   *Area*: `OmsiStudio.App`, `OmsiStudio.Conversion`
*   **OS-006-FIX-001 - Stabilize Manifest Export UI Bindings and Test Cleanup**
    *   *Purpose*: Fixed Avalonia UI data binding refresh by adding missing `NotifyPropertyChangedFor` triggers, and refactored conversion tests to clean up temporary output files.
    *   *Area*: `OmsiStudio.App`, `OmsiStudio.Conversion.Tests`

---

## 📋 Planned Backlog

### P1 - App Kullanılabilirliği
*(No pending tasks)*

### P2 - Conversion Hazırlığı
*(No pending tasks)*


### P2 - 3D/O3D Ön Hazırlık
*(No pending tasks)*

### P3 - Governance / Project Hygiene
*(No pending tasks)*

