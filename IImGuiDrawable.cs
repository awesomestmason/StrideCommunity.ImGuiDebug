using Stride.Games;

namespace StrideCommunity.ImGuiDebug;

public interface IImGuiDrawable
{
    void Draw(ImGuiSystem imGui, GameTime gameTime);
}