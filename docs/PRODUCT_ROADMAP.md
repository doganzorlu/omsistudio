# OmsiStudio Product Roadmap

This document outlines the product direction, architecture modules, and long-term roadmap for OmsiStudio. It groups features into core modular structures to keep development scoped, structured, and decoupled.

---

## 🗺️ Product Roadmap Modules

### 1. SCO Viewer

*   **Goal**: Provide a comprehensive inspection and preview utility for OMSI scenery objects (`.sco`), allowing developers to browse metadata, verify assets references, and render meshes in 3D.
*   **Primary Workflows**:
    *   OMSI root directory scanning and file categorization.
    *   Metadata tag extraction (sound, script, variables, collision boundaries, texture maps).
    *   Model reference resolution mapping.
    *   Interactive 3D software-rendered mesh previews with camera controls and material color swatches.
*   **Core File Formats**: `.sco`, `.o3d`, `.bmp`, `.png`, `.jpg`, `.dds`, `.tga`
*   **Dependencies**: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`
*   **Strict Exclusions for Current Phase**: Editing geometry, exporting/saving `.sco` files, and dynamic scene graph editing.
*   **Suggested Epic IDs**: `OS-001` to `OS-003` (SCO Viewer Baseline), `OS-005` (Model Reference Resolution), `OS-006` (O3D Metadata), `OS-007` (O3D Geometry), `OS-008` (Asset Preview System), `OS-009` (Realistic Preview System).

---

### 2. O3D Converter

*   **Status**: Active/Planned Implementation Phase (linked with [O3D Converter Epic](backlog/O3D_CONVERTER_EPIC.md))
*   **Goal**: Provide conversion, serialization, and generation utilities for OMSI binary `.o3d` mesh models to transition them to modern 3D formats (and vice-versa).
*   **Primary Workflows**:
    *   Exporting asset manifests to structured JSON format.
    *   Converting binary `.o3d` file geometries into standard open formats (e.g. glTF, OBJ, FBX).
    *   Importing/converting modern format geometries back into binary `.o3d` files.
    *   Texture format transcoding (e.g. TGA/BMP to DDS).
*   **Core File Formats**: `.o3d`, `.json` (Manifests), `.gltf`, `.obj`, `.fbx`
*   **Dependencies**: `OmsiStudio.Core`, `OmsiStudio.Conversion`, `OmsiStudio.OmsiFormat`
*   **Strict Exclusions for Current Phase**: Inline 3D mesh editing/modeling (conversion operations are batch or file-to-file).
*   **Suggested Epic IDs**: `OS-004` (Conversion Domain & Manifests), `OS-006` (Manifest-Only Exports), `OS-010` (O3D Converter Epic).

---

### 3. Human Editor

*   **Goal**: Create, edit, and configure OMSI humans, passenger behaviors, and pedestrian paths (`.hum` / `.pas`).
*   **Primary Workflows**:
    *   Browsing and editing pedestrian pathways.
    *   Configuring passenger entry/exit coordinates for vehicles and bus stops.
    *   Modifying human behavior rules, voice trigger maps, and mesh definitions.
*   **Core File Formats**: `.hum`, `.pas`
*   **Dependencies**: `OmsiStudio.Core`
*   **Strict Exclusions for Current Phase**: Skeletal animation editing or blending.
*   **Suggested Epic IDs**: `OS-020` (Human Configuration Parser), `OS-021` (Pedestrian Path Editor Canvas).

---

### 4. Spline Editor

*   **Goal**: Design, construct, and edit OMSI road splines (`.sli`), including profiling lanes, texture mapping, and traffic paths configuration.
*   **Primary Workflows**:
    *   Importing roadway cross-sections and profiles.
    *   Mapping road textures case-insensitively with correct tiling frequencies.
    *   Configuring AI traffic paths, speed limits, vehicle types, and pedestrian walkways on splines.
*   **Core File Formats**: `.sli`
*   **Dependencies**: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`
*   **Strict Exclusions for Current Phase**: In-app map terrain editing.
*   **Suggested Epic IDs**: `OS-030` (Spline Format Parsing), `OS-031` (Spline Cross-Section Vector Editor).

---

### 5. Bus Editor

*   **Goal**: Configure and customize OMSI vehicles, bus scripts, instruments, and performance profiles (`.bus`).
*   **Primary Workflows**:
    *   Inspecting and editing vehicle physics profiles (mass, transmission, engine curves).
    *   Wiring dashboard instrumentation, controls, and dynamic script textures.
    *   Editing passenger seating maps and ticket box ticket resolution mappings.
*   **Core File Formats**: `.bus`, `.ovh`, `.osc`, `.const`
*   **Dependencies**: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`
*   **Strict Exclusions for Current Phase**: Script execution runtime engine (static syntax validation only).
*   **Suggested Epic IDs**: `OS-040` (Vehicle Configuration Parser), `OS-041` (Script Consts/Vars Editor Panel).

---

## 🔗 Connection to Existing Epics

The active and completed epics are directly aligned to build up the foundations of the modules above:

### SCO Viewer Modules
*   **SCO Viewer Baseline (`OS-001` to `OS-003`)**: Implements directory scanning, ANSI encoding parsing, search filtering, and localized metadata/diagnostic listings.
*   **Realistic Preview Foundation (`OS-006`, `OS-007`, `OS-008`, `OS-009`)**: Introduces O3D header parsing, unencrypted geometry loading, the default safe software orbit/zoom viewport, and deterministic texture resolution policies.

### O3D Converter Modules
*   **O3D Converter Foundation (`OS-004`, `OS-006`)**: Sets up the export schemas, absolute output path validations, and JSON manifest generation contracts.
*   **Dual-Direction Blender Workflow (`OS-010`)**: Planned implementation of the dual-direction workflow, intermediate scene graph, geometry compiler, and Blender round-trip format. Detailed tasks are tracked under [O3D_CONVERTER_EPIC.md](backlog/O3D_CONVERTER_EPIC.md).
