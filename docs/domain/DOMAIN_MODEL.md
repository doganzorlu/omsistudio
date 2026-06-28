# Domain Model

This document defines the core domain models used in OmsiStudio.

## Models

### OmsiAsset

Represents an individual discovered OMSI asset.

*   `DisplayName` (string): The display/friendly name of the asset. Defaults to the filename without extension if the friendlyname section is missing.
*   `AssetType` (OmsiAssetType): The category of the asset (e.g. SceneryObject).
*   `SourceScoPath` (string): The absolute path to the `.sco` file on the local filesystem.
*   `RelativePath` (string): The path to the file relative to the `Sceneryobjects` folder.
*   `Description` (string): The description extracted from the asset metadata.
*   `Groups` (IReadOnlyList<string>): The categorization/group hierarchy tags.
*   `ModelReferences` (IReadOnlyList<OmsiModelReference>): Mesh references associated with the asset.
*   `TextureReferences` (IReadOnlyList<string>): List of texture files referenced by the asset.

### OmsiAssetType (Enum)

*   `Unknown = 0`: Fallback default value for unrecognized asset types.
*   `SceneryObject = 1`: Scenery object asset type.

### OmsiModelReference

Represents a reference to a 3D mesh model used by an asset, along with its resolution state on disk and optional O3D metadata.

*   `MeshPath` (string): The filename/path of the mesh file as referenced in the `.sco` file.
*   `ResolvedPath` (string): The absolute path where the mesh was resolved on the local filesystem.
*   `Exists` (bool): Indicates if the mesh file exists on disk.
*   `ResolutionStatus` (OmsiModelReferenceResolutionStatus): The status of the path resolution.
*   `Transform` (OmsiMeshTransform): The parsed translation, rotation, and scale parameters associated with the mesh in the scenery object.
*   `TransformWarnings` (IReadOnlyList<string>): Warning messages generated during the parsing of this reference's transform block. These warnings are collected in `ScoFileParser`, preserved during resolution by `OmsiAssetScanner`, and reported as warning diagnostics during preview load by `AssetPreviewLoader`.
*   `Metadata` (O3dMetadata?): Optional parsed O3D metadata statistics.
*   `MetadataStatus` (O3dMetadataStatus): The outcome status of the metadata read.
*   `MetadataDiagnostics` (IReadOnlyList<O3dDiagnostic>): Diagnostics/warnings produced during metadata parsing.
*   `HasMetadata` (bool): Helper flag indicating if `Metadata` is not null.
*   `HasNoMetadata` (bool): Helper flag indicating if `Metadata` is null.
*   `HasMetadataDiagnostics` (bool): Helper flag indicating if there are warning/error diagnostics present.

### OmsiMeshTransform

Represents the placement transform parameters applied to model reference vertices during multi-mesh preview rendering.

*   `PosX`, `PosY`, `PosZ` (double): Translation offsets along the X, Y, and Z axes (in meters).
*   `RotX`, `RotY`, `RotZ` (double): Rotation angles around the X, Y, and Z axes (in degrees).
*   `ScaleX`, `ScaleY`, `ScaleZ` (double): Scale factors along the X, Y, and Z axes. Defaults to 1.0.
*   `Identity` (OmsiMeshTransform): Default static transform with zero offsets, zero rotations, and unit scale factors. Used as the identity fallback whenever transform parameters are missing or failed to parse.

### OmsiModelReferenceResolutionStatus (Enum)

*   `Unknown = 0`: Default unrecognized status.
*   `Resolved = 1`: The reference resolves to an existing file on disk.
*   `Missing = 2`: The reference maps to an allowed path but the file does not exist.
*   `InvalidPath = 3`: The reference is rejected due to path traversal or syntax errors.

### OmsiScanResult

Represents the complete outcome of directory scanning.

*   `DiscoveredAssets` (IReadOnlyList<OmsiAsset>): List of successfully found and parsed assets.
*   `Warnings` (IReadOnlyList<string>): List of non-fatal warnings captured during the scanning phase (including missing/invalid mesh references).
*   `Errors` (IReadOnlyList<string>): List of non-fatal errors encountered during scanning.

