# ✅ OS-006-FEATURE-001 - O3D Metadata Pipeline (Completed)

This epic defines the next implementation sequence for OmsiStudio's `.o3d` support. It is intentionally limited to metadata inspection.

## Feature Goal

Read an `.o3d` file safely, parse only header-level metadata, extract statistics, and display the results in the UI.

Workflow:

```text
.o3d file
-> safe binary open
-> header read
-> metadata/statistics extraction
-> UI inspector display
```

## Delivery Fields

The feature is complete when the app can expose the following metadata for resolved `.o3d` model references:

* Version
* Encrypted status
* Mesh count
* Vertex count
* Triangle count
* Material count
* Texture references

## Strict Exclusions

These are explicitly out of scope for this feature:

* No vertex buffer reading.
* No face/index buffer reading.
* No normals/UV geometry model.
* No geometry parsing into `MeshData`.
* No 3D rendering or viewport.
* No glTF/OBJ/O3D export.
* No material preview.
* No conversion pipeline expansion beyond metadata availability.

## Source Context

This feature follows the phased recommendation from [O3D_FORMAT_RESEARCH.md](../spikes/O3D_FORMAT_RESEARCH.md):

1. Test fixtures
2. Metadata parser
3. Geometry parser
4. Export/conversion integration

This epic covers phases 1 and 2 only.

---

## Task Breakdown

### ✅ OS-006-TASK-001 - Add O3D Metadata Domain Models

Create Core models for the metadata pipeline.

Suggested models:

* `O3dMetadata`
* `O3dTextureReference`
* `O3dMetadataReadResult`
* `O3dMetadataStatus`
* `O3dFormatVersion`

Acceptance criteria:

* Enum defaults use `Unknown = 0`.
* Models are immutable-style records or init-only classes.
* No parser or binary reader code is introduced in this task.
* `docs/domain/DOMAIN_MODEL.md` is updated.

### ✅ OS-006-TASK-002 - Add Safe Binary Reader Foundation

Create bounded binary reading utilities in `OmsiStudio.OmsiFormat`.

Scope:

* Little-endian primitive reads.
* Bounded string reads.
* Stream length checks before all reads.
* Safe seek and stream position helpers.

Acceptance criteria:

* Controlled parser diagnostics are returned or thrown through known exception types.
* Raw unexplained stream exceptions are not allowed to leak from normal malformed input paths.
* String bounds validation is covered by tests.
* Truncated streams are covered by tests.

### ✅ OS-006-TASK-003 - Add O3D Parse Diagnostics

Create diagnostics support for O3D metadata parsing.

Suggested models:

* `O3dDiagnostic`
* `O3dDiagnosticSeverity`
* common diagnostic codes

Acceptance criteria:

* Diagnostics support warnings and errors.
* `O3dMetadataReadResult` can carry diagnostics.
* No UI integration yet.

### ✅ OS-006-TASK-004 - Add O3D Metadata Fixtures

Add small fixtures for metadata reader development.

Required fixture categories:

* Minimal valid metadata file.
* Truncated header.
* Invalid count / DOS guard sample.
* Invalid string length sample.
* Encrypted or unsupported sample if feasible.

Acceptance criteria:

* Fixtures are small and test-owned.
* No copyrighted commercial addon files are committed.
* Fixture provenance or synthetic-generation notes are documented.

### ✅ OS-006-TASK-005 - Detect O3D Version and Encryption State

Implement the first metadata reader slice.

Scope:

* Open `.o3d` files.
* Inspect only minimal header bytes.
* Detect version if possible.
* Detect encrypted/unsupported state if possible.
* Return status instead of crashing.

Acceptance criteria:

* Valid minimal fixture returns a version.
* Encrypted or unknown fixture returns `Encrypted` or `Unsupported`.
* No geometry blocks are read.

### ✅ OS-006-TASK-006 - Parse O3D Header Counts

Extend metadata reading to extract safe header-level counts.

Fields:

* Vertex count
* Triangle count
* Material count
* Mesh/submesh count if present or safely derivable

Acceptance criteria:

* Bounds checks run before trusting count values.
* Impossible count values produce diagnostics.
* DOS protection is mandatory: count-derived sizes must be checked against stream length before allocation or iteration.
* No vertex or face payload is parsed.

### ✅ OS-006-TASK-007 - Parse O3D Texture References Metadata

