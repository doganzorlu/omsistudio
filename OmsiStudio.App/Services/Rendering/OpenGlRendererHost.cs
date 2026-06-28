using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using Silk.NET.OpenGL;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A real OpenGL implementation of <see cref="IRendererHost"/> powered by Silk.NET.
/// Renders O3D mesh geometry in wireframe mode.
/// </summary>
public sealed class OpenGlRendererHost : IRendererHost
{
    private GL? _gl;
    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private uint _indexCount;
    private bool _meshDirty;

    /// <inheritdoc />
    public RendererHostState State { get; private set; } = RendererHostState.Uninitialized;

    /// <inheritdoc />
    public O3dMeshData? CurrentMesh { get; private set; }

    /// <inheritdoc />
    public PreviewCameraState? CameraState { get; private set; }

    /// <inheritdoc />
    public PreviewRenderOptions? RenderOptions { get; private set; }

    /// <inheritdoc />
    public RendererHostSize CurrentSize { get; private set; } = new() { Width = 0, Height = 0 };

    /// <summary>
    /// Gets the number of vertices uploaded in the current mesh.
    /// </summary>
    public int UploadedVertexCount { get; private set; }

    /// <summary>
    /// Gets the number of indices uploaded in the current mesh.
    /// </summary>
    public int UploadedIndexCount { get; private set; }

    private bool _debugTriangleEnabled;

    /// <inheritdoc />
    public bool DebugTriangleEnabled
    {
        get => _debugTriangleEnabled;
        set
        {
            if (_debugTriangleEnabled != value)
            {
                _debugTriangleEnabled = value;
                _meshDirty = true;
            }
        }
    }

    /// <inheritdoc />
    public bool LastFrameDrawAttempted { get; private set; }

    /// <inheritdoc />
    public int LastFrameUploadedVertexCount { get; private set; }

    /// <inheritdoc />
    public int LastFrameUploadedIndexCount { get; private set; }

    /// <inheritdoc />
    public string LastGlError { get; private set; } = "NoError";

    /// <summary>
    /// Configures the active Silk.NET OpenGL API instance.
    /// </summary>
    /// <param name="gl">The loaded Silk.NET OpenGL API interface.</param>
    public void SetGl(GL gl)
    {
        _gl = gl;
    }

    /// <inheritdoc />
    public Task<RendererInitializationResult> InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        if (_gl == null)
        {
            State = RendererHostState.Failed;
            return Task.FromResult(RendererInitializationResult.Failure("OpenGL API has not been configured."));
        }