## Conversion Models

### ConversionTargetFormat (Enum)

*   `Unknown = 0`: Default unrecognized target format.
*   `Gltf = 1`: glTF 3D file format target.
*   `Obj = 2`: OBJ 3D file format target.
*   `ManifestOnly = 3`: Creates a manifest metadata list without performing mesh/texture export.

### ConversionStatus (Enum)

*   `Unknown = 0`: Default unrecognized conversion status.
*   `Pending = 1`: The conversion request is queued/pending.
*   `Succeeded = 2`: The conversion finished successfully.
*   `Failed = 3`: The conversion failed due to warnings/errors.
*   `Cancelled = 4`: The conversion was aborted by the user.

### ConversionRequest

Represents an instruction to convert an OMSI asset.

*   `Asset` (OmsiAsset): The source scenery object asset metadata.
*   `TargetOutputDirectory` (string): Absolute path to the folder where output files are created.
*   `TargetFormat` (ConversionTargetFormat): The requested target export format.

### ConversionResult

Represents the output details of a conversion execution.

*   `Status` (ConversionStatus): The final outcome status of the conversion.
*   `OutputFiles` (IReadOnlyList<string>): Absolute paths of files produced by the exporter.
*   `Warnings` (IReadOnlyList<string>): Non-blocking feedback messages generated during export.
*   `Errors` (IReadOnlyList<string>): Fatal error messages that caused the conversion to fail.

### ExportManifestReferenceKind (Enum)

*   `Unknown = 0`: Default unrecognized reference type.
*   `Mesh = 1`: Represents a 3D mesh model reference.
*   `Texture = 2`: Represents an image/texture reference.

### ExportManifestReference

Represents a single external resource/reference that will be processed or packaged during export.

*   `Path` (string): The path to the referenced resource.
*   `Kind` (ExportManifestReferenceKind): The kind of reference (Mesh, Texture, or Unknown).

### ExportManifest

Represents the metadata and resource references exported for a selected scenery object asset in manifest format.

*   `AssetDisplayName` (string): Friendly name of the scenery object asset.
*   `SourceScoPath` (string): Absolute path to the original source `.sco` file.
*   `RelativePath` (string): Path relative to the source library directory.
*   `TargetFormat` (ConversionTargetFormat): The format requested for the export request.
*   `GeneratedAtUtc` (DateTimeOffset): The timestamp indicating when the manifest was generated.
*   `Meshes` (IReadOnlyList<ExportManifestReference>): Collection of mesh references extracted for conversion.
*   `Textures` (IReadOnlyList<ExportManifestReference>): Collection of texture references extracted for conversion.
*   `Warnings` (IReadOnlyList<string>): Cumulative list of non-fatal warnings captured during building.

## O3D Metadata Models

### O3dFormatVersion (Enum)

*   `Unknown = 0`: Default unrecognized format version.
*   `Legacy = 1`: Legacy version of O3D format (Version < 3) utilizing short headers.
*   `Version3 = 2`: Version 3 of O3D format utilizing long headers.
*   `Version4 = 3`: Version 4 of O3D format utilizing long headers.

### O3dMetadataStatus (Enum)

*   `Unknown = 0`: Default unrecognized status.
*   `Success = 1`: Metadata read successfully.
*   `Unsupported = 2`: The version is unsupported.
*   `Encrypted = 3`: The O3D file is encrypted and requires keys.
*   `Invalid = 4`: The file headers are invalid/corrupt.
*   `Failed = 5`: Structural failure or file access issue.

### O3dTextureReference

Represents a texture reference embedded in the model slots.

*   `Path` (string): Embedded texture reference file name or path.

### O3dMetadata

Represents parsed header metadata statistics from an O3D file.

