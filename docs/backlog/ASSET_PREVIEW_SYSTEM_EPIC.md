# OS-008-FEATURE-003 - Asset Preview System

This epic defines the implementation sequence for a safe, interactive 3D preview system in OmsiStudio. It builds on the completed O3D metadata and geometry pipelines.

## Feature Goal

Load parsed `O3dMeshData` for a selected scenery object and display it in an interactive preview viewport.

Workflow:

```text
Selected OmsiAsset
-> resolved .o3d model reference
-> O3dGeometryReader
-> MeshData
-> preview scene model
-> interactive viewport
```

## Delivery Fields

The feature is complete when the app can preview supported, unencrypted `.o3d` meshes with:

* Mesh loading from selected asset model references
* Viewport rendering
* Orbit camera
* Zoom
* Reset camera
* Wireframe toggle
* Bounding box toggle
* Basic material/texture preview if available
* Controlled diagnostics for unsupported, encrypted, invalid, or missing geometry

> [!NOTE]
> **Epic Completion Note**:
> The Asset Preview System is fully complete and operational. The default rendering pathway is a safe, software-rendered 3D viewport using `DrawingContext` (supporting Wireframe, Solid, and SolidWireframe visual modes, camera orbit/zoom/reset, bounding box overlays, and deterministic texture-aware material swatches). The OpenGL viewport remains experimental. Real texture mapping (UV mapping of image files) and camera panning are out of scope (future scope).
> 
> * **OS-008 Software Preview Status**: Complete.
> * **Next Milestone**: Real texture/OMSI-like preview will continue under `OS-009`. For roadmap, see the [Realistic OMSI Asset Preview Epic](REALISTIC_ASSET_PREVIEW_EPIC.md).

## Strict Exclusions

These are explicitly out of scope for this feature:

* No glTF/OBJ/O3D export.
* No Blender integration.
* No package generation.
* No `.sco` generation.
* No editing mesh geometry.
* No best-effort rendering when geometry validation fails.
* No support for encrypted `.o3d` decryption.
* No camera panning (Pan is future scope).
* No real texture bitmap mapping on faces (deterministic flat coloring is used instead).

## Mandatory Safety Rules

The preview system must preserve the safety guarantees from the O3D geometry pipeline:

* Preview code must not parse binary geometry directly; it must consume `IO3dGeometryReader`.
* Invalid geometry must not be rendered.
* Large meshes must have bounded memory and performance behavior.
* Geometry loading must be cancellable.
* UI interactions must stay responsive during loading.
* Rendering errors must surface as structured UI state, not crashes.

## Task Breakdown

### ✅ OS-008-SPIKE-001 - Choose Avalonia 3D Rendering Approach

Evaluate the rendering approach for a cross-platform Avalonia desktop app.

Acceptance criteria:

* Compare viable options for Avalonia + .NET 8 desktop rendering.
* Include integration notes for macOS, Windows, and Linux.
* Include risk assessment for input handling, resizing, GPU context lifecycle, and testability.
* Recommend one implementation path.
* No production app behavior changes.

> [!NOTE]
> **Completion Note**: OpenGlControlBase + Silk.NET önerildi. Veldrid ve OpenTK/native hosting alternatifleri değerlendirildi. Renderer implementation, package changes ve production code yapılmadı.

### ✅ OS-008-TASK-001 - Add Preview Domain Models

Create Core/App-level models needed to describe preview state without binding to a rendering library.

Suggested models:

* `AssetPreviewStatus`
* `AssetPreviewRequest`
* `AssetPreviewResult`
* `PreviewCameraState`
* `PreviewRenderOptions`
* `MeshBounds`

Acceptance criteria:

* Models are testable and independent of UI controls.
* Enum defaults use `Unknown = 0`.
* No rendering implementation is introduced.
* `docs/domain/DOMAIN_MODEL.md` is updated.

