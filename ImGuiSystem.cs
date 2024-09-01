using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

namespace StrideCommunity.ImGuiDebug;

public class ImGuiSystem : GameSystemBase
{
    private const int INITIAL_VERTEX_BUFFER_SIZE = 128;
    private const int INITIAL_INDEX_BUFFER_SIZE = 128;

    [FixedAddressValueType] private static SetClipboardDelegate setClipboardFn;

    [FixedAddressValueType] private static GetClipboardDelegate getClipboardFn;

    private readonly GraphicsContext _context;
    private readonly GraphicsDevice _device;
    private readonly EffectSystem _effectSystem;

    // dependencies
    private readonly InputManager _input;

    private readonly Dictionary<Keys, ImGuiKey> _keys = [];
    public readonly ImGuiContextPtr ImGuiContext;
    private CommandList _commandList;
    private Texture _fontTexture;

    // device objects
    private PipelineState _imPipeline;
    private EffectInstance _imShader;
    private VertexDeclaration _imVertLayout;
    private IndexBufferBinding _indexBinding;

    private ImGuiIOPtr _io;
    private float _scale = 1;
    private VertexBufferBinding _vertexBinding;

    public ImGuiSystem([NotNull] IServiceRegistry registry, [NotNull] GraphicsDeviceManager graphicsDeviceManager,
        InputManager inputManager = null) : base(registry)
    {
        _input = inputManager ?? Services.GetService<InputManager>();
        Debug.Assert(_input != null, "ImGuiSystem: InputManager must be available!");

        var deviceManager = graphicsDeviceManager;
        Debug.Assert(deviceManager != null, "ImGuiSystem: GraphicsDeviceManager must be available!");

        _device = deviceManager.GraphicsDevice;
        Debug.Assert(_device != null, "ImGuiSystem: GraphicsDevice must be available!");

        _context = Services.GetService<GraphicsContext>();
        Debug.Assert(_context != null, "ImGuiSystem: GraphicsContext must be available!");

        _effectSystem = Services.GetService<EffectSystem>();
        Debug.Assert(_effectSystem != null, "ImGuiSystem: EffectSystem must be available!");

        ImGuiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(ImGuiContext);
        var theme = new DarkTheme();
        theme.ApplyTheme(ImGui.GetStyle());
        _io = ImGui.GetIO();

        // SETTO
        SetupInput();

        // vbos etc
        CreateDeviceObjects();

        // font stuff
        CreateFontTexture();

        Enabled = true; // Force Update functions to be run
        Visible = true; // Force Draw related functions to be run
        UpdateOrder = 1; // Update should occur after Stride's InputManager
    }

