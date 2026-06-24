# Synthetic O3D Fixture Files

All files in this directory are synthetic binary files generated programmatically for unit testing the O3D metadata parser pipeline of OmsiStudio.

They do not contain any copyrighted commercial OMSI scenery objects or addon data.

## Fixture Descriptions

1. `valid_v3_header.o3d`: A minimal, syntactically valid O3D Version 3 header with 100 vertices, 50 triangles, 1 material, and one texture reference ("texture.bmp").
2. `truncated_header.o3d`: A truncated O3D file containing only a few bytes of version info, terminating prematurely.
3. `dos_excessive_count.o3d`: An O3D header containing a vertex count value of 2,147,483,647 designed to test DoS allocation safety limits.
4. `invalid_string_bounds.o3d`: An O3D header with a string length prefix specifying 1000 bytes, which exceeds the actual remaining stream size.
5. `encrypted_marker.o3d`: An O3D file starting with a simulated encryption magic signature ("ENCR") to test the reader's capability to flag protected meshes.
