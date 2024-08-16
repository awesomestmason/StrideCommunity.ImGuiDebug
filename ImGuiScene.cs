using Stride.Games;

namespace StrideCommunity.ImGuiDebug;

public class ImGuiScene : ImGuiComponentBase
{
    private readonly List<ImGuiComponentBase> _components = new();
    private readonly HashSet<ImGuiComponentBase> _toAdd = new();
    private readonly HashSet<ImGuiComponentBase> _toRemove = new();

    public IEnumerable<T> GetByType<T>() where T : ImGuiComponentBase
    {
        return _components.OfType<T>();
    }

    public void Add(ImGuiComponentBase component)
    {
        component.ImGuiScene = this;
        _toAdd.Add(component);
    }

    public void Remove(ImGuiComponentBase component)
    {
        component.ImGuiScene = null;
        _toRemove.Add(component);
    }

    public override void Draw(ImGuiSystem imGui, GameTime gameTime)
    {
        foreach (var component in _toRemove) _components.Remove(component);
        _toRemove.Clear();
        foreach (var component in _toAdd) _components.Add(component);
        _toAdd.Clear();
        foreach (var component in _components) component.Draw(imGui, gameTime);
    }
}