*   `Version` (O3dFormatVersion): The O3D file format version.
*   `RawVersion` (int): Numeric version value read directly from the O3D header when available.
*   `DisplayVersion` (string): UI-friendly version text preserving `RawVersion` for real O3D files.
*   `IsEncrypted` (bool): Indicates if the model file is encrypted.
*   `MeshCount` (int): Number of meshes/submeshes.
*   `VertexCount` (int): Count of vertices.
*   `TriangleCount` (int): Count of triangles/faces.
*   `MaterialCount` (int): Count of materials defined.
*   `TextureReferences` (IReadOnlyList<O3dTextureReference>): Embedded texture references.

### O3dDiagnosticSeverity (Enum)

*   `Unknown = 0`: Default unrecognized severity.
*   `Info = 1`: Informational trace message.
*   `Warning = 2`: Non-fatal warning indicating formatting or reference issues.
*   `Error = 3`: Fatal parsing error.

### O3dDiagnosticCode (Enum)

*   `Unknown = 0`: Default unrecognized diagnostic code.
*   `TruncatedStream = 1`: Unexpected end of stream.
*   `UnsupportedVersion = 2`: Version is unsupported.
*   `EncryptedFile = 3`: Model file encryption protection.
*   `InvalidHeader = 4`: Invalid header formatting.
*   `InvalidCount = 5`: Count statistics values are negative or invalid.
*   `StringLengthExceeded = 6`: String length exceeds configured limit.
*   `InvalidStringBounds = 7`: String length exceeds stream remainder size.
*   `ReadFailed = 8`: Binary stream read error.
*   `SafetyLimitExceeded = 9`: Allocation sizes exceed safe size thresholds.
*   `InvalidIndex = 10`: Out-of-bounds vertex index referenced by face.
*   `InvalidPath = 11`: Invalid or empty asset path.
*   `UnsupportedFormat = 12`: Unsupported file extension format.
*   `FileNotFound = 13`: File not found.
*   `LoadCancelled = 14`: Loading process cancelled.

### O3dDiagnostic

Represents a structured warning or error message generated during O3D metadata extraction.

*   `Severity` (O3dDiagnosticSeverity): The severity level (Info, Warning, or Error).
*   `Code` (O3dDiagnosticCode): The diagnostic code indicating the failure/warning classification.
*   `Message` (string): Context message describing the error.
*   `ByteOffset` (long?): Optional byte offset in the stream where the diagnostic occurred.
*   `Context` (string?): Optional variable state or debug metadata.

### O3dMetadataReadResult

Represents the output details of an O3D metadata reading execution.

*   `Metadata` (O3dMetadata?): Parsed O3D metadata (if successful).
*   `Status` (O3dMetadataStatus): Final outcome status.
*   `Diagnostics` (IReadOnlyList<O3dDiagnostic>): Cumulative list of structured parsing warnings, errors, or trace logs.

## O3D Metadata Services

### IO3dMetadataReader

Defines the service contract for parsing O3D model file header metadata asynchronously.

*   `ReadAsync(filePath, cancellationToken)`: Reads and parses version, encryption, and count metadata from the specified O3D file asynchronously, returning an `O3dMetadataReadResult`. Supports cooperative cancellation.

## O3D Geometry Models

### O3dGeometryStatus (Enum)

*   `Unknown = 0`: Default unrecognized geometry read status.
*   `Success = 1`: Geometry was read successfully.
*   `Unsupported = 2`: The O3D format version is unsupported by the geometry reader.
*   `Encrypted = 3`: The O3D file is encrypted and cannot be parsed.
*   `Invalid = 4`: The O3D geometry data is invalid or corrupted.
*   `Failed = 5`: The geometry read failed due to file system or structural failures.

### O3dUv

Represents a 2D texture coordinate (UV) in a 3D model.

*   `U` (float): Horizontal texture coordinate.
*   `V` (float): Vertical texture coordinate.

### O3dNormal

Represents a 3D normal vector.

*   `X` (float): X component of the normal vector.
*   `Y` (float): Y component of the normal vector.
*   `Z` (float): Z component of the normal vector.

### O3dVertex

Represents a vertex in a 3D model containing position coordinates, a normal vector, and UV texture coordinates.