> [!NOTE]
> **Completion Note**: Added `PreviewVector3D`, `MeshBounds`, `AssetPreviewStatus`, `AssetPreviewRequest`, `AssetPreviewResult`, `PreviewCameraState`, and `PreviewRenderOptions` as immutable/init-only records in `OmsiStudio.Core`. Added unit tests verifying default values, status enum offsets, and properties mappings under `OmsiStudio.Core.Tests`. Updated DOMAIN_MODEL.md.

### ✅ OS-008-TASK-002 - Add Preview Service Contracts

Define contracts for loading preview-ready mesh data from a selected asset/model reference.

Suggested contracts:

* `IAssetPreviewLoader`
* `Task<AssetPreviewResult> LoadAsync(...)`

Acceptance criteria:

* Contract consumes existing domain objects and `IO3dGeometryReader`.
* Missing, encrypted, invalid, and unsupported geometry are represented safely.
* Cancellation is supported.
* No UI or rendering implementation is introduced.

> [!NOTE]
> **Completion Note**: Created the `IAssetPreviewLoader` service contract under `OmsiStudio.Core.Services`. Added unit tests using a fake implementation to verify contract integration and cancellation flow in `OmsiStudio.Core.Tests`. Updated DOMAIN_MODEL.md.

### ✅ OS-008-TASK-003 - Compute Mesh Bounds and Preview Scene Data

Convert parsed `O3dMeshData` into preview scene data and compute safe bounds.

Acceptance criteria:

* Computes min/max bounds and center.
* Handles empty or invalid mesh data safely.
* Preserves raw DirectX-space geometry without coordinate conversion unless explicitly documented.
* Unit tests cover bounds and degenerate meshes.

> [!NOTE]
> **Completion Note**: Created `IMeshBoundsCalculator` contract and `MeshBoundsCalculator` implementation under `OmsiStudio.Core.Services` to compute bounds (min, max, center, size) preserving raw coordinates. Added unit tests under `OmsiStudio.Core.Tests` covering negative coordinates, asymmetry, single-vertex, empty collections, and null checks. Updated DOMAIN_MODEL.md.

### ✅ OS-008-TASK-004 - Add Preview ViewModel State

Add ViewModel state for selected model preview loading.

Acceptance criteria:

* Selecting a model reference can request preview loading.
* Loading, success, empty, unsupported, invalid, and error states are represented.
* Loading is cancellable.
* No renderer-specific API leaks into the ViewModel.

> [!NOTE]
> **Completion Note**: Added `PreviewStatus`, `PreviewResult`, `SelectedPreviewModelReference`, `PreviewDiagnostics`, `HasPreview`, `HasPreviewDiagnostics`, `IsPreviewLoading`, and `PreviewStatusText` properties, along with `LoadPreviewCommand` and `CancelPreviewCommand` to `MainWindowViewModel`. Registered `IAssetPreviewLoader` (defaulting to `NullAssetPreviewLoader`) as a dependency. Implemented UI status display panel and Preview buttons in `MainWindow.axaml`. Added comprehensive unit tests under `OmsiStudio.App.Tests` covering success, diagnostic, cancellation, and culture-dependent status text update transitions.

### ✅ OS-008-TASK-005 - Add Preview Panel Placeholder

Add a UI panel placeholder for preview state before the renderer is wired.

Acceptance criteria:

* Shows selected model name and preview state.
* Shows diagnostics for invalid/unsupported geometry.
* Does not render 3D yet.
* Existing asset details remain usable.

> [!NOTE]
> **Completion Note**: Placeholder preview panel eklendi. Selected model name/status/diagnostics/mesh summary/bounds summary gösteriliyor. Renderer/OpenGL/Silk.NET/package eklenmedi. Tests App layer’da eklendi.

### ✅ OS-008-TASK-006 - Add Renderer Host Abstraction

Introduce an App-layer rendering host abstraction based on the chosen spike result.

Acceptance criteria:

* Rendering host lifecycle is explicit: initialize, resize, render, dispose.
* No geometry parsing occurs in the renderer.
* Failure to initialize GPU/context is handled safely.
* Unit-testable non-GPU contracts are added.

