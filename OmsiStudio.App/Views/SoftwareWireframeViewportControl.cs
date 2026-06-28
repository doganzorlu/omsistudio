using System;
using System.Numerics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OmsiStudio.App.Services;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.App.ViewModels;
using OmsiStudio.Core.Assets;
using System.Collections.Generic;
namespace OmsiStudio.App.Views;

/// <summary>
/// A custom software wireframe viewport control that projects and renders 3D meshes using Avalonia DrawingContext.
/// </summary>
public class SoftwareWireframeViewportControl : Control
{
    private float[]? _fitVertices;
    private float _scale = 1f;
    private float _offsetX;
    private float _offsetY;
    private float _offsetZ;
    private float _minX, _minY, _minZ;
    private float _maxX, _maxY, _maxZ;
    private O3dMeshData? _lastMeshData;
    private WriteableBitmap? _renderBitmap;

    private Point _lastPointerPosition;
    private bool _isDragging;

    static SoftwareWireframeViewportControl()
    {
        AffectsRender<SoftwareWireframeViewportControl>(
            MeshDataProperty,
            CameraYawProperty,
            CameraPitchProperty,
            CameraDistanceProperty,
            ShowBoundingBoxProperty,
            VisualModeProperty,
            TextureBindingsProperty);

        MeshDataProperty.Changed.AddClassHandler<SoftwareWireframeViewportControl>((x, e) => x.OnMeshDataPropertyChanged());
    }

    private void OnMeshDataPropertyChanged()
    {
        UpdateFitVertices();
    }

    private void UpdateFitVertices()
    {
        var mesh = MeshData;
        if (mesh == null)
        {
            _fitVertices = null;
            _lastMeshData = null;
            return;
        }

        if (_lastMeshData == mesh)
        {
            return;
        }

        _lastMeshData = mesh;
        if (mesh.Vertices != null && mesh.Vertices.Count > 0)
        {
            var (fit, s, ox, oy, oz) = MeshFitter.FitToClipSpace(mesh.Vertices);
            _fitVertices = fit;
            _scale = s;
            _offsetX = ox;
            _offsetY = oy;
            _offsetZ = oz;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var v in mesh.Vertices)
            {
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            _minX = minX; _minY = minY; _minZ = minZ;
            _maxX = maxX; _maxY = maxY; _maxZ = maxZ;
        }
        else
        {
            _fitVertices = null;
        }
    }

    /// <summary>
    /// Defines the <see cref="MeshData"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<O3dMeshData?> MeshDataProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, O3dMeshData?>(nameof(MeshData));

