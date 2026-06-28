using System.Collections.Generic;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.App.Views;
using OmsiStudio.Core.Assets;
using Xunit;

namespace OmsiStudio.App.Tests;

public class SoftwareWireframeViewportControlTests
{
    [Fact]
    public void Control_DefaultProperties_AreInitializedCorrectly()
    {
        // Act
        var control = new SoftwareWireframeViewportControl();

        // Assert
        Assert.Null(control.MeshData);
        Assert.Equal(45f, control.CameraYaw);
        Assert.Equal(-30f, control.CameraPitch);
        Assert.Equal(5f, control.CameraDistance);
        Assert.False(control.ShowBoundingBox);
        Assert.Equal(SoftwareViewportVisualMode.Solid, control.VisualMode);
    }

    [Fact]
    public void Control_SetProperties_CanBeRetrieved()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 1f, Y = 2f, Z = 3f }
            }
        };

        // Act
        control.MeshData = mesh;
        control.CameraYaw = 90f;
        control.CameraPitch = 45f;
        control.CameraDistance = 10f;
        control.ShowBoundingBox = true;
        control.VisualMode = SoftwareViewportVisualMode.SolidWireframe;

        // Assert
        Assert.Same(mesh, control.MeshData);
        Assert.Equal(90f, control.CameraYaw);
        Assert.Equal(45f, control.CameraPitch);
        Assert.Equal(10f, control.CameraDistance);
        Assert.True(control.ShowBoundingBox);
        Assert.Equal(SoftwareViewportVisualMode.SolidWireframe, control.VisualMode);
    }

    [Fact]
    public void Control_WireframeVisualMode_IsPreserved()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();

        // Act
        control.VisualMode = SoftwareViewportVisualMode.Wireframe;

        // Assert
        Assert.Equal(SoftwareViewportVisualMode.Wireframe, control.VisualMode);
    }

    [Fact]
    public void Control_WireframeModeWithMeshData_CanBeAssignedSafely()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 0f, Y = 0f, Z = 0f },
                new O3dVertex { X = 1f, Y = 0f, Z = 0f },
                new O3dVertex { X = 0f, Y = 1f, Z = 0f }
            },
            Triangles = new List<O3dTriangle>
            {
                new O3dTriangle { V0 = 0, V1 = 1, V2 = 2, MaterialSlotIndex = 999 } // Invalid index
            }
        };

        // Act
        control.MeshData = mesh;
        control.VisualMode = SoftwareViewportVisualMode.Wireframe;

        // Assert
        Assert.Same(mesh, control.MeshData);
        Assert.Equal(SoftwareViewportVisualMode.Wireframe, control.VisualMode);
    }

    [Fact]
    public void Control_TextureBindings_CanBeSetAndRetrieved()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        var bindings = new List<MaterialTextureBinding>
        {
            new MaterialTextureBinding { MaterialIndex = 0, Status = TextureBindingStatus.Bound }
        };

        // Act
        control.TextureBindings = bindings;

        // Assert
        Assert.Same(bindings, control.TextureBindings);
    }

    [Fact]
    public void Render_NullContextNullMesh_DoesNotThrow()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        control.MeshData = null;

        // Act & Assert
        control.Render(null!); // should not throw since it exits early when MeshData is null
    }

    [Fact]
    public void Render_WithTextureBindings_IncrementsTexturedCounter()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        control.Arrange(new Avalonia.Rect(0, 0, 100, 100));

        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 0f, Y = 0f, Z = 0f, Uv = new O3dUv { U = 0f, V = 0f } },
                new O3dVertex { X = 1f, Y = 0f, Z = 0f, Uv = new O3dUv { U = 1f, V = 0f } },
                new O3dVertex { X = 0f, Y = 1f, Z = 0f, Uv = new O3dUv { U = 0f, V = 1f } }
            },
            Triangles = new List<O3dTriangle>
            {
                new O3dTriangle { V0 = 0, V1 = 1, V2 = 2, MaterialSlotIndex = 0 }
            },
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" }
            }
        };

        var bindings = new List<MaterialTextureBinding>
        {
            new MaterialTextureBinding
            {
                MaterialIndex = 0,
                Status = TextureBindingStatus.Bound,
                Image = new TextureImageData { Width = 2, Height = 2, PixelsRgba32 = new byte[2 * 2 * 4] }
            }
        };

        control.MeshData = mesh;
        control.TextureBindings = bindings;
        control.VisualMode = SoftwareViewportVisualMode.Solid;

        // Act
        control.Render(null!);

        // Assert
        Assert.Equal(1, control.LastRenderedTriangleCount);
        Assert.Equal(1, control.LastTexturedTriangleCount);
        Assert.Equal(0, control.LastFallbackTriangleCount);
    }

    [Fact]
    public void Render_WithoutTextureBindings_IncrementsFallbackCounter()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        control.Arrange(new Avalonia.Rect(0, 0, 100, 100));

        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 0f, Y = 0f, Z = 0f },
                new O3dVertex { X = 1f, Y = 0f, Z = 0f },
                new O3dVertex { X = 0f, Y = 1f, Z = 0f }
            },
            Triangles = new List<O3dTriangle>
            {
                new O3dTriangle { V0 = 0, V1 = 1, V2 = 2, MaterialSlotIndex = 0 }
            },
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" }
            }
        };

        control.MeshData = mesh;
        control.TextureBindings = null;
        control.VisualMode = SoftwareViewportVisualMode.Solid;

        // Act
        control.Render(null!);

        // Assert
        Assert.Equal(1, control.LastRenderedTriangleCount);
        Assert.Equal(0, control.LastTexturedTriangleCount);
        Assert.Equal(1, control.LastFallbackTriangleCount);
    }

    [Fact]
    public void Render_InWireframeMode_DoesNotUseTextureRasterizer()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        control.Arrange(new Avalonia.Rect(0, 0, 100, 100));

        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 0f, Y = 0f, Z = 0f },
                new O3dVertex { X = 1f, Y = 0f, Z = 0f },
                new O3dVertex { X = 0f, Y = 1f, Z = 0f }
            },
            Triangles = new List<O3dTriangle>
            {
                new O3dTriangle { V0 = 0, V1 = 1, V2 = 2, MaterialSlotIndex = 0 }
            },
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" }
            }
        };

        control.MeshData = mesh;
        control.VisualMode = SoftwareViewportVisualMode.Wireframe;

        // Act
        control.Render(null!);

        // Assert
        Assert.Equal(1, control.LastRenderedTriangleCount);
        Assert.Equal(0, control.LastTexturedTriangleCount);
        Assert.Equal(0, control.LastFallbackTriangleCount);
    }

    [Fact]
    public void Render_WithValidMesh_AppliesClippingWithoutThrowing()
    {
        // Arrange
        var control = new SoftwareWireframeViewportControl();
        control.Arrange(new Avalonia.Rect(0, 0, 100, 100));

        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex>
            {
                new O3dVertex { X = 0f, Y = 0f, Z = 0f },
                new O3dVertex { X = 1f, Y = 0f, Z = 0f },
                new O3dVertex { X = 0f, Y = 1f, Z = 0f }
            },
            Triangles = new List<O3dTriangle>
            {
                new O3dTriangle { V0 = 0, V1 = 1, V2 = 2 }
            }
        };
        control.MeshData = mesh;

        // Act & Assert
        var ex = Record.Exception(() => control.Render(null!));
        Assert.Null(ex);
    }
}
