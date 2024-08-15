using Stride.Games;

namespace StrideCommunity.ImGuiDebug;

public class ImGuiScene : ImGuiComponentBase
{
    public IEnumerable<T> GetByType<T>() where T : ImGuiComponentBase
    {
        return _components.OfType<T>();
    }
    
    private readonly List<ImGuiComponentBase> _components = new();
    public void Add(ImGuiComponentBase component)
    {
        component.ImGuiScene = this;
        _components.Add(component);
    }
    public void Remove(ImGuiComponentBase component)
    {
        component.ImGuiScene = null;
        _components.Remove(component);
    }
    public override void Draw(ImGuiSystem imGui, GameTime gameTime)
    {
        foreach (var component in _components)
        {
            component.Draw(imGui, gameTime);
        }
    }
}