*   `X` (float): X position coordinate of the vertex.
*   `Y` (float): Y position coordinate of the vertex.
*   `Z` (float): Z position coordinate of the vertex.
*   `Normal` (O3dNormal): Normal vector of the vertex.
*   `Uv` (O3dUv): Texture coordinate (UV) of the vertex.

### O3dMaterialSlot

Represents a material slot in an O3D model, mapping a material name to an optional texture reference path.

*   `MaterialName` (string): The name of the material.
*   `TextureReference` (string?): Optional texture reference path.

### O3dTriangle

Represents a triangle/face in an O3D model referencing three vertex indices and an optional material slot index.

*   `V0` (int): Index of the first vertex.
*   `V1` (int): Index of the second vertex.
*   `V2` (int): Index of the third vertex.
*   `MaterialSlotIndex` (int?): Optional index of the material slot assigned to this triangle.

### O3dMeshData

Represents the complete parsed mesh geometry data of an O3D model.

*   `Vertices` (IReadOnlyList<O3dVertex>): Collection of vertices.
*   `Triangles` (IReadOnlyList<O3dTriangle>): Collection of triangles/faces.
*   `MaterialSlots` (IReadOnlyList<O3dMaterialSlot>): Collection of material slots.
*   `Metadata` (O3dMetadata?): Optional header metadata associated with the mesh.

**Invariants & Safety Rules:**
*   **Collection Alignment**: If parsing is successful, actual collection counts must match the counts reported in the metadata header:
    *   `Metadata.VertexCount == Vertices.Count`
    *   `Metadata.TriangleCount == Triangles.Count`
    *   `Metadata.MaterialCount == MaterialSlots.Count`
*   **Index Bounds Validity**: 
    *   Every triangle vertex index (`V0`, `V1`, `V2`) must satisfy `0 <= index < Vertices.Count`.
    *   Every triangle `MaterialSlotIndex` must satisfy `0 <= index < MaterialSlots.Count`.
*   **Texture References**: `Metadata.TextureReferences` is populated from the texture paths inside `MaterialSlots`.
*   **Result Guarantees**: When `O3dGeometryReadResult.Status` is `Success`, `MeshData` must be non-null and all nested collections must be non-null. In any other state (e.g., `Invalid`, `Failed`), `MeshData` must be null.

### O3dGeometryReadResult

Represents the output details of an O3D geometry reading execution.

*   `MeshData` (O3dMeshData?): Parsed mesh geometry data (if successful).
*   `Status` (O3dGeometryStatus): Final outcome status.
*   `Diagnostics` (IReadOnlyList<O3dDiagnostic>): Cumulative list of warnings, errors, or diagnostics generated during parsing.

## O3D Geometry Services

### IO3dGeometryReader

Defines the service contract for parsing O3D model file geometry asynchronously.

*   `ReadAsync(filePath, cancellationToken)`: Reads and parses version, encryption, vertex, face, and material geometry data from the specified O3D file asynchronously, returning an `O3dGeometryReadResult`. Supports cooperative cancellation.

## DirectX Geometry Services

### IDirectXGeometryReader

Defines the service contract for parsing DirectX .x model file geometry asynchronously.

*   `ReadAsync(filePath, cancellationToken)`: Reads and parses vertices, faces (triangulating 4+ sided polygons via fan triangulation), UV texture coordinates, and material texture filename mappings from text-based DirectX .x files. Returns an `O3dGeometryReadResult`.
*   **Safety Limits**: Implements performance constraints (vertex, face, material count checks) based on `PreviewPerformancePolicy` using checked arithmetic (measuring generated triangle count limits rather than raw face count) and file size guardrails (maximum 50 MB) to prevent denial-of-service/allocations crash.
*   **Count & Index Validation**: Asserts that all counts are non-negative, faces have at least 3 vertices, and material list assignments point to valid slot indices within range `[0, nMaterials - 1]`.
*   **Quote-Aware Stripping**: Employs quote-aware comment stripping to preserve comment characters (`//` and `#`) when they reside inside quoted texture filenames.
*   **Cancellation Support**: Propagates cooperative cancellation requests as `OperationCanceledException` at early exit points and during parsing loops.
*   **Unsupported Formats**: Rejects binary or compressed DirectX .x formats and returns an `UnsupportedFormat` diagnostic.
*   **Exclusions**: Skeletal animations, bone weights, hierarchical frame transforms, and advanced PBR materials are out of scope.

