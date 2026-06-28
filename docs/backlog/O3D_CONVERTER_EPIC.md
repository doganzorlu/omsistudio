# OS-010-FEATURE-005 - O3D Converter System

This epic defines the development roadmap and task breakdown for the O3D Converter system in OmsiStudio. It outlines the architectural pathway to support a dual-direction workflow: importing binary `.o3d` and `.sco` scenery objects, editing them in Blender, and exporting them back to valid `.o3d` geometry and `.sco` definitions safely.

---

## Feature Goal

Establish a reliable conversion pipeline between OMSI formats and editable 3D formats (such as glTF/OBJ), enabling seamless Blender integration:
`OMSI .o3d/.sco -> Intermediate Scene -> Blender edit -> Export to O3D/SCO.`

---

## Delivery Fields

The O3D Converter epic is complete when the system can:
*   Import textured O3D meshes and resolve material texture references.
*   Bridge geometry to Blender (either via direct glTF/OBJ import-export loops or a dedicated plugin strategy).
*   Parse user edits from Blender and map them back to internal scene structures.
*   Export scene geometry to valid binary `.o3d` format matching target specification rules.
*   Serialize `.sco` manifests and model reference blocks back to local disk files.
*   Provide complete round-trip safety validations to ensure geometry scales and material slot maps are preserved intact.

---

## Strict Exclusions

These are explicitly out of scope for this epic:
*   **No Destructive Overwrite**: The converter must not overwrite original scenery object files without explicit user warning and confirmation in the UI.
*   **No Commercial Asset Redistribution**: Excludes any features aimed at packaging or redistributing copyrighted commercial assets.
*   **No Skeletal/Bone Animations**: Mesh animations, skeleton joints, bone weights, and animation frames are excluded from this first version of the converter.
*   **No Binary/Compressed DirectX .x Writer**: Writing geometry to binary/compressed `.x` format is unsupported (only text-based or standard O3D outputs).
*   **No Automatic Texture Transcoding**: Excludes automatic conversion of texture image formats (e.g. DDS to PNG or TGA to JPG) during export, unless specified by future tasks.

---

## Mandatory Safety Rules

The O3D Converter must maintain formatting standards and prevent structural corruption of OMSI assets:
*   **Validation Verification**: Before writing output `.o3d` files, vertex indices, material slots, and bounding boxes must be validated mathematically.
*   **Encrypted Format Guard**: The converter must refuse to write or edit encrypted O3D models.
*   **Non-blocking Execution**: Import and export processes must run asynchronously off the main UI thread.
*   **Diagnostics Surfacing**: File write errors, index overflows, and validation failures must propagate meaningful error diagnostics to the presentation layer.

---

## Task Breakdown

### OS-010-TASK-001 - Create O3D Converter Epic and Backlog
*   **Purpose**: Document the converter architecture, task breakdown, exclusions, and safety policies.
*   **Area**: Project Documentation
*   **Acceptance Criteria**:
    *   `O3D_CONVERTER_EPIC.md` created in `docs/backlog/`.
    *   `OMSISTUDIO_EPIC_BACKLOG.md` updated to include `OS-010-FEATURE-005` in planned backlog.
    *   `PRODUCT_ROADMAP.md` updated to mark the converter module as active/planned.
    *   `README.md` updated to list the new epic in key documentation.

### OS-010-SPIKE-001 - Blender Integration Strategy
*   **Purpose**: Research and draft the optimal technical bridge between OmsiStudio and Blender (e.g. glTF intermediate format vs direct FBX/OBJ vs custom python add-on).
*   **Area**: Architectural Spike Documentation
*   **Acceptance Criteria**:
    *   Write a spike report documenting intermediate format compatibility, coordinate systems (left-handed Y-up vs right-handed Z-up), and python execution scripts.

### OS-010-TASK-002 - Add Converter Domain Models
*   **Purpose**: Formulate core domain entities representing converted models, exports, and round-trip verification blocks.
*   **Area**: `OmsiStudio.Core`
*   **Acceptance Criteria**:
    *   Implement new models for conversion tracking: `OmsiConversionSession`, `OmsiConversionManifest`, and `OmsiExportProfile`.

### OS-010-TASK-003 - Define Import/Export Service Contracts
*   **Purpose**: Establish service interfaces for importing O3D meshes and exporting target format files.
*   **Area**: `OmsiStudio.Core`
*   **Acceptance Criteria**:
    *   Define `IOmsiImportService` and `IOmsiExportService` interfaces supporting cancel tokens and progress reports.

