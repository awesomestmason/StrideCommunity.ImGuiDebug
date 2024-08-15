using System.Numerics;
using Hexa.NET.ImGui;
using Stride.Engine;
using Stride.Games;
using static StrideCommunity.ImGuiDebug.ImGuiExtension;

namespace StrideCommunity.ImGuiDebug;

public class ImGuiWindow : ImGuiComponentBase
{
    private ImGuiComponentBase? _content;
    public ImGuiComponentBase? Content
    {
        get => _content;
        set
        {
            if (value != null)
            {
                value.ImGuiScene = ImGuiScene;
            }
            _content = value;
        }
    }
    private static readonly Dictionary<string, uint> _windowId = new();
    private readonly string _uniqueName;
    private uint Id;

    private bool Open = true;


    public ImGuiWindow(ImGuiComponentBase? content = null)
    {
        var n = GetType().Name;
        lock (_windowId)
        {
            if (_windowId.TryGetValue(n, out Id) == false)
            {
                Id = 1;
                _windowId.Add(n, Id);
            }

            _windowId[n] = Id + 1;
        }

        _uniqueName = Id == 1 ? n : $"{n}({Id})";
        
        Content = content;
    }
    public ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;
    public Vector2? WindowPos = null;
    public Vector2? WindowSize = null;

    public override void Draw(ImGuiSystem imGui, GameTime gameTime)
    {

        if (WindowPos != null)
            ImGui.SetNextWindowPos(WindowPos.Value);
        if (WindowSize != null)
            ImGui.SetNextWindowSize(WindowSize.Value);
        using (Window(_uniqueName, ref Open, out var collapsed, WindowFlags))
        {
            if (!collapsed && Content is not null)
            {                
                Content.Draw(imGui, gameTime);
            }
        }

        if (Open == false)
        {
            ImGuiScene?.Remove(this);
        }
    }
    
    
}