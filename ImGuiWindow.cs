using System.Numerics;
using Hexa.NET.ImGui;
using Stride.Games;
using static StrideCommunity.ImGuiDebug.ImGuiExtension;

namespace StrideCommunity.ImGuiDebug;

public class ImGuiWindow : ImGuiComponentBase
{
    private static readonly Dictionary<string, uint> _windowId = new();
    private readonly string _uniqueName;
    private ImGuiComponentBase? _content;
    private readonly uint Id;

    private bool Open = true;
    public ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;
    public Vector2? WindowPos = null;
    public Vector2? WindowSize = null;


    public ImGuiWindow(string title, ImGuiComponentBase? content = null)
    {
        lock (_windowId)
        {
            if (_windowId.TryGetValue(title, out Id) == false)
            {
                Id = 1;
                _windowId.Add(title, Id);
            }

            _windowId[title] = Id + 1;
        }

        _uniqueName = Id == 1 ? title : $"{title}({Id})";

        Content = content;
    }

    public ImGuiComponentBase? Content
    {
        get => _content;
        set
        {
            if (value != null) value.ImGuiScene = ImGuiScene;
            _content = value;
        }
    }

    public override void Draw(ImGuiSystem imGui, GameTime gameTime)
    {
        if (WindowPos != null)
            ImGui.SetNextWindowPos(WindowPos.Value);
        if (WindowSize != null)
            ImGui.SetNextWindowSize(WindowSize.Value);
        using (Window(_uniqueName, ref Open, out var collapsed, WindowFlags))
        {
            if (!collapsed && Content is not null) Content.Draw(imGui, gameTime);
        }

        if (Open == false) ImGuiScene?.Remove(this);
    }
}