### OS-010-TASK-004 - Implement Internal Scene Graph Model
*   **Purpose**: Design a lightweight intermediate scene graph to hold model hierarchy, meshes, materials, and scopes before translation.
*   **Area**: `OmsiStudio.Core`
*   **Acceptance Criteria**:
    *   Create internal representation objects that capture parent-child nodes, bounding hierarchies, and material references.

### OS-010-TASK-005 - O3D to Intermediate Scene Mapping
*   **Purpose**: Implement translation logic to map parsed `.o3d` mesh arrays into intermediate scene models.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Write unit tests mapping raw vertices, indices, normals, UVs, and material slots to scene nodes.

### OS-010-TASK-006 - SCO to Scene Composition Mapping
*   **Purpose**: Implement parser logic to combine multiple mesh references and placement coordinates from a `.sco` definition into a single scene composition.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Correctly map translation, rotation, and scaling coordinates to scene nodes representing individual meshes.

### OS-010-TASK-007 - Blender Export Format Decision and Prototype
*   **Purpose**: Prototype exporting intermediate scene compositions to a Blender-friendly format (e.g., glTF 2.0).
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Export a test scene to glTF, verify it imports into Blender with correct vertex layouts and texture bindings.

### OS-010-TASK-008 - Blender Import Round-trip Prototype
*   **Purpose**: Parse edited intermediate format models exported back from Blender and verify geometry integrity.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Validate that vertices count, normals, UV coordinates, and materials map back to the scene graph.

### OS-010-TASK-009 - Material/Texture Mapping Policy
*   **Purpose**: Establish the naming, binding, and file resolution rules for texture references in imported/exported models.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Ensure case-insensitivity mapping and asset path structure rules are verified to prevent broken texture references in OMSI.

### OS-010-TASK-010 - O3D Writer Contract and Safety Policy
*   **Purpose**: Define the binary format writer interface and enforce strict safety policies for O3D generation.
*   **Area**: `OmsiStudio.Core`
*   **Acceptance Criteria**:
    *   Define `IO3dGeometryWriter` interface and establish verification rules (e.g., maximum indices bounds).

### OS-010-TASK-011 - Implement O3D Geometry Writer
*   **Purpose**: Implement the binary writer that serializes intermediate scene mesh data into standard `.o3d` formats.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Produce byte-accurate binary outputs readable by standard O3D loaders.
    *   Add tests checking version fields, mesh counts, vertices layout, and material lists.

### OS-010-TASK-012 - Implement SCO Writer
*   **Purpose**: Implement serialization of the scenery object `.sco` configuration files including mesh paths, bounds, and placement offsets.
*   **Area**: `OmsiStudio.OmsiFormat`
*   **Acceptance Criteria**:
    *   Serialize parsed modifications (or new coordinates) back to standard ASCII files conforming to the Turkish/English encoding settings.

### OS-010-TASK-013 - Conversion Validation and Diagnostics
*   **Purpose**: Establish a validation engine to run checks on geometries, coordinates, and textures before writing to disk.
*   **Area**: `OmsiStudio.Conversion`
*   **Acceptance Criteria**:
    *   Return a structured collection of diagnostics highlighting issues like missing textures, degenerate triangles, or scale abnormalities.

### OS-010-TASK-014 - Converter UI Entry Points
*   **Purpose**: Wire the import, Blender bridging, and export commands to the desktop GUI.
*   **Area**: `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Add "Export to Blender" and "Import from Blender" command buttons to the asset details panel.
    *   Show progress dialogs and conversion status details in the UI.

### OS-010-TASK-015 - Converter Tests and Documentation
*   **Purpose**: Complete unit, integration, and round-trip verification tests and document the converter architecture in DOMAIN_MODEL.md.
*   **Area**: `OmsiStudio.App.Tests`, documentation folder
*   **Acceptance Criteria**:
    *   Assert 100% round-trip accuracy on sample geometries.
    *   Verify Turkish/English localizations function correctly.

---

## Recommended Execution Order

1.  `OS-010-TASK-001`
2.  `OS-010-SPIKE-001`
3.  `OS-010-TASK-002`
4.  `OS-010-TASK-003`
5.  `OS-010-TASK-004`
6.  `OS-010-TASK-005`
7.  `OS-010-TASK-006`
8.  `OS-010-TASK-007`
9.  `OS-010-TASK-008`
10. `OS-010-TASK-009`
11. `OS-010-TASK-010`
12. `OS-010-TASK-011`
13. `OS-010-TASK-012`
14. `OS-010-TASK-013`
15. `OS-010-TASK-014`
16. `OS-010-TASK-015`
