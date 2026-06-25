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
*   `Metadata` (O3dMetadata?): Optional parsed O3D metadata statistics.
*   `MetadataStatus` (O3dMetadataStatus): The outcome status of the metadata read.
*   `MetadataDiagnostics` (IReadOnlyList<O3dDiagnostic>): Diagnostics/warnings produced during metadata parsing.
*   `HasMetadata` (bool): Helper flag indicating if `Metadata` is not null.
*   `HasNoMetadata` (bool): Helper flag indicating if `Metadata` is null.
*   `HasMetadataDiagnostics` (bool): Helper flag indicating if there are warning/error diagnostics present.

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

## Coordinate System & Winding Decisions

*   **Raw Orientation**: Parsed geometry data is kept in its raw, original DirectX-space coordinate system (typically left-handed, Y-up/Z-forward). No coordinate transformations or basis modifications are performed at the format parser level.
*   **Winding Order**: Triangles preserve their raw winding order as defined in the binary `.o3d` face records.
*   **Decoupling**: Coordinate system transformations, scale adjustments, and winding-order flips (e.g. for glTF/right-handed conversions) are intentionally decoupled from the parser and will be handled during exporting/conversion in later phases.