Read texture reference strings only when safely reachable from metadata/header/material sections.

Acceptance criteria:

* String bounds validation is mandatory.
* Invalid string lengths produce diagnostics.
* No texture file existence checks are performed.
* No material rendering logic is introduced.

### ✅ OS-006-TASK-008 - Add O3D Metadata Reader Contract

Add a Core service contract and OmsiFormat implementation.

Suggested contract:

* `IO3dMetadataReader`
* `Task<O3dMetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)`

Acceptance criteria:

* Contract returns `O3dMetadataReadResult`.
* CancellationToken is supported.
* Implementation lives in `OmsiStudio.OmsiFormat`.
* No App dependency is introduced.

### ✅ OS-006-TASK-009 - Extend Domain Mapping for Model Metadata

Attach metadata to resolved model references.

Recommended option:

* Add `O3dMetadata? Metadata` or equivalent metadata result reference to `OmsiModelReference`.

Acceptance criteria:

* Existing `MeshPath`, `ResolvedPath`, `Exists`, and `ResolutionStatus` behavior remains compatible.
* Missing or invalid model references do not attempt metadata reads.
* `docs/domain/DOMAIN_MODEL.md` is updated.

### ✅ OS-006-TASK-010 - Integrate Metadata Reader Into Scanner Flow

Read metadata for resolved `.o3d` paths during scan.

Scope:

* Resolved `.o3d` model references only.
* Metadata diagnostics become scan warnings.
* Bad `.o3d` metadata does not fail the whole scan.

Acceptance criteria:

* Scan never crashes because of a malformed `.o3d` file.
* Existing `.sco` scan behavior remains intact.
* Missing/invalid model references keep existing warning behavior.
* No `.o3d` geometry payload is read.

### ✅ OS-006-TASK-011 - Add UI O3D Metadata Inspector Section

Show metadata in selected asset details.

Display per model reference:

* Version
* Encrypted status
* Vertex count
* Triangle count
* Material count
* Texture references
* Diagnostics summary

Acceptance criteria:

* Labels are localized in Turkish and English.
* Empty/no metadata state is handled.
* Encrypted/unsupported state is shown safely.
* No 3D preview or geometry display is added.

### ✅ OS-006-TASK-012 - Add Metadata Reader App Tests

Add UI/ViewModel-facing tests for metadata display behavior.

Acceptance criteria:

* Selected asset exposes metadata.
* Encrypted/unsupported metadata is shown safely.
* Diagnostics can be surfaced without crashing.
* Missing `.o3d` files remain warning-only.

### ✅ OS-006-TASK-013 - Add Metadata Pipeline Safety Audit Tests

Add explicit safety tests derived from the O3D research spike.

Required coverage:

* DOS count protection.
* Geometry/index blocks are not read during metadata phase.
* String bounds validation.
* Truncated stream handling.
* Cancellation handling.

Acceptance criteria:

* Safety rules from `O3D_FORMAT_RESEARCH.md` are covered for metadata parsing.
* No unbounded allocation is possible from untrusted header values.
* Tests fail if the metadata reader starts reading geometry payloads.

### ✅ OS-006-TASK-014 - Documentation and Backlog Update

Update project documentation after implementation.

Update:

* `README.md`
* `docs/domain/DOMAIN_MODEL.md`
* `docs/backlog/OMSISTUDIO_EPIC_BACKLOG.md`
* optionally `docs/spikes/O3D_FORMAT_RESEARCH.md` with implementation notes

Acceptance criteria:

* Documentation clearly says O3D metadata exists.
* Documentation clearly says geometry/render/conversion are still not implemented.
* Backlog marks completed tasks accurately.

---

## Recommended Execution Order

1. `OS-006-TASK-001`
2. `OS-006-TASK-002`
3. `OS-006-TASK-003`
4. `OS-006-TASK-004`
5. `OS-006-TASK-005`
6. `OS-006-TASK-006`
7. `OS-006-TASK-007`
8. `OS-006-TASK-008`
9. `OS-006-TASK-009`
10. `OS-006-TASK-010`
11. `OS-006-TASK-011`
12. `OS-006-TASK-012`
13. `OS-006-TASK-013`
14. `OS-006-TASK-014`

## Epic Completion Note

OS-006-FEATURE-001 is complete. Future O3D work should continue under FEATURE-002 O3D Geometry Pipeline.

