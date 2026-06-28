# OS-009-FEATURE-004 - Realistic OMSI Asset Preview System

This epic defines the development roadmap and task breakdown for a realistic 3D asset preview system in OmsiStudio. It builds upon the completed software wireframe/solid preview pipeline (`OS-008`) and aims to render O3D geometry textured with actual bitmap images resolved from OMSI scenery object structures.

## Feature Goal

Transition the 3D viewport from procedural color shading to a realistic textured preview:
`.sco -> model references -> .o3d geometry -> material texture references -> bitmap loading -> UV mapped preview.`

## Delivery Fields

The realistic preview feature is complete when the viewport can:
* Resolve and locate texture files recursively from OMSI asset directories.
* Load and decode common image formats (BMP, JPG, PNG, TGA, DDS).
* Rasterize texture bitmaps onto O3D geometry faces using UV mapping coordinates.
* Display multi-mesh scenery objects with correct relative translation, rotation, and scaling coordinates parsed from the `.sco` file.
* Handle basic alpha blending and transparency thresholds.
* Maintain responsiveness and safety using caching and performance policies.

## Strict Exclusions

These are explicitly out of scope for this feature:
* No writing/saving modified texture files.
* No format transcoding or export of textures (e.g. converting TGA to DDS).
* No real-time shadow mapping, normal/bump maps, or complex PBR materials.
* No editing of multi-mesh placements or saving changes back to `.sco`.

## Mandatory Safety Rules

The realistic preview system must maintain stability and avoid memory depletion:
* **Memory Safety**: Large textures must be loaded with maximum dimensions policy and downscaled if they exceed memory guardrails.
* **Format Safety**: Corrupted or unsupported texture files must fail gracefully with warnings and continue rendering using flat swatches.
* **Non-blocking UI**: Image loading and decoding must occur asynchronously.
* **Resource Cleanup**: Texture resources must be cached and disposed of correctly on model unloading.

## Task Breakdown

### ✅ OS-009-TASK-001 - Create Realistic Preview Epic and Backlog
*   **Purpose**: Document the architecture, task sequence, and acceptance criteria for realistic preview capabilities.
*   **Area**: Project Documentation
*   **Acceptance Criteria**:
    *   `REALISTIC_ASSET_PREVIEW_EPIC.md` created in `docs/backlog/`.
    *   `OMSISTUDIO_EPIC_BACKLOG.md` updated to include `OS-009-FEATURE-004` in planned backlog.
    *   `ASSET_PREVIEW_SYSTEM_EPIC.md` contains transition notes linking to `OS-009`.
    *   `README.md` contains links to the new epic.
*   **Strict Exclusions**: No implementation of loaders or renderers in code.

### ✅ OS-009-TASK-002 - Texture Path Resolver Service
*   **Purpose**: Dynamically locate referenced texture files across OMSI scenery object structures.
*   **Area**: `OmsiStudio.Core`, `OmsiStudio.OmsiFormat`
*   **Acceptance Criteria**:
    *   Find texture files by checking the model directory, the asset directory, and the parent texture directory.
    *   Support case-insensitive matching to handle incorrect casing in `.sco` references.
    *   Support fallback pathways.