        try
        {
            // Set a premium slate-like clear color (rgb: 26, 26, 30) matching OmsiStudio theme
            _gl.ClearColor(0.1f, 0.1f, 0.12f, 1.0f);
            
            // Enable depth testing
            _gl.Enable(EnableCap.DepthTest);

            // Compile shaders and build program
            CompileShaders();

            State = RendererHostState.Initialized;
            return Task.FromResult(RendererInitializationResult.Success());
        }
        catch (Exception ex)
        {
            State = RendererHostState.Failed;
            return Task.FromResult(RendererInitializationResult.Failure($"OpenGL initialization failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public void Resize(RendererHostSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        
        CurrentSize = size;

        if (State == RendererHostState.Initialized && _gl != null)
        {
            _gl.Viewport(0, 0, (uint)size.Width, (uint)size.Height);
        }
    }

    /// <inheritdoc />
    public void SetMesh(O3dMeshData? meshData)
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        CurrentMesh = meshData;
        _meshDirty = true;
    }

    /// <inheritdoc />
    public void SetCamera(PreviewCameraState cameraState)
    {
        ArgumentNullException.ThrowIfNull(cameraState);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        CameraState = cameraState;
    }

    /// <inheritdoc />
    public void SetRenderOptions(PreviewRenderOptions renderOptions)
    {
        ArgumentNullException.ThrowIfNull(renderOptions);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        RenderOptions = renderOptions;
    }

    /// <inheritdoc />
    public RenderFrameResult RenderFrame()
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        
        if (State != RendererHostState.Initialized || _gl == null)
        {
            return RenderFrameResult.Failure("Renderer is not initialized.");
        }

        LastFrameDrawAttempted = false;
        LastGlError = "NoError";

        try
        {
            // Clear any prior unhandled errors
            _ = _gl.GetError();

            // Process mesh upload if dirty
            if (_meshDirty)
            {
                UploadMesh();
                var uploadErr = _gl.GetError();
                if (uploadErr != GLEnum.NoError)
                {
                    LastGlError = uploadErr.ToString();
                    return RenderFrameResult.Failure($"OpenGL error after mesh upload: {uploadErr}");
                }
            }

            // Clear both color and depth buffers
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            // Render mesh wireframe if loaded
            if (_vao != 0 && _indexCount > 0 && _shaderProgram != 0)
            {
                _gl.UseProgram(_shaderProgram);

                // Set camera transform uniform
                int transformLoc = _gl.GetUniformLocation(_shaderProgram, "uTransform");
                if (transformLoc != -1)
                {
                    var transform = CameraTransformCalculator.Calculate(CameraState);
                    unsafe
                    {
                        _gl.UniformMatrix4(transformLoc, 1, true, (float*)&transform);
                    }
                }

                _gl.BindVertexArray(_vao);
                
                // Set line width
                _gl.LineWidth(2.0f);

                // Draw as wireframe (lines)
                _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
                
                LastFrameDrawAttempted = true;
                LastFrameUploadedVertexCount = UploadedVertexCount;
                LastFrameUploadedIndexCount = UploadedIndexCount;

                unsafe
                {
                    _gl.DrawElements(GLEnum.Triangles, _indexCount, GLEnum.UnsignedInt, null);
                }
                
                // Restore default polygon mode
                _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                
                _gl.BindVertexArray(0);
                _gl.UseProgram(0);

                var drawErr = _gl.GetError();
                if (drawErr != GLEnum.NoError)
                {
                    LastGlError = drawErr.ToString();
                    return RenderFrameResult.Failure($"OpenGL error after draw: {drawErr}");
                }
            }

            return RenderFrameResult.Success();
        }
        catch (Exception ex)
        {
            return RenderFrameResult.Failure($"Frame rendering failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely detaches the active OpenGL context without disposing the renderer host instance.
    /// </summary>
    public void DetachGl()
    {
        CleanupMeshBuffers();
        if (_gl != null && _shaderProgram != 0)
        {
            _gl.DeleteProgram(_shaderProgram);
            _shaderProgram = 0;
        }
        _gl = null;
        if (State != RendererHostState.Disposed)
        {
            State = RendererHostState.Uninitialized;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CleanupMeshBuffers();
        if (_gl != null && _shaderProgram != 0)
        {
            _gl.DeleteProgram(_shaderProgram);
            _shaderProgram = 0;
        }
        State = RendererHostState.Disposed;
        _gl = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void CompileShaders()
    {
        if (_gl == null) return;

        uint vertexShader = 0;
        uint fragmentShader = 0;
        uint program = 0;

        try
        {
            string vertexSource = @"#version 330 core
layout (location = 0) in vec3 aPos;
uniform mat4 uTransform;
void main()
{
    gl_Position = uTransform * vec4(aPos, 1.0);
}";

            string fragmentSource = @"#version 330 core
out vec4 FragColor;
void main()
{
    FragColor = vec4(0.39f, 0.4f, 0.95f, 1.0f);
}";

            vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, vertexSource);
            _gl.CompileShader(vertexShader);
            CheckShaderCompile(vertexShader, "Vertex");

            fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, fragmentSource);
            _gl.CompileShader(fragmentShader);
            CheckShaderCompile(fragmentShader, "Fragment");

            program = _gl.CreateProgram();
            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);
            CheckProgramLink(program);

            _shaderProgram = program;
        }
        catch
        {
            if (program != 0)
            {
                _gl.DeleteProgram(program);
            }
            throw;
        }
        finally
        {
            if (vertexShader != 0)
            {
                _gl.DeleteShader(vertexShader);
            }
            if (fragmentShader != 0)
            {
                _gl.DeleteShader(fragmentShader);
            }
        }
    }

    private void CheckShaderCompile(uint shader, string type)
    {
        if (_gl == null) return;
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == (int)GLEnum.False)
        {
            string infoLog = _gl.GetShaderInfoLog(shader);
            throw new Exception($"OpenGL {type} shader compile error: {infoLog}");
        }
    }

    private void CheckProgramLink(uint program)
    {
        if (_gl == null) return;
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == (int)GLEnum.False)
        {
            string infoLog = _gl.GetProgramInfoLog(program);
            throw new Exception($"OpenGL program linking error: {infoLog}");
        }
    }