    /// <summary>
    /// Gets or sets the 3D mesh data to render.
    /// </summary>
    public O3dMeshData? MeshData
    {
        get => GetValue(MeshDataProperty);
        set => SetValue(MeshDataProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="CameraYaw"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<float> CameraYawProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, float>(nameof(CameraYaw), defaultValue: 45f, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Gets or sets the current camera orbit yaw angle.
    /// </summary>
    public float CameraYaw
    {
        get => GetValue(CameraYawProperty);
        set => SetValue(CameraYawProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="CameraPitch"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<float> CameraPitchProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, float>(nameof(CameraPitch), defaultValue: -30f, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Gets or sets the current camera orbit pitch angle.
    /// </summary>
    public float CameraPitch
    {
        get => GetValue(CameraPitchProperty);
        set => SetValue(CameraPitchProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="CameraDistance"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<float> CameraDistanceProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, float>(nameof(CameraDistance), defaultValue: 5f, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Gets or sets the current camera zoom/distance.
    /// </summary>
    public float CameraDistance
    {
        get => GetValue(CameraDistanceProperty);
        set => SetValue(CameraDistanceProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ShowBoundingBox"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowBoundingBoxProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, bool>(nameof(ShowBoundingBox), defaultValue: false);

    /// <summary>
    /// Gets or sets whether to display the AABB bounding box.
    /// </summary>
    public bool ShowBoundingBox
    {
        get => GetValue(ShowBoundingBoxProperty);
        set => SetValue(ShowBoundingBoxProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="VisualMode"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<SoftwareViewportVisualMode> VisualModeProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, SoftwareViewportVisualMode>(
            nameof(VisualMode), defaultValue: SoftwareViewportVisualMode.Solid);

    /// <summary>
    /// Gets or sets the visual mode for rendering (Wireframe, Solid, or Solid + Wireframe).
    /// </summary>
    public SoftwareViewportVisualMode VisualMode
    {
        get => GetValue(VisualModeProperty);
        set => SetValue(VisualModeProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="TextureBindings"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<MaterialTextureBinding>?> TextureBindingsProperty =
        AvaloniaProperty.Register<SoftwareWireframeViewportControl, IReadOnlyList<MaterialTextureBinding>?>(nameof(TextureBindings));

    /// <summary>
    /// Gets or sets the texture bindings mapping.
    /// </summary>
    public IReadOnlyList<MaterialTextureBinding>? TextureBindings
    {
        get => GetValue(TextureBindingsProperty);
        set => SetValue(TextureBindingsProperty, value);
    }

    public static readonly DirectProperty<SoftwareWireframeViewportControl, int> LastTexturedTriangleCountProperty =
        AvaloniaProperty.RegisterDirect<SoftwareWireframeViewportControl, int>(
            nameof(LastTexturedTriangleCount),
            o => o.LastTexturedTriangleCount);

    private int _lastTexturedTriangleCount;
    public int LastTexturedTriangleCount
    {
        get => _lastTexturedTriangleCount;
        private set => SetAndRaise(LastTexturedTriangleCountProperty, ref _lastTexturedTriangleCount, value);
    }

    public static readonly DirectProperty<SoftwareWireframeViewportControl, int> LastFallbackTriangleCountProperty =
        AvaloniaProperty.RegisterDirect<SoftwareWireframeViewportControl, int>(
            nameof(LastFallbackTriangleCount),
            o => o.LastFallbackTriangleCount);

    private int _lastFallbackTriangleCount;
    public int LastFallbackTriangleCount
    {
        get => _lastFallbackTriangleCount;
        private set => SetAndRaise(LastFallbackTriangleCountProperty, ref _lastFallbackTriangleCount, value);
    }

    public static readonly DirectProperty<SoftwareWireframeViewportControl, int> LastRenderedTriangleCountProperty =
        AvaloniaProperty.RegisterDirect<SoftwareWireframeViewportControl, int>(
            nameof(LastRenderedTriangleCount),
            o => o.LastRenderedTriangleCount);

    private int _lastRenderedTriangleCount;
    public int LastRenderedTriangleCount
    {
        get => _lastRenderedTriangleCount;
        private set => SetAndRaise(LastRenderedTriangleCountProperty, ref _lastRenderedTriangleCount, value);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _lastPointerPosition = e.GetPosition(this);
            _isDragging = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _lastPointerPosition;
            _lastPointerPosition = currentPosition;

            float yawSensitivity = 0.5f;
            float pitchSensitivity = 0.5f;

            CameraYaw = CameraDeltaCalculator.CalculateYaw(CameraYaw, (float)delta.X, yawSensitivity);
            CameraPitch = CameraDeltaCalculator.CalculatePitch(CameraPitch, (float)delta.Y, pitchSensitivity);

            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        float zoomSensitivity = 1.0f;
        CameraDistance = CameraDeltaCalculator.CalculateDistance(CameraDistance, (float)e.Delta.Y, zoomSensitivity);
        e.Handled = true;
    }

    private Vector3 Project(float x, float y, float z, Matrix4x4 matrix, float centerX, float centerY, float viewportScale)
    {
        var v3 = new Vector3(x, y, z);
        var transformed = Vector3.Transform(v3, matrix);

        // NDC -> viewport coordinates
        float screenX = centerX + transformed.X * viewportScale;
        float screenY = centerY - transformed.Y * viewportScale;
        return new Vector3(screenX, screenY, transformed.Z);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        int texturedCount = 0;
        int fallbackCount = 0;
        int renderedCount = 0;

        void ScheduleDebugCountersUpdate()
        {
            if (VisualRoot != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    LastTexturedTriangleCount = texturedCount;
                    LastFallbackTriangleCount = fallbackCount;
                    LastRenderedTriangleCount = renderedCount;
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
            else
            {
                LastTexturedTriangleCount = texturedCount;
                LastFallbackTriangleCount = fallbackCount;
                LastRenderedTriangleCount = renderedCount;
            }
        }

        // 1. Draw premium slate background
        context?.FillRectangle(new SolidColorBrush(Color.Parse("#1a1a1e")), new Rect(0, 0, Bounds.Width, Bounds.Height));

        var mesh = MeshData;
        if (mesh == null || mesh.Vertices == null || mesh.Vertices.Count == 0 || _fitVertices == null)
        {
            if (context != null)
            {
                // Draw placeholder text
                string placeholder = "No geometry to preview";
                if (DataContext is MainWindowViewModel vm)
                {
                    placeholder = vm.L["PreviewNoGeometry"];
                }

                var text = new FormattedText(
                    placeholder,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Arial"),
                    13,
                    Brushes.Gray);

                double x = (Bounds.Width - text.Width) / 2;
                double y = (Bounds.Height - text.Height) / 2;
                context.DrawText(text, new Point(x, y));
            }
            ScheduleDebugCountersUpdate();
            return;
        }

        using (context != null ? context.PushClip(new Rect(0, 0, Bounds.Width, Bounds.Height)) : default)
        {
            // Calculate transformation
            var cameraState = new PreviewCameraState
            {
                Yaw = CameraYaw,
                Pitch = CameraPitch,
            Distance = CameraDistance
        };

        var cameraMatrix = CameraTransformCalculator.Calculate(cameraState);

        float centerX = (float)Bounds.Width / 2f;
        float centerY = (float)Bounds.Height / 2f;
        float viewportScale = Math.Min((float)Bounds.Width, (float)Bounds.Height) / 2f;

        // 2. Project vertices
        Vector3[] projectedPoints = new Vector3[mesh.Vertices.Count];
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            float fitX = _fitVertices[i * 3];
            float fitY = _fitVertices[i * 3 + 1];
            float fitZ = _fitVertices[i * 3 + 2];
            projectedPoints[i] = Project(fitX, fitY, fitZ, cameraMatrix, centerX, centerY, viewportScale);
        }

        var wireframePen = new Pen(new SolidColorBrush(Color.Parse("#6366f1")), 1.0);

        if (VisualMode == SoftwareViewportVisualMode.Wireframe)
        {
            // 3. Draw wireframe faces directly without sorting
            foreach (var tri in mesh.Triangles)
            {
                if (tri.V0 < projectedPoints.Length && tri.V1 < projectedPoints.Length && tri.V2 < projectedPoints.Length)
                {
                    renderedCount++;
                    var p0 = new Point(projectedPoints[tri.V0].X, projectedPoints[tri.V0].Y);
                    var p1 = new Point(projectedPoints[tri.V1].X, projectedPoints[tri.V1].Y);
                    var p2 = new Point(projectedPoints[tri.V2].X, projectedPoints[tri.V2].Y);

                    context?.DrawLine(wireframePen, p0, p1);
                    context?.DrawLine(wireframePen, p1, p2);
                    context?.DrawLine(wireframePen, p2, p0);
                }
            }
        }
        else
        {
            // 3. Collect and sort triangles back-to-front (depth buffer simulation)
            var sortedTriangles = new System.Collections.Generic.List<(O3dTriangle Triangle, float Depth)>(mesh.Triangles.Count);
            foreach (var tri in mesh.Triangles)
            {
                if (tri.V0 < projectedPoints.Length && tri.V1 < projectedPoints.Length && tri.V2 < projectedPoints.Length)
                {
                    float z0 = projectedPoints[tri.V0].Z;
                    float z1 = projectedPoints[tri.V1].Z;
                    float z2 = projectedPoints[tri.V2].Z;
                    float avgZ = (z0 + z1 + z2) / 3f;
                    sortedTriangles.Add((tri, avgZ));
                }
            }

            // Ascending order: smaller/negative depth (further away) first, larger/positive depth (closer) last
            sortedTriangles.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            int w = (int)Bounds.Width;
            int h = (int)Bounds.Height;

            if (w > 0 && h > 0)
            {
                byte[] cpuBuffer = new byte[w * h * 4];
                // Fill background slate color (#1a1a1e)
                for (int idx = 0; idx < cpuBuffer.Length; idx += 4)
                {
                    cpuBuffer[idx] = 26;     // R
                    cpuBuffer[idx + 1] = 26; // G
                    cpuBuffer[idx + 2] = 30; // B
                    cpuBuffer[idx + 3] = 255;// A
                }

                foreach (var item in sortedTriangles)
                {
                    var tri = item.Triangle;
                    var pt0 = projectedPoints[tri.V0];
                    var pt1 = projectedPoints[tri.V1];
                    var pt2 = projectedPoints[tri.V2];

                    // Compute normal in view-space to shade the triangle face
                    Vector3 v0_orig = new Vector3(_fitVertices[tri.V0 * 3], _fitVertices[tri.V0 * 3 + 1], _fitVertices[tri.V0 * 3 + 2]);
                    Vector3 v1_orig = new Vector3(_fitVertices[tri.V1 * 3], _fitVertices[tri.V1 * 3 + 1], _fitVertices[tri.V1 * 3 + 2]);
                    Vector3 v2_orig = new Vector3(_fitVertices[tri.V2 * 3], _fitVertices[tri.V2 * 3 + 1], _fitVertices[tri.V2 * 3 + 2]);

                    Vector3 v0_view = Vector3.Transform(v0_orig, cameraMatrix);
                    Vector3 v1_view = Vector3.Transform(v1_orig, cameraMatrix);
                    Vector3 v2_view = Vector3.Transform(v2_orig, cameraMatrix);

                    Vector3 crossNormal = MaterialColorSelector.CalculateViewSpaceNormal(v0_view, v1_view, v2_view);

                    // Compute flat normal shading intensity factor
                    float intensity = 0f;
                    float len = crossNormal.Length();
                    if (len > 1e-6f)
                    {
                        Vector3 normalized = crossNormal / len;
                        intensity = Math.Abs(normalized.Z);
                    }
                    float intensityFactor = 0.4f + 0.6f * intensity;

                    renderedCount++;
                    TextureImageData? texture = null;
                    float u0 = 0f, v0 = 0f;
                    float u1 = 0f, v1 = 0f;
                    float u2 = 0f, v2 = 0f;

                    bool isTextured = false;
                    if (tri.MaterialSlotIndex >= 0 && mesh.MaterialSlots != null && tri.MaterialSlotIndex < mesh.MaterialSlots.Count)
                    {
                        var binding = TextureBindings?.FirstOrDefault(b => b.MaterialIndex == tri.MaterialSlotIndex);
                        if (binding != null && binding.Status == TextureBindingStatus.Bound && binding.Image != null)
                        {
                            texture = binding.Image;
                            isTextured = true;
                            u0 = mesh.Vertices[tri.V0].Uv.U;
                            v0 = mesh.Vertices[tri.V0].Uv.V;
                            u1 = mesh.Vertices[tri.V1].Uv.U;
                            v1 = mesh.Vertices[tri.V1].Uv.V;
                            u2 = mesh.Vertices[tri.V2].Uv.U;
                            v2 = mesh.Vertices[tri.V2].Uv.V;
                        }
                    }

                    if (isTextured)
                    {
                        texturedCount++;
                    }
                    else
                    {
                        fallbackCount++;
                    }

                    // Fallback to solid material color if no texture is bound
                    if (texture == null)
                    {
                        Color baseColor = MaterialColorSelector.GetMaterialColor(mesh, tri.MaterialSlotIndex);
                        texture = new TextureImageData
                        {
                            Width = 1,
                            Height = 1,
                            PixelsRgba32 = new byte[] { baseColor.R, baseColor.G, baseColor.B, 255 }
                        };
                        u0 = 0f; v0 = 0f;
                        u1 = 0f; v1 = 0f;
                        u2 = 0f; v2 = 0f;
                    }

                    // Rasterize onto CPU buffer
                    if (context != null)
                    {
                        SoftwareTexturedTriangleRasterizer.Rasterize(
                            cpuBuffer, w, h,
                            pt0.X, pt0.Y,
                            pt1.X, pt1.Y,
                            pt2.X, pt2.Y,
                            u0, v0,
                            u1, v1,
                            u2, v2,
                            texture,
                            intensityFactor
                        );
                    }
                }

                if (context != null)
                {
                    if (_renderBitmap == null || _renderBitmap.PixelSize.Width != w || _renderBitmap.PixelSize.Height != h)
                    {
                        _renderBitmap?.Dispose();
                        _renderBitmap = new WriteableBitmap(
                            new PixelSize(w, h),
                            new Avalonia.Vector(96, 96),
                            Avalonia.Platform.PixelFormat.Rgba8888,
                            Avalonia.Platform.AlphaFormat.Opaque);
                    }

                    using (var locked = _renderBitmap.Lock())
                    {
                        System.Runtime.InteropServices.Marshal.Copy(cpuBuffer, 0, locked.Address, cpuBuffer.Length);
                    }

                    context.DrawImage(_renderBitmap, new Rect(0, 0, w, h), new Rect(0, 0, w, h));
                }

                // Draw wireframe overlay if Solid + Wireframe
                if (VisualMode == SoftwareViewportVisualMode.SolidWireframe && context != null)
                {
                    foreach (var item in sortedTriangles)
                    {
                        var tri = item.Triangle;
                        var pt0 = projectedPoints[tri.V0];
                        var pt1 = projectedPoints[tri.V1];
                        var pt2 = projectedPoints[tri.V2];

                        var p0 = new Point(pt0.X, pt0.Y);
                        var p1 = new Point(pt1.X, pt1.Y);
                        var p2 = new Point(pt2.X, pt2.Y);

                        context.DrawLine(wireframePen, p0, p1);
                        context.DrawLine(wireframePen, p1, p2);
                        context.DrawLine(wireframePen, p2, p0);
                    }
                }
            }
        }

        // 4. Draw bounding box if requested
        if (ShowBoundingBox && context != null)
        {
            var bboxPen = new Pen(new SolidColorBrush(Color.Parse("#f59e0b")), 1.0, dashStyle: DashStyle.Dash);

            var corners = new Point[8];
            Vector3 proj;
            proj = Project(_minX * _scale + _offsetX, _minY * _scale + _offsetY, _minZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[0] = new Point(proj.X, proj.Y);
            proj = Project(_maxX * _scale + _offsetX, _minY * _scale + _offsetY, _minZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[1] = new Point(proj.X, proj.Y);
            proj = Project(_maxX * _scale + _offsetX, _maxY * _scale + _offsetY, _minZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[2] = new Point(proj.X, proj.Y);
            proj = Project(_minX * _scale + _offsetX, _maxY * _scale + _offsetY, _minZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[3] = new Point(proj.X, proj.Y);

            proj = Project(_minX * _scale + _offsetX, _minY * _scale + _offsetY, _maxZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[4] = new Point(proj.X, proj.Y);
            proj = Project(_maxX * _scale + _offsetX, _minY * _scale + _offsetY, _maxZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[5] = new Point(proj.X, proj.Y);
            proj = Project(_maxX * _scale + _offsetX, _maxY * _scale + _offsetY, _maxZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[6] = new Point(proj.X, proj.Y);
            proj = Project(_minX * _scale + _offsetX, _maxY * _scale + _offsetY, _maxZ * _scale + _offsetZ, cameraMatrix, centerX, centerY, viewportScale);
            corners[7] = new Point(proj.X, proj.Y);

            // Bottom face
            context.DrawLine(bboxPen, corners[0], corners[1]);
            context.DrawLine(bboxPen, corners[1], corners[2]);
            context.DrawLine(bboxPen, corners[2], corners[3]);
            context.DrawLine(bboxPen, corners[3], corners[0]);

            // Top face
            context.DrawLine(bboxPen, corners[4], corners[5]);
            context.DrawLine(bboxPen, corners[5], corners[6]);
            context.DrawLine(bboxPen, corners[6], corners[7]);
            context.DrawLine(bboxPen, corners[7], corners[4]);

            // Vertical edges connecting bottom to top
            context.DrawLine(bboxPen, corners[0], corners[4]);
            context.DrawLine(bboxPen, corners[1], corners[5]);
            context.DrawLine(bboxPen, corners[2], corners[6]);
            context.DrawLine(bboxPen, corners[3], corners[7]);
        }
        }
        ScheduleDebugCountersUpdate();
    }

    private ILocalizationService? _locService;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_locService != null)
        {
            _locService.CultureChanged -= OnCultureChanged;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _locService = vm.L;
            if (_locService != null)
            {
                _locService.CultureChanged += OnCultureChanged;
            }
        }
        else
        {
            _locService = null;
        }

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_locService != null)
        {
            _locService.CultureChanged -= OnCultureChanged;
            _locService = null;
        }
        _renderBitmap?.Dispose();
        _renderBitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
