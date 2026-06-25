# O3D Geometry Synthetic Fixtures

This directory contains synthetic `.o3d` files generated specifically for testing the O3D Geometry Pipeline in OmsiStudio.

## Core Rules & Provenance
- **100% Synthetic**: Every file in this folder was generated programmatically or via hex-editing in C#/Python test jigs. No copyrighted commercial files, OMSI assets, or commercial add-on resources were used.
- **Purpose**: These files are designed to validate vertex, triangle, and material slot parsing, stream bounds, safety thresholds, and error handling.
- **Layout Assumptions**:
  - **Version Header**: Uses Version 3 (starts with `3` as a 2-byte word, followed by a 2-byte `0`).
  - **Mesh Count**: 1 (unless testing limits).
  - **Vertex Count**: Safe number of vertex records (32 bytes per vertex: 3D float position, 3D float normal, 2D float UV).
  - **Triangle Count**: Safe number of triangle indices.
  - **Material Count**: Safe number of material reference strings.
  - **Material Strings**: Pascal-style string serialization (4-byte length prefix, followed by ASCII bytes).
  - **Vertex Block**: Vertex count * 32 bytes of float components.
  - **Face Block**: Triangle count * 8 bytes (each face is `<HHHH`: 3 x uint16 indices + 1 x uint16 material index).

## Fixture Catalog
- `minimal_valid_geometry.o3d`: A single valid submesh containing 3 vertices and 1 triangle with 1 material.
- `multi_triangle_geometry.o3d`: Contains 4 vertices forming 2 adjacent triangles.
- `invalid_index_geometry.o3d`: A malformed triangle index references vertex index `5` which is out-of-bounds for a mesh with only 3 vertices.
- `truncated_vertex_block.o3d`: Declares 3 vertices but the stream is cut short inside the vertex payload block.
- `truncated_face_block.o3d`: Declares 1 triangle but the stream ends before the face index block.
- `excessive_geometry_counts.o3d`: Declares 2,000,000 vertices and triangles to test memory exhaustion protection (DoS/safety-limits).
- `material_slot_geometry.o3d`: Contains 3 vertices, 1 triangle, and 2 distinct material slots with valid texture names.
