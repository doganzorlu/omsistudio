# OS-007-FEATURE-002 - O3D Geometry Pipeline

This epic defines the next implementation sequence for OmsiStudio's `.o3d` support. It builds on the completed metadata pipeline and is intentionally limited to safe geometry extraction.

## Feature Goal

Read supported, unencrypted `.o3d` files and extract geometry into an internal mesh data model.

Workflow:

```text
.o3d file
-> metadata/header validation
-> vertex block read
-> normal and UV extraction
-> face/index block read
-> material reference association
-> validated MeshData result
```

## Delivery Fields

The feature is complete when the format layer can expose safe mesh data for supported `.o3d` files:

* Vertices
* Normals
* UV coordinates
* Triangle indices
* Material references or material slots
* Mesh/submesh grouping if safely available
* Validation diagnostics
* Memory safety diagnostics

## Strict Exclusions

These are explicitly out of scope for this feature:

* No 3D rendering or viewport.
* No orbit/pan/zoom/camera controls.
* No glTF/OBJ/O3D export.
* No Blender integration.
* No package generation.
* No `.sco` generator.
* No UI asset preview panel.
* No material preview rendering.
* No encrypted `.o3d` decryption.
* No best-effort geometry parsing when safety validation fails.

## Mandatory Safety Rules

The safety constraints from [O3D_FORMAT_RESEARCH.md](../spikes/O3D_FORMAT_RESEARCH.md) are mandatory acceptance criteria for this feature:

* DoS protection: never allocate arrays directly from untrusted count fields without threshold and stream-length validation.
* Index validation: every face/index value must be checked against the parsed vertex count before a mesh result is considered valid.
* String bounds validation: every string length must be capped and checked against remaining stream length before allocation/read.
* Truncated stream handling: malformed files must return controlled diagnostics, not raw unhandled exceptions.
* Geometry read bounds: vertex and face block sizes must be calculated with overflow checks before reading.
* Cancellation support: long-running geometry reads must respect `CancellationToken`.

## Source Context

This feature follows the phase plan from [O3D_FORMAT_RESEARCH.md](../spikes/O3D_FORMAT_RESEARCH.md):

1. Test fixtures
2. Metadata parser
3. Geometry parser
4. Export/conversion integration

This epic covers phase 3 only.

---

## Task Breakdown

### ✅ OS-007-TASK-001 - Add Mesh Geometry Domain Models

Create Core domain models for parsed mesh geometry.

Suggested models:

* `O3dMeshData`
* `O3dVertex`
* `O3dNormal`
* `O3dUv`
* `O3dTriangle`
* `O3dMaterialSlot`
* `O3dGeometryReadResult`
* `O3dGeometryStatus`

Acceptance criteria:

* Enum defaults use `Unknown = 0`.
* Models are immutable-style records or init-only classes.
* No binary parser or file IO is introduced in this task.
* `docs/domain/DOMAIN_MODEL.md` is updated.

### ✅ OS-007-TASK-002 - Add O3D Geometry Reader Contract

Create a Core abstraction for geometry reads.

Suggested contract:

* `IO3dGeometryReader`
* `Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)`

Acceptance criteria:

* Contract lives in `OmsiStudio.Core`.
* Return type carries mesh data and structured diagnostics.
* No scanner, UI, conversion, or rendering integration is introduced.

### ✅ OS-007-TASK-003 - Add Geometry Fixture Set

Add small synthetic `.o3d` geometry fixtures.

Required fixture categories:

* Minimal valid geometry file.
* Multiple triangle fixture.
* Invalid index fixture.
* Truncated vertex block fixture.
* Truncated face block fixture.
* Excessive geometry count fixture.
* Material slot fixture if feasible.

Acceptance criteria:

* Fixtures are synthetic and tiny.
* No commercial OMSI/addon data is committed.
* Fixture provenance is documented.
* Fixtures are copied to test output.

### ✅ OS-007-TASK-004 - Add Geometry Safety Limit Policy

Centralize geometry safety thresholds and byte-size calculations.

Required checks:

* Maximum vertices.
* Maximum triangles.
* Maximum material slots.
* Vertex block byte-size overflow.
* Face block byte-size overflow.
* Stream-length feasibility before allocation.

Acceptance criteria:

* Count-derived sizes use checked arithmetic.
* Safety failures produce structured diagnostics.
* Tests cover boundary and overflow cases.

### ✅ OS-007-TASK-005 - Implement Vertex Reader

Read O3D vertex blocks after metadata validation.

Expected vertex layout from research:

