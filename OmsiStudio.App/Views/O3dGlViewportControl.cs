using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.App.ViewModels;

namespace OmsiStudio.App.Views;

/// <summary>
/// A custom Avalonia control that integrates an OpenGL graphics context and pointer orbit/zoom handling.
/// </summary>
public class O3dGlViewportControl : OpenGlControlBase
{
    private Silk.NET.OpenGL.GL? _glApi;
    private IRendererHost? _lastRendererHost;

    static O3dGlViewportControl()
    {
        AffectsRender<O3dGlViewportControl>(CameraYawProperty, CameraPitchProperty, CameraDistanceProperty);
        CameraYawProperty.Changed.AddClassHandler<O3dGlViewportControl>((x, e) => x.OnCameraPropertyChanged());
        CameraPitchProperty.Changed.AddClassHandler<O3dGlViewportControl>((x, e) => x.OnCameraPropertyChanged());
        CameraDistanceProperty.Changed.AddClassHandler<O3dGlViewportControl>((x, e) => x.OnCameraPropertyChanged());
        RenderVersionProperty.Changed.AddClassHandler<O3dGlViewportControl>((x, e) => x.OnRenderVersionChanged());
        RendererHostProperty.Changed.AddClassHandler<O3dGlViewportControl>((x, e) => x.OnRendererHostPropertyChanged());
    }

    private void RequestFrameSoon()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RequestNextFrameRendering();
#pragma warning disable CS0618
            InvalidateVisual();
#pragma warning restore CS0618
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void OnCameraPropertyChanged()
    {
        RequestFrameSoon();
    }

    private void OnRenderVersionChanged()
    {
        RequestFrameSoon();
    }

    private void OnRendererHostPropertyChanged()
    {
        InitializeRendererHostIfNeeded();
    }

    /// <summary>
    /// Defines the <see cref="RenderVersion"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<int> RenderVersionProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, int>(nameof(RenderVersion), defaultValue: 0);