## Coordinate System & Winding Decisions

*   **Raw Orientation**: Parsed geometry data is kept in its raw, original DirectX-space coordinate system (typically left-handed, Y-up/Z-forward). No coordinate transformations or basis modifications are performed at the format parser level.
*   **Winding Order**: Triangles preserve their raw winding order as defined in the binary `.o3d` face records.
*   **Decoupling**: Coordinate system transformations, scale adjustments, and winding-order flips (e.g. for glTF/right-handed conversions) are intentionally decoupled from the parser and will be handled during exporting/conversion in later phases.

## 3D Asset Preview Models

These models define the state and options of the interactive 3D asset preview system. They are designed to be completely independent of any specific rendering framework (such as OpenGL/Silk.NET) or Avalonia UI controls.

### PreviewVector3D
Represents a basic 3D point or offset vector in the preview domain.
*   `X` (float): X coordinate.
*   `Y` (float): Y coordinate.
*   `Z` (float): Z coordinate.

### MeshBounds
Represents the axis-aligned bounding box (AABB) of a mesh.
*   `Min` (PreviewVector3D): The minimum bounding corner.
*   `Max` (PreviewVector3D): The maximum bounding corner.
*   `Center` (PreviewVector3D): The computed center point of the mesh.
*   `Size` (PreviewVector3D): The total width, height, and depth dimensions.

### AssetPreviewStatus (Enum)
Represents the status of the preview loader and viewport.
*   `Unknown = 0`: Default/unknown state.
*   `Idle`: Viewport is empty, no asset selected.
*   `Loading`: Geometry is being parsed or buffered.
*   `Success`: Viewport is rendering successfully.
*   `Missing`: The `.o3d` file reference was not found.
*   `Unsupported`: Version header is unsupported.
*   `Encrypted`: Encrypted marker was detected.
*   `Invalid`: Safety parsing check failed.
*   `Failed`: General parsing or rendering failure.
*   `Cancelled`: Loading process was cancelled by the user.

### AssetPreviewRequest
Represents an immutable request to parse and display one or more meshes.
*   `AssetId` (string): The identifier of the scenery object.
*   `ModelPath` (string): The path of the target `.o3d` mesh.
*   `ModelPaths` (IReadOnlyList<string>): The list of resolved `.o3d` model paths to be combined into a multi-mesh preview.

### AssetPreviewResult
Represents the outcome of a preview load request.
*   `Status` (AssetPreviewStatus): Load outcome status.
*   `MeshData` (O3dMeshData?): Parsed mesh data (on success).
*   `Bounds` (MeshBounds?): Computed mesh bounding box (on success).
*   `Diagnostics` (IReadOnlyList<O3dDiagnostic>): Cumulative warnings or errors.

### PreviewCameraState
Represents the configuration of the orbital preview camera.
*   `Yaw` (float): Orbital rotation angle (default: 45°).
*   `Pitch` (float): Orbital elevation angle (default: -30°).
*   `Distance` (float): Zoom distance from the center (default: 5 units).
*   `PanOffset` (PreviewVector3D): Vector offset for camera panning (reserved for future scope).

### PreviewRenderOptions
Represents user settings for the 3D viewport.
*   `WireframeEnabled` (bool): Toggles wireframe view.
*   `BoundingBoxEnabled` (bool): Toggles rendering of the AABB.
*   `MaterialPreviewEnabled` (bool): Toggles color/texture mapping (default: true).

## 3D Asset Preview Services