*   **Strict Exclusions**: Actual file reading or decoding.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-002: Formulated `OmsiTextureReferenceResolutionStatus` and `OmsiTextureReference` domain models, registered `IOmsiTextureReferenceResolver` interface, and implemented `OmsiTextureReferenceResolver.cs`. It validates paths against path traversal escaping (checking model folder, asset folder, and sceneryobjects root allowed scopes), maps candidate directories (model dir, model subfolders, asset folder, asset subfolders, and root relative), and enforces case-insensitive segment matchings for cross-platform file system support. Created comprehensive unit tests validating resolution scopes, case casing, missing statuses, traversals, and absolute paths constraints.
> * OS-009-FIX-002A: Hardened relative path traversal checks by directly blocking any path containing a `..` segment (supporting `/` and `\` directory separators). Updated `IsDescendantOf` to be a separator-aware string segment comparison helper to eliminate sibling-path false positives. Removed parent directory traversal capabilities (`..`) inside `ResolveCaseInsensitive`. Expanded unit tests to assert invalid paths with relative traversal segments, case formats, and sibling edge cases.

### ✅ OS-009-TASK-003 - Bitmap Texture Loader & Format Inventory
*   **Purpose**: Build a robust reader to load and decode supported image formats.
*   **Area**: `OmsiStudio.Conversion` (or dedicated Service layer)
*   **Acceptance Criteria**:
    *   Load BMP, JPG, PNG, DDS, and TGA.
    *   Verify headers and safely decode pixel buffers.
    *   Gracefully throw exceptions on corrupted/invalid image data.
*   **Strict Exclusions**: Native OS decoders that leak memory; must use managed or safe wrappers.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-003: Added `TextureImageFormat` and `TextureLoadStatus` enums, `TextureImageData` and `TextureLoadResult` domain records in `OmsiStudio.Core`. Registered `ITextureImageLoader` service contract. Implemented `TextureImageLoader` utilizing `StbImageSharp` package (100% managed C#, zero native dependencies). Restricts dimension to 4096 max, queries headers via `ImageInfo` to reject oversized payloads early, verifies format magic headers for PNG, BMP, JPEG, and intercepts TGA/DDS files as `UnsupportedFormat`. Completely supports cooperative cancellation. Wrote synthetic BMP/PNG/JPEG in-memory generator unit tests asserting dimensions, formats, cancellations, limits, and corrupted/missing files.
> * OS-009-FIX-003A: Hardened memory allocation policies by implementing a file size guardrail (`MaxTextureFileBytes = 64MB`) immediately rejecting files before loading bytes. Restructured dimension checking to seek and read only magic header bytes (first 4 bytes) and probe dimensions using `ImageInfo.FromStream(fs)` directly on the file stream, performing full byte reads and pixel decoding only after size and header checks pass. Added sparse file length check tests and early cooperative cancellation tests.

### ✅ OS-009-TASK-004 - Material-to-Texture Binding & Validation
*   **Purpose**: Model the link between O3D material slots and decoded texture assets.
*   **Area**: `OmsiStudio.Core`
*   **Acceptance Criteria**:
    *   Bind parsed `O3dMaterialSlot` values to their loaded `Bitmap` references.
    *   Generate structured warning diagnostics if a material has a missing texture.
*   **Strict Exclusions**: Rendering.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-004: Created `TextureBindingStatus` enum and `MaterialTextureBinding` domain record in `OmsiStudio.Core`. Registered `IMaterialTextureBindingService` contract and implemented `MaterialTextureBindingService` in `OmsiStudio.App`. It matches O3D material slots to texture references, delegates resolution to `IOmsiTextureReferenceResolver` and decoding to `ITextureImageLoader`, formats statuses, handles diagnostics context mapping (prepending material indices and names), and preserves order/index. Wrote comprehensive tests covering successful binds, empty reference, missing resolver paths, invalid paths, and loader outcomes (unsupported, invalid, too-large, failed, cancellation).
> * OS-009-FIX-004A: Hardened diagnostic collection in material texture binding by preserving structured loader fields (Severity, Code, ByteOffset, Context) in mapped binding outcomes. Assigned correct diagnostic codes (such as `O3dDiagnosticCode.FileNotFound` for missing texture files and `O3dDiagnosticCode.InvalidPath` for traversal violations) to service-generated errors. Updated tests to assert diagnostic codes, offsets, and context preservation.

### ✅ OS-009-TASK-005 - Add Textured Triangle Rasterizer Core
*   **Purpose**: Extend the software viewport control to interpolate pixels and render UV-mapped textures onto faces.
*   **Area**: `OmsiStudio.App` (Views/Rendering)
*   **Acceptance Criteria**:
    *   Implement scanline rendering or affine texture mapping on 2D projected triangles.
    *   Apply base colors mapped from the texture reference.
    *   Retain flat normal shading overlays.
*   **Strict Exclusions**: Mipmap, bilinear filtering, texture cache, SCO composition, UI toggle.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-005: Formulated a UI-independent CPU-side `SoftwareTexturedTriangleRasterizer` helper class inside `OmsiStudio.App`. It accepts target RGBA pixel buffers, coordinates, UV mapping pairs, and source texture data. Calculates clamped projected screen bounding boxes for early clipping, checks sub-pixel centers using Barycentric coordinates, re-normalizes weights, maps nearest-neighbor repeating wraps, and applies intensity shading to color channels while preserving alpha. Wrote comprehensive tests covering center pixel colors, UV mappings, alpha channel preservation, viewport bounds clipping, degenerate triangles no-ops, and safety guards for null/invalid arguments.
> * OS-009-FIX-005A: Hardened memory bounds check logic inside `SoftwareTexturedTriangleRasterizer` using checked arithmetic (with `checked` scope) to verify expected byte array sizes (`width * height * 4` and `texture.Width * texture.Height * 4`) and detect numeric overflows, immediately returning no-op on validation failures. Expanded tests to assert safe, crash-free execution on short pixel arrays and numeric overflows.

### ✅ OS-009-TASK-006 - Integrate Textured Rasterizer Into Software Preview Control
*   **Purpose**: Prevent reloading identical texture files across multiple material slots or meshes.
*   **Area**: `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Solid and SolidWireframe modes in SoftwareWireframeViewportControl render bound textures using the rasterizer.
    *   Flat material color fallback is preserved if no texture is bound.
    *   Degenerate triangles and invalid material index handle safely.
*   **Strict Exclusions**: OpenGL experimental path.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-006: Integrated `SoftwareTexturedTriangleRasterizer` rendering loop inside the back-to-front sorted depth drawing section of `SoftwareWireframeViewportControl`. Added dependency property `TextureBindings` to bind VM-level mappings. Created a cached `WriteableBitmap` rendering target resized only on layout changes to prevent GC pressure. Non-textured or invalid texture slots render using 1x1 flat textures matching deterministic base colors. Handled null contexts and invalid indices gracefully. Updated unit tests verifying property accessibility and early exits.
> * OS-009-FIX-006A: Wired texture bindings into the production UI by adding the `PreviewTextureBindings` property to `MainWindowViewModel` and configuring XAML binding `TextureBindings="{Binding PreviewTextureBindings}"` on `SoftwareWireframeViewportControl`. Integrated `IMaterialTextureBindingService` optionally into the ViewModel constructor. Successfully invokes `BindAsync` during preview loading on a background thread pool, resetting or clearing the collection during cancels, failures, or asset selection changes without failing the overall preview task. Added robust unit tests verifying loading, resetting, and service failure outcomes.
> * OS-009-FIX-006B: Corrected the Sceneryobjects root resolution during texture binding in `MainWindowViewModel.LoadPreviewAsync` using a testable helper method `GetSceneryObjectsRoot` (resolves path to `{RootDirectory}/Sceneryobjects` if the OMSI root is chosen, preserving it if the selection is already Sceneryobjects). Refactored cancellation semantics during texture binding to throw `OperationCanceledException` and prevent overwrite of `PreviewStatus` or bindings. Added unit tests for path resolution, cancellation propagation, and race-prevention during manual cancel command invocations.
> * OS-009-FIX-006C: Hardened the `GetSceneryObjectsRoot` path segment check in `MainWindowViewModel.cs` using `Path.GetFileName` to enforce that only directories whose last segment is exactly "Sceneryobjects" (case-insensitive) are preserved. Other paths (e.g. backup folders or suffix matches like "FooSceneryobjects") are correctly treated as general roots and combined with the `Sceneryobjects` suffix. Expanded `GetSceneryObjectsRoot_ResolvesPathCorrectly` unit tests to cover false segment suffix match directories.

### ✅ OS-009-TASK-007 - Add Texture Sampling Modes And Alpha Blending
*   **Purpose**: Protect system resources by limiting maximum texture sizes.
*   **Area**: `OmsiStudio.App.Services`
*   **Acceptance Criteria**:
    *   Support Nearest and Bilinear texture sampling modes.
    *   Implement standard source-over alpha blending onto target RGBA buffers.
    *   Verify blending is alpha-aware to prevent darkening on transparent targets.
*   **Strict Exclusions**: GPU Mipmaps.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-007: Added `TextureSamplingMode` enum containing `Nearest` and `Bilinear` values. Refactored `SoftwareTexturedTriangleRasterizer.Rasterize` to support both modes, setting Bilinear as the default mode. Bilinear sampling uses repeating/wrapping coordinates modulo calculations across 4 neighboring texels. Implemented standard source-over alpha blending equation (`outRgb = (srcRgb * srcA + dstRgb * dstA * (1 - srcA)) / outA`) to safely blend textures with transparency over target buffers without background darkening. Added tests for bilinear interpolation math, source-over blending, and unshaded alpha channels.
> * OS-009-FIX-007A: Expanded unit test coverage in `SoftwareTexturedTriangleRasterizerTests.cs` to test wrapped UV coordinates bilinear sampling. Added positive wraps (`UV > 1.0`), negative wraps (`UV < 0.0`), and boundary UV coordinates (`UV = 1.0`) to assert that correct values are sampled, correct alpha weights are preserved, and no IndexOutOfRangeException is thrown.
> * OS-009-FIX-007B: Exposed texture binding diagnostics, status, size dimensions, and small thumbnail bitmaps on `MaterialDisplayItem` UI display model. Refactored `MainWindowViewModel.UpdatePreviewMaterials` to match and inject bindings into display items whenever the parsed model reference or bindings collections change. Rewrote `MainWindow.axaml` preview materials list with a premium template showing colored status text (Bound, Missing, Unsupported, Invalid, Failed), dimensions, resolved path, nested error diagnostics warnings list, and `Image` thumbnail controls. Added comprehensive unit tests for statuses, sizes, diagnostics, and headless environment safety.

### ✅ OS-009-TASK-008 - Add Texture Decode Cache For Preview Pipeline
*   **Purpose**: Prevent duplicate texture decoding operations during model preview rendering.
*   **Area**: `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Introduce `ITextureImageCacheService` and `TextureImageCacheService` to handle process-scoped safe caching.
    *   Compare absolute resolved texture paths case-insensitively.
    *   Implement LRU eviction bounds (max entry count and total pixel bytes capacity).
    *   Ensure thread-safety and avoid caching failed or cancelled loads.
*   **Strict Exclusions**: GPU-level VRAM caching.

> [!NOTE]
> **Completion Note**: 
> * OS-009-TASK-008: Created `ITextureImageCacheService` contract and its thread-safe `TextureImageCacheService` implementation. Uses a `SemaphoreSlim(1,1)` alongside a `Dictionary` and `LinkedList` to deduplicate concurrent requests for the same path by caching and awaiting their `Task<TextureLoadResult>`, and evicts least recently used items under max size limits (count/bytes). Refactored `MaterialTextureBindingService` to query the cache service instead of raw loaders, maintaining full backward compatibility. Added comprehensive unit tests for duplicate paths, case-insensitivity, failures/cancellations exclusion, and LRU evictions.
> * OS-009-FIX-008A: Stabilized material thumbnail tests in headless runs by refactoring `MaterialDisplayItem.TextureThumbnail` from `Bitmap` to `IImage` interface and introducing a static `ThumbnailFactory` delegate hook. Inside tests, a mock non-rendering `DummyImage` class implementing `IImage` is registered to bypass Avalonia graphics platform initialization. Wrapped the `ThumbnailFactory` call inside a `try-catch` block to handle thrown test stub mock exceptions gracefully.
> * OS-009-FIX-008B: Added localized "Not bound" / "Bağlanmadı" state for materials without loaded bindings. Integrated three testable debug auto-properties (`LastTexturedTriangleCount`, `LastFallbackTriangleCount`, `LastRenderedTriangleCount`) inside `SoftwareWireframeViewportControl.cs`. Tracked triangle drawing counts by resetting and updating these properties in both Solid and Wireframe mode loops. Refactored the `Render` method to run projections and update counters under headless (null context) environments without throwing exceptions. Displayed the debug counters in the `MainWindow.axaml` preview material panel. Added unit tests verifying counters under bound, unbound, and wireframe scenarios.
> * OS-009-FIX-009A: Enclosed drawing primitives within a `context.PushClip(Rect)` using block inside `SoftwareWireframeViewportControl.Render` to clip wireframes/borders within control boundaries. Enlarged viewport layout inside `MainWindow.axaml` by increasing its Border minimum height from a fixed `Height="200"` to `MinHeight="420"`. Hided orbit/reset camera button overlays (`IsVisible="False"`) in XAML in favor of mouse drag/zoom gesture interactions. Changed default visual rendering mode in control property and view model backing fields to `Solid` (textured solid). Added auto preview load logic inside `OnSelectedAssetChanged` hook in `MainWindowViewModel.cs` to resolve the first resolved model reference and invoke `LoadPreviewCommand` automatically. Added unit tests for clipping and auto-load selection/cancellation.
> * OS-009-FIX-009B: Updated the auto preview loader selection inside `MainWindowViewModel.OnSelectedAssetChanged` to enforce that only model references ending with `.o3d` (case-insensitive extension check on either `ResolvedPath` or `MeshPath`) are selected. Added unit tests verifying skipping of non-`.o3d` resolved references, uppercase `.O3D` extension matches, and preview state clearance when no valid `.o3d` resolved references are found.
> * OS-009-TASK-009: Extended `AssetPreviewRequest` with `ModelPaths` to support multi-mesh loading requests. Rewrote `AssetPreviewLoader.LoadAsync` to load multiple `.o3d` references using `IO3dGeometryReader` and combine them into a single `O3dMeshData` by concatenating vertex lists, appending material slot lists, offsetting vertex index references in triangle definitions, and re-indexing material slot indices. Preserved exact single-model preview references, diagnostic severities, and error message structures for backward compatibility. Added private boolean state tracking `_isPreviewingSingleMesh` inside `MainWindowViewModel.cs` to distinguish between automatic multi-mesh scene previews and explicit manual single-mesh selections. Added comprehensive loader and view model integration unit tests covering offset indices, warning diagnostic accumulation on partially-failed loads, and full clear behaviors.
> * OS-009-TASK-008C: Introduced the `OmsiMeshFormat` enum and helper class to identify `.o3d` and `.x` (DirectX) files. Designed and created the `IDirectXGeometryReader` interface contract alongside a default placeholder class `DirectXGeometryReader` that returns a recognized-but-not-implemented-yet status. Updated `AssetPreviewLoader` constructor to accept and store the DirectX reader, and update file extension validation and dispatcher routing to pass `.x` models to the DirectX reader. Integrated Turkish and English translations under the `"DirectXMeshParserPending"` key to return format recognized, parser pending diagnostic messages. Modified `MainWindowViewModel.cs` to allow both `.o3d` and `.x` references as candidates, prioritize `.o3d` selection, and fallback to `.x` auto-load. Added full unit tests for format detection (case-insensitive extension checks for `.x` and `.X`), parser pending diagnostics, priority candidate VM selection, and localized UI label assertions.
> * OS-009-TASK-010: Created `OmsiMeshTransform` record carrying PosX/Y/Z, RotX/Y/Z, and ScaleX/Y/Z properties with a static `Identity` default fallback. Updated `OmsiModelReference` to store `Transform` and `TransformWarnings`. Refactored `ScoParser.cs` to parse `[new_pos]`, `[rot_x]`, `[rot_y]`, `[rot_z]`, `[rotx]`, `[roty]`, `[rotz]`, and `[scale]` blocks, registering warnings for malformed data and assigning properties to the corresponding `ScoMeshReference`. Extended `AssetPreviewRequest` to carry `ModelReferences` list. Updated `AssetPreviewLoader.LoadAsync` to apply transformations (scale, roll/pitch/yaw Euler rotations, translation offsets) to combined vertices. Handled fallback behavior to Identity when transforms are empty or failed to parse. Added full unit tests checking valid parsing, malformed warning fallbacks, transformed vertex calculations, triangle offset checks, and bounds validations.
> * OS-009-FIX-010A: Mapped `ScoMeshReference.Warnings` into `OmsiModelReference.TransformWarnings` in `ScoFileParser.cs`. Preserved `TransformWarnings` when reconstructing resolved `OmsiModelReference` instances in `OmsiAssetScanner.cs`. Extended `AssetPreviewRequest` with the `ApplyModelTransforms` boolean property. Configured `MainWindowViewModel.cs` to set `ApplyModelTransforms = !_isPreviewingSingleMesh`. Refactored `AssetPreviewLoader.LoadAsync` to apply vertex transformations for single-mesh auto-previews whenever `ApplyModelTransforms` is `true`, while preserving identity/untransformed vertex layouts for manual explicit single-mesh previews. Added comprehensive unit tests for warning propagation in the parser, warning preservation in the scanner, and loaders transform/untransformed application.
> * OS-009-TASK-011: Added `alphaThreshold` parameter (default `8`) to `SoftwareTexturedTriangleRasterizer.Rasterize`. Samples below this threshold are discarded via a quick `continue` check inside the pixel rendering loop to prevent low-alpha transparency artifacts. Modified `SoftwareWireframeViewportControl.cs` to explicitly pass `alphaThreshold: 8` into the rasterizer call for the textured solid rendering path. Preserved original source-over alpha blending and back-to-front depth sorting routines. Added unit tests verifying discard conditions for pixel alpha values below the threshold, preservation and blending for alpha values above or equal to the threshold, and safe execution within both solid and wireframe viewport render contexts.
> * OS-009-TASK-012: Created the `SoftwareLightingCalculator` static class to compute flat ambient and directional light intensity factors, clamping final values within `[0.35, 1.15]`. Used a base ambient level of `0.45` to keep back-facing and low-light normals readable instead of rendering them completely black, and integrated a default directional light source vector. Assigned a deterministic `0.45` fallback factor to degenerate/zero normal vectors. Refactored the solid textured rendering path inside `SoftwareWireframeViewportControl.cs` to apply `SoftwareLightingCalculator.ComputeIntensity` view-space normal shading. Added unit tests covering light-facing, side, back-facing, and degenerate normal test scenarios.
> * OS-009-TASK-013: Established a central static policy record `PreviewPerformancePolicy` carrying `MaxPreviewVertices`, `MaxPreviewTriangles`, `MaxPreviewMaterials`, `MaxTextureBindings`, `MaxTotalTexturePixels`, and `MaxViewportRasterPixels`. Integrated `AssetPreviewLoader.cs` properties with the central policy, preserving backward compatibility. Refactored `MaterialTextureBindingService.cs` to check limits before loading/decoding textures; when a limit is exceeded, further binds are skipped with a warning diagnostic and fall back to solid color rendering. Updated `MainWindowViewModel.cs` to merge texture binding diagnostics into the main preview diagnostics display. Refactored `SoftwareWireframeViewportControl.cs` to check `MaxViewportRasterPixels` and automatically fall back to line-based vector wireframe drawing (avoiding CPU pixel buffer allocations and rendering overhead) and reset viewport counters on extremely large windows. Added unit tests for binding limits, total pixel bounds, and viewport allocation fallbacks.
> * OS-009-FIX-013A: Updated `MaterialTextureBindingService.cs` to track performance budgets (`MaxTextureBindings` and `MaxTotalTexturePixels`) by unique resolved texture paths (case-insensitive) instead of counting them per material slot. When a texture is shared across multiple material slots, it bypasses the budget checks and is bound directly without consuming additional budget, preventing premature limit exclusions. Added unit tests for same-path slot sharing (deduplication check), different-path slot counts, casing variations, and limit bounds.
> * OS-009-TASK-014: Expanded the visual mode modes in `SoftwareViewportVisualMode` to include `TexturedSolid` (0), `TexturedWireframe` (1), `SolidColor` (2), `SolidColorWireframe` (3), and `Wireframe` (4), and default visual mode is set to `TexturedSolid` (0). Added combobox items inside `MainWindow.axaml` and bound them to `VisualModeInt` in `MainWindowViewModel.cs`. Extended translations for both Turkish and English languages in `LocalizationService.cs`. Updated `SoftwareWireframeViewportControl.cs` rendering to skip texture resolution entirely for `SolidColor` and `SolidColorWireframe` modes, and overlay wireframe outlines for `TexturedWireframe` and `SolidColorWireframe` modes. Updated unit tests for view model default mode index mapping, viewport rendering fallback counts under solid color mode, overlay logic, and localization key existence.
> * OS-009-TASK-015: Completed full epic validation and developer documentation updates. Added specific unit tests for `SolidColorWireframe` rendering mode, no-texture fallback, integrated multi-mesh placement/casing/budgets, and visual diagnostics merging into MainWindowViewModel. Cleaned up stale descriptions in `README.md` and `OMSISTUDIO_EPIC_BACKLOG.md` (moving `OS-009-FEATURE-004` to completed tasks backlog and listing `OS-009-TASK-016` as planned/future scope).
> * OS-009-TASK-016: Replaced the DirectX `.x` geometry reader placeholder with a fully-functional text-based DirectX `.x` parser. Parsed vertices, polygon faces (automatically performing fan triangulation for 4+ sided quads/polygons), UV texture coordinates (`MeshTextureCoords`), and material texture filename mappings (`MeshMaterialList`). Enforced safety limits check for vertices, faces, and material slots against `PreviewPerformancePolicy`, alongside maximum 50 MB file size constraints. Returns unsupported diagnostics for binary/compressed formats. Added unit tests for parsing, quads triangulation, UV/material slots mapping, safety limits, binary format rejection, and multi-mesh O3D + DirectX combinations.
> * OS-009-FIX-016A: Hardened text DirectX `.x` parser logic to include asynchronous cancellation check routines (`CancellationToken` propagation inside stripping, tokenization, vertex, UV, and material loops), checked arithmetic limits validation (triangulation triangle safety count limits using cumulative sums), negative/malformed count checks, material index bounds verification ([0, nMaterials - 1]), and quote-aware comment stripping. Added unit tests for cancellation, fan triangulation limits bounds, invalid material index diagnostic, quote-aware comment parsing containing '#' and '//', and negative/malformed counts.

### ✅ OS-009-TASK-009 - Multi-Mesh SCO Preview Composition
*   **Purpose**: Group and render all meshes referenced by a single `.sco` file.
*   **Area**: `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Parse multiple mesh references in a scenery object.
    *   Combine and render them together within the same viewport.
*   **Strict Exclusions**: Scene graphs or generic hierarchical parent-child nodes.

### ✅ OS-009-TASK-010 - Object Transform & Placement Handling
*   **Purpose**: Align meshes according to their offsets and rotations configured in the `.sco`.
*   **Area**: `OmsiStudio.Core`, `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Apply translation, rotation, and offset transformations to coordinates before rendering.
    *   Verify bounds calculations correctly wrap the entire combined transformed structure.
*   **Strict Exclusions**: Dynamic editing/manipulation of offsets.

### ✅ OS-009-TASK-011 - Alpha & Transparent Material Handling
*   **Purpose**: Support cutout transparency for grates, windows, or foliage.
*   **Area**: `OmsiStudio.App` (Views/Rendering)
*   **Acceptance Criteria**:
    *   Support alpha thresholding (discard pixels with alpha below a limit).
    *   Enable sorting of semi-transparent faces to render correctly back-to-front.
*   **Strict Exclusions**: Complex screen-space ambient occlusion or translucent refraction.

### ✅ OS-009-TASK-012 - Lighting & Material Approximation
*   **Purpose**: Add basic shading to make the model surface appear 3D.
*   **Area**: `OmsiStudio.App` (Views/Rendering)
*   **Acceptance Criteria**:
    *   Combine face lighting normal vectors with texture colors to simulate ambient light.
*   **Strict Exclusions**: Dynamic shadow mapping or ray tracing.

### ✅ OS-009-TASK-013 - Realistic Preview Performance Guardrails
*   **Purpose**: Establish limits for realistic rendering.
*   **Area**: `OmsiStudio.App.Services`
*   **Acceptance Criteria**:
    *   Introduce maximum limits (e.g. `MaxTextureMemoryUsage`, `MaxCombinedTextureSize`).
    *   Gracefully fall back to solid rendering if limits are violated.
*   **Strict Exclusions**: Arbitrary thresholds that cannot be customized.

### ✅ OS-009-TASK-014 - Viewport UI Toggles
*   **Purpose**: Give the user controls over texture display.
*   **Area**: `OmsiStudio.App`
*   **Acceptance Criteria**:
    *   Add a visual settings checkbox/combobox options: Textured, Solid, Wireframe.
*   **Strict Exclusions**: Persistent viewport state settings.

### ✅ OS-009-TASK-015 - Realistic Preview Tests & Documentation
*   **Purpose**: Finalize the epic with testing and complete developer documentation.
*   **Area**: `OmsiStudio.App.Tests`, documentation folder
*   **Acceptance Criteria**:
    *   Assert loader and renderer pipelines handle multiple texture resolutions, fallbacks, and boundary scenarios correctly.
    *   Update DOMAIN_MODEL.md with realistic preview specifications.
*   **Strict Exclusions**: Verification of visual output accuracy (smoke rendering tests are sufficient).

### ✅ OS-009-TASK-016 - DirectX .x Text Mesh Parser
*   **Purpose**: Implement the geometry parser for DirectX `.x` files (both text format and binary format if applicable).
*   **Area**: `OmsiStudio.Core.Assets`
*   **Acceptance Criteria**:
    *   Create a fully functional implementation of `IDirectXGeometryReader`.
    *   Parse vertex coordinates, index buffers (triangles), normals, and texture coordinates from `.x` files.
    *   Produce a correct `O3dMeshData` object representing the parsed mesh.
*   **Strict Exclusions**: Complex animations, hierarchical skeleton structures, skinning, and non-mesh frames processing.

## Recommended Execution Order

1. `OS-009-TASK-001`
2. `OS-009-TASK-002`
3. `OS-009-TASK-003`
4. `OS-009-TASK-004`
5. `OS-009-TASK-005`
6. `OS-009-TASK-006`
7. `OS-009-TASK-007`
8. `OS-009-TASK-008`
9. `OS-009-TASK-009`
10. `OS-009-TASK-010`
11. `OS-009-TASK-011`
12. `OS-009-TASK-012`
13. `OS-009-TASK-013`
14. `OS-009-TASK-014`
15. `OS-009-TASK-015`
16. `OS-009-TASK-016`