    /// <summary>
    /// Gets or sets the current rendering version. When changed, forces a viewport redraw.
    /// </summary>
    public int RenderVersion
    {
        get => GetValue(RenderVersionProperty);
        set => SetValue(RenderVersionProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="RendererHost"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<IRendererHost?> RendererHostProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, IRendererHost?>(nameof(RendererHost));

    /// <summary>
    /// Gets or sets the renderer host driving this viewport.
    /// </summary>
    public IRendererHost? RendererHost
    {
        get => GetValue(RendererHostProperty);
        set => SetValue(RendererHostProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="CameraYaw"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<float> CameraYawProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, float>(nameof(CameraYaw), defaultValue: 45f, defaultBindingMode: BindingMode.TwoWay);

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
        AvaloniaProperty.Register<O3dGlViewportControl, float>(nameof(CameraPitch), defaultValue: -30f, defaultBindingMode: BindingMode.TwoWay);

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
        AvaloniaProperty.Register<O3dGlViewportControl, float>(nameof(CameraDistance), defaultValue: 5f, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Gets or sets the current camera zoom/distance.
    /// </summary>
    public float CameraDistance
    {
        get => GetValue(CameraDistanceProperty);
        set => SetValue(CameraDistanceProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="HasError"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, bool>(nameof(HasError), defaultValue: false);

    private bool _hasError;

    /// <summary>
    /// Gets a value indicating whether the viewport initialization or rendering encountered an error.
    /// </summary>
    public bool HasError
    {
        get => _hasError;
        private set
        {
            _hasError = value;
            SetValue(HasErrorProperty, value);
        }
    }

    /// <summary>
    /// Defines the <see cref="ErrorMessage"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, string?>(nameof(ErrorMessage));

    private string? _errorMessage;

    /// <summary>
    /// Gets the detailed error message if initialization or rendering failed.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            SetValue(ErrorMessageProperty, value);
        }
    }

    public static readonly StyledProperty<bool> IsOpenGlInitializedProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, bool>(nameof(IsOpenGlInitialized), defaultValue: false, defaultBindingMode: BindingMode.TwoWay);

    private bool _isOpenGlInitialized;
    public bool IsOpenGlInitialized
    {
        get => _isOpenGlInitialized;
        private set
        {
            _isOpenGlInitialized = value;
            SetValue(IsOpenGlInitializedProperty, value);
        }
    }

    public static readonly StyledProperty<bool> IsRendererHostInitializedProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, bool>(nameof(IsRendererHostInitialized), defaultValue: false, defaultBindingMode: BindingMode.TwoWay);

    private bool _isRendererHostInitialized;
    public bool IsRendererHostInitialized
    {
        get => _isRendererHostInitialized;
        private set
        {
            _isRendererHostInitialized = value;
            SetValue(IsRendererHostInitializedProperty, value);
        }
    }

    public static readonly StyledProperty<int> OpenGlRenderCallCountProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, int>(nameof(OpenGlRenderCallCount), defaultValue: 0, defaultBindingMode: BindingMode.TwoWay);

    private int _openGlRenderCallCount;
    public int OpenGlRenderCallCount
    {
        get => _openGlRenderCallCount;
        private set
        {
            _openGlRenderCallCount = value;
            SetValue(OpenGlRenderCallCountProperty, value);
        }
    }

    public static readonly StyledProperty<string?> LastViewportErrorProperty =
        AvaloniaProperty.Register<O3dGlViewportControl, string?>(nameof(LastViewportError), defaultValue: null, defaultBindingMode: BindingMode.TwoWay);

    private string? _lastViewportError;
    public string? LastViewportError
    {
        get => _lastViewportError;
        private set
        {
            _lastViewportError = value;
            SetValue(LastViewportErrorProperty, value);
        }
    }

    private Point _lastPointerPosition;
    private bool _isDragging;

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

            CameraYaw = CameraDeltaCalculator.CalculateYaw(CameraYaw, delta.X, yawSensitivity);
            CameraPitch = CameraDeltaCalculator.CalculatePitch(CameraPitch, delta.Y, pitchSensitivity);

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
        CameraDistance = CameraDeltaCalculator.CalculateDistance(CameraDistance, e.Delta.Y, zoomSensitivity);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        try
        {
            HasError = false;
            ErrorMessage = null;
            LastViewportError = null;
            IsOpenGlInitialized = true;

            _glApi = Silk.NET.OpenGL.GL.GetApi(proc => gl.GetProcAddress(proc));
            
            InitializeRendererHostIfNeeded();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"OpenGL initialization exception: {ex.Message}";
            LastViewportError = ErrorMessage;
            IsOpenGlInitialized = false;
        }
    }

    private void InitializeRendererHostIfNeeded()
    {
        if (_glApi == null || RendererHost == null)
        {
            return;
        }

        if (_lastRendererHost == RendererHost && RendererHost.State == RendererHostState.Initialized)
        {
            return;
        }

        try
        {
            HasError = false;
            ErrorMessage = null;

            // Detach GL from the previous host if it changed
            if (_lastRendererHost != null && _lastRendererHost != RendererHost)
            {
                if (_lastRendererHost is OpenGlRendererHost oldGlHost)
                {
                    oldGlHost.DetachGl();
                }
            }

            _lastRendererHost = RendererHost;

            if (RendererHost is OpenGlRendererHost openGlHost)
            {
                openGlHost.SetGl(_glApi);
            }

            var initTask = RendererHost.InitializeAsync();
            var initResult = initTask.GetAwaiter().GetResult();

            if (!initResult.IsSuccess)
            {
                HasError = true;
                ErrorMessage = initResult.ErrorMessage ?? "OpenGL host initialization failed.";
                LastViewportError = ErrorMessage;
                IsRendererHostInitialized = false;
            }
            else
            {
                IsRendererHostInitialized = true;
                RequestFrameSoon();
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"OpenGL host initialization exception: {ex.Message}";
            LastViewportError = ErrorMessage;
            IsRendererHostInitialized = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestFrameSoon();
    }

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        try
        {
            if (RendererHost is OpenGlRendererHost openGlHost)
            {
                openGlHost.DetachGl();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during OpenGL deinit: {ex}");
        }

        _lastRendererHost = null;
        _glApi = null;
        IsOpenGlInitialized = false;
        IsRendererHostInitialized = false;
        base.OnOpenGlDeinit(gl);
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        OpenGlRenderCallCount++;

        if (HasError || RendererHost == null)
        {
            return;
        }

        try
        {
            // Propagate control boundaries to renderer host
            var bounds = Bounds;
            RendererHost.Resize(new RendererHostSize { Width = bounds.Width, Height = bounds.Height });

            // Run rendering pass
            var renderResult = RendererHost.RenderFrame();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.NotifyRendererDebugStateChanged();
            }

            if (!renderResult.IsSuccess)
            {
                HasError = true;
                ErrorMessage = renderResult.ErrorMessage ?? "Frame rendering failed.";
                LastViewportError = ErrorMessage;
            }
            else
            {
                // Request next frame rendering to keep viewport updated if debug triangle or mesh preview is enabled
                if (RendererHost.DebugTriangleEnabled || RendererHost.CurrentMesh != null)
                {
                    RequestFrameSoon();
                }
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"OpenGL rendering exception: {ex.Message}";
            LastViewportError = ErrorMessage;
        }
    }
}