> [!NOTE]
> **Completion Note**: Renderer host lifecycle interface (`IRendererHost`) and models (`RendererHostState`, `RendererHostSize`, `RendererInitializationResult`, `RenderFrameResult`) were defined in `OmsiStudio.App.Services.Rendering`. Created `NullRendererHost` as a safe fallback and implemented unit tests under `OmsiStudio.App.Tests` to verify transition states, resize, property propagation, render frame, and post-dispose protection.

### ✅ OS-008-TASK-007 - Render MeshData in Viewport

Render loaded mesh data in the preview panel.

Acceptance criteria:

* Supported mesh data appears in the viewport.
* Invalid or unsupported geometry is not rendered.
* Viewport remains responsive while loading.
* No export/conversion behavior is added.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-007A: Integrated Avalonia `OpenGlControlBase` control (`O3dGlViewportControl.cs`) with safe lifecycle management and localization support.
> * OS-008-TASK-007B: Implemented GPU buffer allocation (VAO, VBO, EBO) and basic flat-colored wireframe shader pipeline in `OpenGlRendererHost.cs` using `Silk.NET.OpenGL`. Enabled unsafe pointer support in the project file. Covered empty/null mesh safety and renderer lifecycle transitions with unit tests.

### ✅ OS-008-TASK-008 - Add Camera Controls

Implement basic interactive camera controls.

Acceptance criteria:

* Orbit is supported.
* Pan is explicitly future scope.
* Zoom is supported.
* Reset camera is supported.
* Controls work without interfering with asset list scrolling.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-008A: Implemented camera property bindings (`CameraYaw`, `CameraPitch`, `CameraDistance`) and `ResetCameraCommand` on `MainWindowViewModel`, propagating camera matrices via `CameraTransformCalculator` to the `OpenGlRendererHost` shader.
> * OS-008-TASK-008B: Embedded overlay UI buttons (Yaw left/right, Pitch up/down, Zoom in/out, Reset) in `MainWindow.axaml` with full TR/EN localization and strict boundary clamps (Pitch: -89 to 89, Zoom: 0.5 to 50). Mouse interaction and pan controls are out of scope for this task and postponed to future orbit controls tasks.

### ✅ OS-008-TASK-009 - Add Wireframe and Bounding Box Toggles

Add basic inspection modes for model structure.

Acceptance criteria:

* Wireframe toggle works.
* Bounding box toggle works.
* Toggle state is visible in the UI.
* Defaults are documented and tested at ViewModel level.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-009: Superseded by the Software Wireframe Viewport pivot and integrated alongside visual render modes. Renders bounding boxes dynamically via custom pen geometries, and enables toggling between Wireframe, Solid, and Solid + Wireframe modes via ViewModel properties, all fully localized and covered by unit tests.

### ✅ OS-008-TASK-010 - Add Basic Material and Texture Preview

Use parsed material slots and texture references for basic preview styling.

Acceptance criteria:

* Texture references are resolved only if safely available.
* Missing textures do not fail mesh rendering.
* Basic material fallback is provided.
* No material editing is introduced.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-010A: Implemented high-fidelity software wireframe/solid viewport fallback rendering with face normal shading (painters-sorting back-to-front depth simulation), support for wireframe overlay, and localized placeholder text.
> * OS-008-FIX-010A: Decoupled software viewport from OpenGL state, corrected camera control overlay visibility to always show in software mode, made Solid + Wireframe the default view mode, and moved visual mode enum to rendering services namespace.

### ✅ OS-008-TASK-011 - Add Software Preview Material Color Modes

Support software preview material color modes to display distinct colors for triangles based on their material slot index.

Acceptance criteria:

