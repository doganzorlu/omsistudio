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

Represents a reference to a 3D mesh model used by an asset, along with its resolution state on disk.

*   `MeshPath` (string): The filename/path of the mesh file as referenced in the `.sco` file.
*   `ResolvedPath` (string): The absolute path where the mesh was resolved on the local filesystem.
*   `Exists` (bool): Indicates if the mesh file exists on disk.
*   `ResolutionStatus` (OmsiModelReferenceResolutionStatus): The status of the path resolution.

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