### IAssetPreviewLoader
Defines the contract for asynchronously loading 3D asset previews from selected scenery object models.
*   `LoadAsync(request, cancellationToken)`: Triggers O3D mesh reading, computes safe bounds, collects diagnostics, and returns an `AssetPreviewResult`. Supports cooperative cancellation.
*   **Multi-Mesh Composition**: When multiple model paths are requested, `IAssetPreviewLoader` parses each valid `.o3d` file, combines their vertices, triangles, and material slots in order, applying correct vertex/material offsets and triangle re-indexing. If at least one mesh succeeds, the combined model is previewed successfully and failures are reported as warning diagnostics.

### IMeshBoundsCalculator
Defines the contract for calculating the axis-aligned bounding box (AABB) of mesh geometry.
*   `CalculateBounds(meshData)`: Calculates the minimum/maximum coordinates, center, and dimensions of a mesh using checked iterations over the vertices.
*   **Coordinate Preservation**: Raw DirectX-space coordinates ($X$, $Y$, $Z$) are preserved without conversions during calculation to ensure layout-level accuracy.
*   **Empty Verification**: Empty vertex arrays safely fall back to zeroed coordinates without throwing exceptions. Null data requests trigger controlled argument validation checks.

## Software 3D Viewport Options

### SoftwareViewportVisualMode
Defines the software viewport visual inspection modes:
*   `TexturedSolid` (Default): Renders flat-shaded polygon faces using bound textures where available, or fallback solid colors.
*   `TexturedWireframe`: Combines textured/fallback solid rendering with overlaid wireframe outlines.
*   `SolidColor`: Renders flat-shaded polygon faces using solid material colors only, ignoring all texture bindings.
*   `SolidColorWireframe`: Combines solid color rendering with overlaid wireframe outlines.
*   `Wireframe`: Renders only face edges as lines without filling faces or utilizing texture rasterization.

### MaterialDisplayItem
A UI-safe model presenting localized metadata of a material slot:
*   `MaterialName` (string): The material name or fallback identifier.
*   `TextureReference` (string): Resolved texture filename or localized missing text indicator.
*   `ColorBrush` (IBrush): Deterministic preview brush computed by hashing the texture path or slot index.

## Preview Performance Guardrails
To prevent UI blockages and excessive memory consumption during software rendering of massive models, the system implements central performance limits defined in `PreviewPerformancePolicy`:
*   `MaxPreviewVertices` (int): Maximum vertices allowed for rendering (default: 100,000). Exceeding causes the preview to be skipped, returning `AssetPreviewStatus.Unsupported` with a `SafetyLimitExceeded` diagnostic.
*   `MaxPreviewTriangles` (int): Maximum triangle faces allowed for rendering (default: 100,000). Exceeding causes the preview to be skipped, returning `AssetPreviewStatus.Unsupported` with a `SafetyLimitExceeded` diagnostic.
*   `MaxPreviewMaterials` (int): Maximum material slots allowed (default: 100). Exceeding causes the preview to be skipped, returning `AssetPreviewStatus.Unsupported` with a `SafetyLimitExceeded` diagnostic.
*   `MaxTextureBindings` (int): Maximum unique successfully bound resolved texture paths (case-insensitive, default: 50). Exceeding triggers a warning diagnostic, skipping subsequent bindings and falling back to solid material color rendering. Material slots sharing identical texture files do not consume extra bindings budget.
*   `MaxTotalTexturePixels` (long): Maximum total decoded texture pixels (sum of width * height across all unique resolved textures, default: 8192 * 8192). Exceeding triggers a warning diagnostic, skipping subsequent texture decodes and falling back to solid color rendering. Shared textures are counted only once.
*   `MaxViewportRasterPixels` (int): Maximum viewport rasterizer pixel area (width * height, default: 3840 * 2160). Exceeding limits solid/textured rendering to avoid huge memory allocations, automatically falling back to wireframe-only line vector drawing in the viewport.

## OMSI Texture Resolution Services

### IOmsiTextureReferenceResolver
Defines the contract for recursively resolving texture file paths from O3D model references inside the OMSI directory structure.
*   `Resolve(texturePath, modelFilePath, sceneryObjectsRoot)`: Performs case-insensitive, structured search for texture names, validates them against path traversal restrictions, and returns an `OmsiTextureReference`.

