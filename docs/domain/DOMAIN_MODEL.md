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

### OmsiAssetType (Enum)

*   `Unknown = 0`: Fallback default value for unrecognized asset types.
*   `SceneryObject = 1`: Scenery object asset type.

### OmsiModelReference

Represents a reference to a 3D mesh model used by an asset.

*   `MeshPath` (string): The filename/path of the mesh file (typically `.o3d`).

### OmsiScanResult

Represents the complete outcome of directory scanning.

*   `DiscoveredAssets` (IReadOnlyList<OmsiAsset>): List of successfully found and parsed assets.
*   `Warnings` (IReadOnlyList<string>): List of non-fatal warnings captured during the scanning phase.
*   `Errors` (IReadOnlyList<string>): List of non-fatal errors encountered during scanning.