    public ImGuiScene ImGuiScene { get; set; }

    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            CreateFontTexture();
        }
    }

    protected override void Destroy()
    {
        ImGui.DestroyContext(ImGuiContext);
        base.Destroy();
    }

    private unsafe void SetupInput()
    {
        // keyboard nav yes
        _io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        _keys.Add(Keys.Tab, ImGuiKey.Tab);
        _keys.Add(Keys.Left, ImGuiKey.LeftArrow);
        _keys.Add(Keys.Right, ImGuiKey.RightArrow);
        _keys.Add(Keys.Up, ImGuiKey.UpArrow);
        _keys.Add(Keys.Down, ImGuiKey.DownArrow);
        _keys.Add(Keys.PageUp, ImGuiKey.PageUp);
        _keys.Add(Keys.PageDown, ImGuiKey.PageDown);
        _keys.Add(Keys.Home, ImGuiKey.Home);
        _keys.Add(Keys.End, ImGuiKey.End);
        _keys.Add(Keys.Delete, ImGuiKey.Delete);
        _keys.Add(Keys.Back, ImGuiKey.Backspace);
        _keys.Add(Keys.Enter, ImGuiKey.Enter);
        _keys.Add(Keys.Escape, ImGuiKey.Escape);
        _keys.Add(Keys.Space, ImGuiKey.Space);
        _keys.Add(Keys.A, ImGuiKey.A);
        _keys.Add(Keys.C, ImGuiKey.C);
        _keys.Add(Keys.V, ImGuiKey.V);
        _keys.Add(Keys.X, ImGuiKey.X);
        _keys.Add(Keys.Y, ImGuiKey.Y);
        _keys.Add(Keys.Z, ImGuiKey.Z);

        setClipboardFn = SetClipboard;
        getClipboardFn = GetClipboard;

        _io.SetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(setClipboardFn);
        _io.GetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(getClipboardFn);
    }

    private void SetClipboard(IntPtr data)
    {
    }

    private unsafe IntPtr GetClipboard()
    {
        return (nint)_io.ClipboardUserData;
    }

    private void CreateDeviceObjects()
    {
        // set up a commandlist
        _commandList = _context.CommandList;

        // compile de shader
        _imShader = new EffectInstance(_effectSystem.LoadEffect("ImGuiShader").WaitForResult());
        _imShader.UpdateEffect(_device);

        var layout = new VertexDeclaration(
            VertexElement.Position<Vector2>(),
            VertexElement.TextureCoordinate<Vector2>(),
            VertexElement.Color(PixelFormat.R8G8B8A8_UNorm)
        );

        _imVertLayout = layout;

        // de pipeline desc
        var pipeline = new PipelineStateDescription
        {
            BlendState = BlendStates.NonPremultiplied,

            RasterizerState = new RasterizerStateDescription
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultisampleAntiAliasLine = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0
            },

            PrimitiveType = PrimitiveType.TriangleList,
            InputElements = _imVertLayout.CreateInputElements(),
            DepthStencilState = DepthStencilStates.Default,

            EffectBytecode = _imShader.Effect.Bytecode,
            RootSignature = _imShader.RootSignature,

            Output = new RenderOutputDescription(PixelFormat.R8G8B8A8_UNorm)
        };

        // finally set up the pipeline
        var pipelineState = PipelineState.New(_device, ref pipeline);
        _imPipeline = pipelineState;

        CreateBuffers(INITIAL_VERTEX_BUFFER_SIZE, INITIAL_INDEX_BUFFER_SIZE);
    }

    private unsafe void CreateFontTexture()
    {
        _io.Fonts.Clear();
        // font data, important
        var text = _io.Fonts.AddFontDefault();
        text.Scale = Scale;

        byte* pixelData;
        int width;
        int height;
        int bytesPerPixel;
        _io.Fonts.GetTexDataAsRGBA32(&pixelData, &width, &height, &bytesPerPixel);

        var newFontTexture = Texture.New2D(_device, width, height, PixelFormat.R8G8B8A8_UNorm);
        newFontTexture.SetData(_commandList, new DataPointer(pixelData, width * height * bytesPerPixel));

        _fontTexture = newFontTexture;
    }

    public override void Update(GameTime gameTime)
    {
        var surfaceSize = Game.Window.ClientBounds;
        _io.DisplaySize = new System.Numerics.Vector2(surfaceSize.Width, surfaceSize.Height);
        _io.DeltaTime = (float)gameTime.TimePerFrame.TotalSeconds;

        if (_input.HasMouse == false || _input.IsMousePositionLocked == false)
        {
            var mousePos = _input.AbsoluteMousePosition;
            _io.MousePos = new System.Numerics.Vector2(mousePos.X, mousePos.Y);

            if (_io.WantTextInput)
                _input.TextInput.EnabledTextInput();
            else
                _input.TextInput.DisableTextInput();

            // handle input events
            foreach (var ev in _input.Events)
                switch (ev)
                {
                    case TextInputEvent tev:
                        if (tev.Text == "\t") continue;
                        _io.AddInputCharactersUTF8(tev.Text);
                        break;
                    case KeyEvent kev:
                        if (_keys.TryGetValue(kev.Key, out var imGuiKey))
                            _io.AddKeyEvent(imGuiKey, _input.IsKeyDown(kev.Key));
                        break;
                    case MouseWheelEvent mw:
                        _io.MouseWheel += mw.WheelDelta;
                        break;
                }

            var mouseDown = _io.MouseDown;
            mouseDown[0] = _input.IsMouseButtonDown(MouseButton.Left);
            mouseDown[1] = _input.IsMouseButtonDown(MouseButton.Right);
            mouseDown[2] = _input.IsMouseButtonDown(MouseButton.Middle);

            _io.KeyAlt = _input.IsKeyDown(Keys.LeftAlt) || _input.IsKeyDown(Keys.RightAlt);
            _io.KeyShift = _input.IsKeyDown(Keys.LeftShift) || _input.IsKeyDown(Keys.RightShift);
            _io.KeyCtrl = _input.IsKeyDown(Keys.LeftCtrl) || _input.IsKeyDown(Keys.RightCtrl);
            _io.KeySuper = _input.IsKeyDown(Keys.LeftWin) || _input.IsKeyDown(Keys.RightWin);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        ImGui.NewFrame();
        ImGuiScene?.Draw(this, gameTime);
    }

    public override void EndDraw()
    {
        ImGui.Render();
        RenderDrawLists(ImGui.GetDrawData());
    }

    private void CreateBuffers(int vtxCount, int idxCount)
    {
        var totalVboSize = (uint)(vtxCount * Unsafe.SizeOf<ImDrawVert>());
        if (totalVboSize > (_vertexBinding.Buffer?.SizeInBytes ?? 0))
        {
            var vertexBuffer = Buffer.Vertex.New(_device, (int)(totalVboSize * 1.5f));
            _vertexBinding = new VertexBufferBinding(vertexBuffer, _imVertLayout, 0);
        }

        var totalIboSize = (uint)(idxCount * sizeof(ushort));
        if (totalIboSize > (_indexBinding?.Buffer?.SizeInBytes ?? 0))
        {
            var is32Bits = false;
            var indexBuffer = Buffer.Index.New(_device, (int)(totalIboSize * 1.5f));
            _indexBinding = new IndexBufferBinding(indexBuffer, is32Bits, 0);
        }
    }

    private unsafe void WriteToBuffers(ImDrawDataPtr drawData)
    {
        // copy de dators
        var vtxOffsetBytes = 0;
        var idxOffsetBytes = 0;

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            _vertexBinding.Buffer.SetData(_commandList,
                new DataPointer(cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()),
                vtxOffsetBytes);
            _indexBinding.Buffer.SetData(_commandList,
                new DataPointer(cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort)), idxOffsetBytes);
            vtxOffsetBytes += cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
            idxOffsetBytes += cmdList.IdxBuffer.Size * sizeof(ushort);
        }
    }

    private void RenderDrawLists(ImDrawDataPtr drawData)
    {
        // view proj
        var surfaceSize = Game.Window.ClientBounds;
        var projMatrix = Matrix.OrthoRH(surfaceSize.Width, -surfaceSize.Height, -1, 1);

        CreateBuffers(drawData.TotalVtxCount, drawData.TotalIdxCount); // potentially resize buffers first if needed
        WriteToBuffers(drawData); // updeet em now

        // set pipeline stuff
        var is32Bits = false;
        _commandList.SetPipelineState(_imPipeline);
        _commandList.SetVertexBuffer(0, _vertexBinding.Buffer, 0, Unsafe.SizeOf<ImDrawVert>());
        _commandList.SetIndexBuffer(_indexBinding.Buffer, 0, is32Bits);
        _imShader.Parameters.Set(ImGuiShaderKeys.tex, _fontTexture);

        var vtxOffset = 0;
        var idxOffset = 0;
        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            for (var i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var cmd = cmdList.CmdBuffer[i];

                if (cmd.TextureId != IntPtr.Zero)
                {
                    // imShader.Parameters.Set(ImGuiShaderKeys.tex, fontTexture);
                }
                else
                {
                    _commandList.SetScissorRectangle(
                        new Rectangle(
                            (int)cmd.ClipRect.X,
                            (int)cmd.ClipRect.Y,
                            (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                            (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                        )
                    );

                    _imShader.Parameters.Set(ImGuiShaderKeys.tex, _fontTexture);
                    _imShader.Parameters.Set(ImGuiShaderKeys.proj, ref projMatrix);
                    _imShader.Apply(_context);

                    _commandList.DrawIndexed((int)cmd.ElemCount, idxOffset, vtxOffset);
                }

                idxOffset += (int)cmd.ElemCount;
            }

            vtxOffset += cmdList.VtxBuffer.Size;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetClipboardDelegate(IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetClipboardDelegate();
}