### OmsiTextureReference
Represents the result of a texture path resolution:
*   `TexturePath` (string): Original referenced texture filename.
*   `ResolvedPath` (string?): Absolute resolved file path if valid.
*   `Exists` (bool): True if the resolved file actually exists on the disk.
*   `ResolutionStatus` (OmsiTextureReferenceResolutionStatus): The status outcome of resolution.

### OmsiTextureReferenceResolutionStatus
Specifies the enum status of resolution:
*   `Unknown`: Default unresolved state.
*   `Resolved`: Safely located and verified.
*   `Missing`: Path resolved structurally but file does not exist.
*   `InvalidPath`: File path escapes allowed scopes or traversal check failed.

## Texture Image Decoding Services

### ITextureImageLoader
Defines the contract for asynchronously loading and decoding 3D texture image files into raw pixel bytes.
*   `LoadAsync(filePath, cancellationToken)`: Asynchronously opens, parses, and decodes image formats (BMP, PNG, JPEG), returns a `TextureLoadResult`, and implements safety policies (dimension checks, format validation).

### TextureImageData
Represents the raw, decoded 32-bit RGBA pixel buffers of a texture:
*   `Width` (int): Width in pixels.
*   `Height` (int): Height in pixels.
*   `Format` (TextureImageFormat): The format category of the decoded image.
*   `PixelsRgba32` (byte[]): The raw 8-bit RGBA pixel byte array of size Width * Height * 4.

### TextureLoadStatus
Specifies the outcome status of the loading operation:
*   `Success`: Decoded successfully.
*   `UnsupportedFormat`: Format detection (e.g. DDS, TGA) mapped it as unsupported.
*   `Invalid`: File header or magic number verification failed.
*   `TooLarge`: Dimensions exceed maximum allowed threshold (4096).
*   `Failed`: File not found or general read exception.

### TextureImageFormat
Specifies the image format:
*   `Bmp`, `Png`, `Jpeg`, `Tga`, `Dds`, `Unknown`.

## Material-to-Texture Binding Services

### IMaterialTextureBindingService
Defines the contract for matching parsed O3D material slots to their resolved files and loaded/decoded images.
*   `BindAsync(meshData, modelFilePath, sceneryObjectsRoot, cancellationToken)`: Maps each O3dMaterialSlot, calls the path resolver and image loader services, propagates diagnostics, and formats binding results.

### MaterialTextureBinding
Represents the mapping and resolution binding state of an O3D material slot:
*   `MaterialIndex` (int): Zero-based index of the material slot.
*   `MaterialName` (string): Name of the material slot.
*   `TextureReference` (string): Original referenced texture filename.
*   `ResolvedTexture` (OmsiTextureReference?): Resolved texture path details.
*   `Image` (TextureImageData?): Loaded image payload on success.
*   `Status` (TextureBindingStatus): Outcome status of the binding.
*   `Diagnostics` (IReadOnlyList<O3dDiagnostic>): Diagnostics tagged with material context headers.

### TextureBindingStatus
Specifies the outcome status of the binding operation:
*   `Bound`, `Missing`, `Unsupported`, `Invalid`, `TooLarge`, `Failed`, `Unknown`.

## Software Textured Triangle Rasterizer

### SoftwareLightingCalculator
A CPU-side shading utility that calculates face normal lighting intensity factors for the viewport rendering loop:
*   **Ambient Light Contribution**: A base ambient level of `0.45` prevents back-facing or unlit surfaces from becoming completely black, keeping all geometry readable.
*   **Directional Light Contribution**: Employs a flat directional light vector (pointing towards top-front-right) to calculate diffuse dot products (`normal · lightDir`) contributing up to `0.55`.
*   **Clamp Bounds**: Final shading intensity is mathematically clamped to `[0.35, 1.15]` to ensure readable highlights and shadows without overexposure.
*   **Degenerate Fallback**: Fallback value of `0.45` (ambient base) is deterministically assigned to zero or near-zero normal vectors to prevent rendering division errors.