* Position: `x`, `y`, `z`
* Normal: `nx`, `ny`, `nz`
* UV: `u`, `v`
* 8 floats, 32 bytes per vertex

Acceptance criteria:

* Reads vertices only for supported, unencrypted files.
* Validates vertex block size before allocation/read.
* Handles truncated vertex blocks with diagnostics.
* Does not read face/index blocks yet.

### OS-007-TASK-006 - Implement Face Reader

Read triangle/face index blocks safely.

Acceptance criteria:

* Supports the known standard index layout from the research spike.
* Uses explicit bounds checks before reading.
* Validates every index against vertex count.
* Invalid indices produce diagnostics and no successful mesh result.
* Does not render or export geometry.

### OS-007-TASK-007 - Handle Long Index Layout

Support the long face/index layout for meshes that require wider indices.

Acceptance criteria:

* Layout choice is deterministic and documented in tests.
* Long index reads use bounds and overflow checks.
* Index validation is identical to standard layout validation.
* Existing standard index tests remain passing.

### OS-007-TASK-008 - Implement Material Slot Reader

Parse material slot metadata needed to associate triangles with material references.

Acceptance criteria:

* String bounds validation is mandatory.
* Material strings are capped by explicit maximum length.
* No shader/material preview logic is introduced.
* No texture file existence checks are performed.

### OS-007-TASK-009 - Compose MeshData From Geometry Sections

Combine metadata, vertices, faces, and material slots into `O3dMeshData`.

Acceptance criteria:

* Result preserves vertex order and triangle index order.
* Result carries material slot associations if available.
* Diagnostics are preserved in `O3dGeometryReadResult`.
* Partial invalid geometry does not return a successful mesh result.

### OS-007-TASK-010 - Add Geometry Reader Cancellation Tests

Ensure long-running geometry reads support cancellation.

Acceptance criteria:

* Cancellation before reading throws or returns a consistent cancellation result.
* Cancellation during vertex loop is covered.
* Cancellation during face loop is covered.
* No cancellation is swallowed as a generic read failure.

### OS-007-TASK-011 - Add Geometry Validation Audit Tests

Add focused safety audit tests for the full geometry pipeline.

Required coverage:

* DoS count protection.
* Vertex block length validation.
* Face block length validation.
* Index validation.
* String bounds validation.
* Truncated stream handling.

Acceptance criteria:

* Tests fail if untrusted counts can cause unbounded allocation.
* Tests fail if invalid face indices are accepted.
* Tests fail if malformed strings overrun the stream.

### OS-007-TASK-012 - Add Scanner-Independent Geometry Reader Tests

Validate geometry reader behavior without involving `.sco` scanning.

Acceptance criteria:

* Tests target `IO3dGeometryReader` / concrete implementation directly.
* No `OmsiAssetScanner` dependency.
* No App dependency.
* No conversion dependency.

### OS-007-TASK-013 - Document Coordinate System and Winding Decisions

Document the raw geometry orientation assumptions before any preview/export work.

Acceptance criteria:

* Document whether parsed data is raw O3D/DirectX-space or transformed.
* Document winding-order handling status.
* Do not implement transformation/export in this task.
* Update research notes if implementation confirms or changes assumptions.

### OS-007-TASK-014 - Geometry Pipeline Documentation and Backlog Update

Update project docs after the geometry pipeline is complete.

Update:

* `README.md`
* `docs/domain/DOMAIN_MODEL.md`
* `docs/backlog/OMSISTUDIO_EPIC_BACKLOG.md`
* `docs/backlog/O3D_GEOMETRY_PIPELINE_EPIC.md`
* optionally `docs/spikes/O3D_FORMAT_RESEARCH.md`

Acceptance criteria:

* Documentation says geometry parsing exists.
* Documentation still says rendering, viewport, and conversion/export are not implemented.
* Backlog accurately marks completed tasks.

---

## Recommended Execution Order

1. `OS-007-TASK-001`
2. `OS-007-TASK-002`
3. `OS-007-TASK-003`
4. `OS-007-TASK-004`
5. `OS-007-TASK-005`
6. `OS-007-TASK-006`
7. `OS-007-TASK-007`
8. `OS-007-TASK-008`
9. `OS-007-TASK-009`
10. `OS-007-TASK-010`
11. `OS-007-TASK-011`
12. `OS-007-TASK-012`
13. `OS-007-TASK-013`
14. `OS-007-TASK-014`

## First Task To Issue

Start with:

```text
OS-007-TASK-001 - Add Mesh Geometry Domain Models
```