* Solid render loop calculates flat view-space face normal safely to prevent NaN/crashes.
* Procedural deterministic color generation based on MaterialSlotIndex is used for Solid modes.
* Degenerate triangles and invalid material slot indexes fall back safely to base slate color.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-011: Implemented procedural deterministic HSL color generation based on `MaterialSlotIndex` (base color * face normal intensity) for solid rendering modes. Handled empty/null/invalid material slots and out-of-bounds slot indexes gracefully with slate fallback color. Created comprehensive unit tests for deterministic coloring, fallback behaviors, and wireframe preservation.
> * OS-008-FIX-011A: Hardened the solid render loop normal calculations against degenerate triangles to prevent NaN/division-by-zero crashes. Moved normal calculation and shading multiplication to testable static methods in `MaterialColorSelector`. Created unit tests verifying degenerate normal and invalid material index fallback behaviors.

### ✅ OS-008-TASK-012 - Add Software Preview Texture-Aware Material Display

Support texture-aware visual material coloring where distinct texture references map to deterministic preview colors, and expose the previewed materials list in the UI.

Acceptance criteria:

* MaterialColorSelector hashes TextureReference deterministically.
* Empty/null texture references fall back to material index-based coloring.
* Selected preview materials collection is safely exposed and bound in the UI panel.
* Material name, texture reference, and color swatches are localized and displayed nicely.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-012: Extended `MaterialColorSelector` to compute deterministic base colors by hashing the slot's `TextureReference` using a polynomial rolling hash. If the texture reference is missing or empty, it falls back to slot-index-based coloring. Formulated `MaterialDisplayItem` UI display model and exposed `PreviewMaterials` collection in `MainWindowViewModel`. Bound it in `MainWindow.axaml` using a premium `ItemsControl` with swatches and localized text blocks. Added comprehensive unit tests for texture hashing, fallback coloring, and VM/UI collection exposure.

### ✅ OS-008-TASK-013 - Add Preview Performance Guardrails

Add guardrails for large mesh preview behavior.

Acceptance criteria:

* Large mesh loading remains cancellable.
* UI does not block during preview load.
* Preview can decline rendering meshes above documented thresholds.
* User-facing diagnostics explain skipped previews.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-013: Introduced configurable limits (`MaxPreviewVertices`, `MaxPreviewTriangles`, `MaxPreviewMaterials`) in `AssetPreviewLoader.cs`. Checked these parameters immediately post-load, returning `AssetPreviewStatus.Unsupported` with null `MeshData` and a localized `PreviewMeshTooLarge` diagnostic warning context. Integrated the localization service into the loader and verified safety behaviors (under limits success, vertex limit skip, triangle limit skip, material limit skip, and output warnings) with unit tests.

### ✅ OS-008-TASK-014 - Preview System Tests and Documentation

Close the preview epic with tests and documentation.

Acceptance criteria:

* README documents preview capability and exclusions.
* Backlog marks OS-008 complete.
* Tests cover preview ViewModel state, loader outcomes, and rendering host contracts.
* Export/conversion remains explicitly future scope.

> [!NOTE]
> **Completion Note**: 
> * OS-008-TASK-014: Consolidated unit test coverage, ensuring `Bounds == null` assertions on guardrail limits skips. Verified TR/EN format strings for `PreviewMeshTooLarge` warnings and validated dynamic material list localization. Re-designed the wireframe viewport smoke test to safely evaluate property and state assignment correctness. Updated repository `README.md` and deep-dive `DOMAIN_MODEL.md` to document implemented software rendering features and exclusions. Closed the preview epic backlog.

## Recommended Execution Order

1. `OS-008-SPIKE-001`
2. `OS-008-TASK-001`
3. `OS-008-TASK-002`
4. `OS-008-TASK-003`
5. `OS-008-TASK-004`
6. `OS-008-TASK-005`
7. `OS-008-TASK-006`
8. `OS-008-TASK-007`
9. `OS-008-TASK-008`
10. `OS-008-TASK-009`
11. `OS-008-TASK-010`
12. `OS-008-TASK-011`
13. `OS-008-TASK-012`
14. `OS-008-TASK-013`
15. `OS-008-TASK-014`