### SoftwareTexturedTriangleRasterizer
A UI-independent, static CPU-side utility class that performs textured triangle rasterization onto target RGBA byte buffers.
*   **Texture Sampling Modes**: 
    *   `Nearest`: Deterministic closest-texel mapping, matching original low-overhead rasterization.
    *   `Bilinear`: Standard 2x2 texel grid linear interpolation mapping that prevents blocky pixelation at close angles.
*   **Source-Over Alpha Blending**: Blends the sampled texture output with the destination target buffer colors using standard non-premultiplied source-over equations:
    *   `outA = srcA + dstA * (1 - srcA)`
    *   `outRgb = (srcRgb * srcA + dstRgb * dstA * (1 - srcA)) / outA`
    *   Eliminates black rendering shadows when writing semi-transparent layers over uninitialized transparent targets.
*   **Wrap-Around Wrapping**: Repeating UV coordinate texture limits wrapper modulo calculation ensures seamless tiling mapping.
*   **Intensity Shading**: Color shading multiplier is applied only to target color channels (RGB) preserving alpha values.
*   **Safeties**: Excludes degenerate triangles, clips viewport out-of-bounds scanlines, and prevents crashes on malformed inputs.
*   **Alpha Thresholding / Cutout**: Discards sampled pixels whose alpha values fall below a configurable threshold (defaults to `8/255`) to prevent low-alpha artifacts on transparent cutout surfaces (such as grates, window panes, and foliage). Pixels with alpha values at or above the threshold are blended onto the destination buffer using standard source-over blending.

## Software Viewport Texture Binding Integration
The `SoftwareWireframeViewportControl` optionally binds and renders texture images onto solid model faces:
*   **Property Model**: Exposes a `TextureBindings` dependency property representing the list of mapped `MaterialTextureBinding` results.
*   **Render Pipeline**: When `VisualMode` is set to `Solid` or `SolidWireframe`, a back-to-front depth-sorted loop evaluates each triangle:
    *   If a texture is successfully bound and loaded, `SoftwareTexturedTriangleRasterizer` is called with UV mapping coordinates.
    *   If no texture is present or is invalid, the control falls back to rendering flat-shaded material colors.
*   **Overlay**: Wireframe lines are layered on top of the textured rasterization output in `SolidWireframe` mode.

### Texture Decode Cache Policy
To prevent redundant memory allocation and CPU cycles when multiple model materials reference identical texture sheets, a process-scoped caching layer is integrated:
*   **Deduplication**: `ITextureImageCacheService` captures and shares in-progress load/decode `Task` instances. Concurrent binding requests for the same image await the same single decode operation.
*   **Case-Insensitivity**: Cache entries are keyed using absolute resolved paths compared case-insensitively.
*   **Eviction Guards**: Memory footprint is constrained using an LRU (Least Recently Used) cache policy governed by `MaxCachedCount` (default: 50) and `MaxTotalPixelBytes` (default: 128MB). Exceeding these bounds evicts the oldest items.
*   **Fault-Tolerant Caching**: Successful decoding, unsupported formats, and too-large limits are cached since their file state remains static. Failed operations or cancellations are excluded from the cache to ensure clean subsequent retries.

### ViewModel Preview Texture Binding Flow
To support realistic model previewing, texture bindings are managed at the presentation level:
*   **Data Binding**: `MainWindowViewModel` exposes a reactive `PreviewTextureBindings` property bound directly to the viewport's `TextureBindings` property in XAML.
*   **Asynchronous Resolution**: Upon successfully parsing a 3D mesh inside the background thread pool during `LoadPreviewAsync`, the ViewModel calls `IMaterialTextureBindingService.BindAsync(...)` to resolve, validate, and load texture image format payloads.
*   **Safe Failure Policy**: Binding issues (e.g. missing files, unsupported formats) propagate diagnostics but never crash or fail the preview loader execution; unmapped or failed textures fallback safely to standard material color values.
*   **Resource Cleardown**: Whenever a preview task is cancelled, fails, or the active asset selection resets/changes, the ViewModel resets the collection to prevent memory leaks.