    private void UploadDebugTriangle()
    {
        if (_gl == null || State != RendererHostState.Initialized) return;

        CleanupMeshBuffers();

        float[] vertexData = new float[]
        {
             0.0f,  0.5f, 0.0f,
            -0.5f, -0.5f, 0.0f,
             0.5f, -0.5f, 0.0f
        };

        uint[] indexData = new uint[]
        {
            0, 1, 2
        };

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* pData = vertexData)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Length * sizeof(float)), pData, BufferUsageARB.StaticDraw);
            }
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        unsafe
        {
            fixed (uint* pData = indexData)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indexData.Length * sizeof(uint)), pData, BufferUsageARB.StaticDraw);
            }
        }

        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        }
        _gl.EnableVertexAttribArray(0);

        _gl.BindVertexArray(0);

        _indexCount = (uint)indexData.Length;
        UploadedVertexCount = 3;
        UploadedIndexCount = 3;
        _meshDirty = false;
    }

    private void UploadMesh()
    {
        if (_gl == null || State != RendererHostState.Initialized) return;

        CleanupMeshBuffers();

        if (DebugTriangleEnabled)
        {
            UploadDebugTriangle();
            return;
        }

        var mesh = CurrentMesh;
        if (mesh == null || mesh.Vertices.Count == 0 || mesh.Triangles.Count == 0)
        {
            _meshDirty = false;
            return;
        }

        // Pack Vertex Data (Positions X, Y, Z centered and normalized to clip space)
        var fitResult = MeshFitter.FitToClipSpace(mesh.Vertices);
        float[] vertexData = fitResult.Vertices;

        // Pack Index Data
        uint[] indexData = new uint[mesh.Triangles.Count * 3];
        for (int i = 0; i < mesh.Triangles.Count; i++)
        {
            indexData[i * 3] = (uint)mesh.Triangles[i].V0;
            indexData[i * 3 + 1] = (uint)mesh.Triangles[i].V1;
            indexData[i * 3 + 2] = (uint)mesh.Triangles[i].V2;
        }

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        // Upload vertex positions
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* pData = vertexData)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Length * sizeof(float)), pData, BufferUsageARB.StaticDraw);
            }
        }

        // Upload indices
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        unsafe
        {
            fixed (uint* pData = indexData)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indexData.Length * sizeof(uint)), pData, BufferUsageARB.StaticDraw);
            }
        }

        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        }
        _gl.EnableVertexAttribArray(0);

        _gl.BindVertexArray(0);

        _indexCount = (uint)indexData.Length;
        UploadedVertexCount = mesh.Vertices.Count;
        UploadedIndexCount = indexData.Length;
        _meshDirty = false;
    }

    private void CleanupMeshBuffers()
    {
        if (_gl == null) return;

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
            _vao = 0;
        }
        if (_vbo != 0)
        {
            _gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }
        if (_ebo != 0)
        {
            _gl.DeleteBuffer(_ebo);
            _ebo = 0;
        }
        _indexCount = 0;
        UploadedVertexCount = 0;
        UploadedIndexCount = 0;
    }
}
