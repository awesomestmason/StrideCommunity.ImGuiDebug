using Stride.Games;

namespace StrideCommunity.ImGuiDebug;

public abstract class ImGuiComponentBase : IImGuiDrawable
{
    public ImGuiScene? ImGuiScene;
    public abstract void Draw(ImGuiSystem imGui, GameTime gameTime);
}